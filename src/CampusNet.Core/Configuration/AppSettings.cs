namespace CampusNet.Core.Models;

/// <summary>应用与守护设置（明文 JSON，不含账号密码）。</summary>
public class AppSettings
{
    /// <summary>断线检测间隔（秒）。</summary>
    public int CheckIntervalSeconds { get; set; } = 15;

    /// <summary>连续多少次检测失败才触发重拨。</summary>
    public int FailThreshold { get; set; } = 2;

    /// <summary>重拨成功后的冷却时间（秒），期间不重复拨号。</summary>
    public int CooldownSeconds { get; set; } = 60;

    /// <summary>是否开启开机自启（守护进程）。</summary>
    public bool AutoStart { get; set; }

    /// <summary>单次拨号超时（秒）。</summary>
    public int DialTimeoutSeconds { get; set; } = 75;

    /// <summary>拨号失败最大重试轮数。</summary>
    public int MaxAttempts { get; set; } = 6;
}
