using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;

namespace CampusNet.Core.Network;

/// <summary>网络连通性检测与拨号接口信息读取。</summary>
public static class NetworkMonitor
{
    // 国内可靠公共 DNS
    private static readonly string[] PingTargets = { "223.5.5.5", "119.29.29.29", "114.114.114.114" };

    // TCP 兜底目标（防 ICMP 被禁）：DNS 53 / HTTPS 443
    private static readonly (string Host, int Port)[] TcpTargets =
    {
        ("223.5.5.5", 443),
        ("119.29.29.29", 443),
    };

    public static async Task<bool> HasInternetAsync(int timeoutMs = 2500)
    {
        var pingTasks = PingTargets.Select(t => PingOnceAsync(t, timeoutMs));
        bool[] pingResults = await Task.WhenAll(pingTasks);
        if (pingResults.Any(r => r)) return true;

        var tcpTasks = TcpTargets.Select(t => TcpOnceAsync(t.Host, t.Port, timeoutMs));
        bool[] tcpResults = await Task.WhenAll(tcpTasks);
        return tcpResults.Any(r => r);
    }

    private static async Task<bool> PingOnceAsync(string host, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TcpOnceAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var tcs = new TaskCompletionSource<bool>();
            using (client.ConnectAsync(host, port).ContinueWith(t =>
                tcs.TrySetResult(t.IsCompletedSuccessfully), TaskScheduler.Default))
            {
                var delay = Task.Delay(timeoutMs);
                Task done = await Task.WhenAny(tcs.Task, delay);
                return done == tcs.Task && tcs.Task.Result;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>读取拨号连接（PPP 接口）获取到的 IPv4 地址；未找到返回 null。</summary>
    public static string? GetRasIpAddress(string entry)
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => string.Equals(n.Name, entry, StringComparison.OrdinalIgnoreCase)
                                     && n.OperationalStatus == OperationalStatus.Up);
            if (nic == null) return null;

            var props = nic.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(addr.Address))
                {
                    string ip = addr.Address.ToString();
                    // 排除 APIPA 自分配地址
                    if (!ip.StartsWith("169.254.")) return ip;
                }
            }
        }
        catch { }
        return null;
    }
}
