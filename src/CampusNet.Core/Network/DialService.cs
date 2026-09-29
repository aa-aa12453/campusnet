using System.Diagnostics;
using System.IO;
using CampusNet.Core.Configuration;
using CampusNet.Core.Credentials;
using CampusNet.Core.Logging;
using CampusNet.Core.Models;
using CampusNet.Core.Network;

namespace CampusNet.Core.Ras;

/// <summary>
/// 高层拨号服务：三级通道降级（RAS 原生 API → 临时电话簿 → rasdial.exe），
/// 拨号后验证网络真正可用，失败按设置重试。
/// </summary>
public class DialService
{
    private readonly AppLogger _log = new(LogKind.Dial);

    private static string SystemPbk => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Network", "Connections", "Pbk", "rasphone.pbk");

    /// <summary>带重试的完整拨号，用于开机自拨和守护重拨。</summary>
    public async Task<DialResult> DialAsync(DialCredential cred, AppSettings settings,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        DialResult result = DialResult.Fail(0, "未执行");
        for (int attempt = 1; attempt <= settings.MaxAttempts && !ct.IsCancellationRequested; attempt++)
        {
            progress?.Report($"第 {attempt}/{settings.MaxAttempts} 次尝试连接…");
            _log.Info($"开始第 {attempt}/{settings.MaxAttempts} 轮拨号，连接={cred.ConnectionName}，账号={Mask(cred.UserName)}");

            result = await DialOnceAsync(cred);
            if (result.Success)
            {
                progress?.Report("已连接，正在验证网络…");
                bool online = await NetworkMonitor.HasInternetAsync();
                if (online)
                {
                    _log.Info($"拨号成功并验证联网，IP={result.IpAddress}");
                    return DialResult.Ok(result.IpAddress);
                }
                _log.Warn("RAS 返回成功但网络不可达，继续重试。");
            }
            else
            {
                _log.Error($"拨号返回 {result.ErrorCode}：{result.Message}");
            }

            if (attempt < settings.MaxAttempts)
            {
                progress?.Report($"未成功，{settings.CheckIntervalSeconds} 秒后重试…");
                try { await Task.Delay(TimeSpan.FromSeconds(settings.CheckIntervalSeconds), ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        return result;
    }

    /// <summary>单次拨号（GUI 一键拨号），依次尝试三条通道。</summary>
    public async Task<DialResult> DialOnceAsync(DialCredential cred)
    {
        // 通道 1：RAS 原生 API + 系统默认电话簿
        uint rc = NativeRas.Dial(cred.ConnectionName, cred.UserName, cred.Password);
        if (rc == 0)
        {
            await Task.Delay(1500);
            return DialResult.Ok(NetworkMonitor.GetRasIpAddress(cred.ConnectionName));
        }
        _log.Warn($"通道1（RAS API）返回 {rc}。");

        // 通道 2：623 时用临时电话簿，绕开 RasMan 缓存
        if (rc == 623)
        {
            uint rc2 = await DialViaTempPbkAsync(cred);
            if (rc2 == 0)
            {
                await Task.Delay(1500);
                return DialResult.Ok(NetworkMonitor.GetRasIpAddress(cred.ConnectionName));
            }
            _log.Warn($"通道2（临时电话簿）返回 {rc2}。");
            rc = rc2;
        }

        // 通道 3：rasdial.exe 兜底
        uint rc3 = await DialViaExeAsync(cred);
        if (rc3 == 0)
        {
            await Task.Delay(1500);
            return DialResult.Ok(NetworkMonitor.GetRasIpAddress(cred.ConnectionName));
        }
        _log.Warn($"通道3（rasdial.exe）返回 {rc3}。");

        uint finalCode = rc3 != 0 ? rc3 : rc;
        return DialResult.Fail(finalCode, NativeRas.GetErrorString(finalCode));
    }

    private Task<uint> DialViaTempPbkAsync(DialCredential cred)
    {
        try
        {
            if (File.Exists(SystemPbk))
            {
                File.Copy(SystemPbk, AppPaths.TempPbk, true);
            }
            return Task.FromResult(NativeRas.Dial(cred.ConnectionName, cred.UserName, cred.Password, AppPaths.TempPbk));
        }
        catch (Exception ex)
        {
            _log.Error("临时电话簿通道异常：" + ex.Message);
            return Task.FromResult(0xFFFFu);
        }
    }

    private static async Task<uint> DialViaExeAsync(DialCredential cred)
    {
        try
        {
            var psi = new ProcessStartInfo("rasdial.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            // ArgumentList 自动处理引号转义，避免特殊字符注入
            psi.ArgumentList.Add(cred.ConnectionName);
            psi.ArgumentList.Add(cred.UserName);
            psi.ArgumentList.Add(cred.Password);

            using var proc = Process.Start(psi);
            if (proc == null) return 0xFFFF;
            await proc.WaitForExitAsync();
            return (uint)proc.ExitCode;
        }
        catch
        {
            return 0xFFFF;
        }
    }

    public bool HangUp(string entry)
    {
        _log.Info($"手动挂断连接 {entry}");
        return NativeRas.HangUp(entry);
    }

    public bool IsConnected(string entry) => NativeRas.IsConnected(entry);

    public static string Mask(string value)
    {
        if (string.IsNullOrEmpty(value)) return "(空)";
        if (value.Length <= 2) return new string('*', value.Length);
        return value[0] + new string('*', Math.Max(1, value.Length - 2)) + value[^1];
    }
}
