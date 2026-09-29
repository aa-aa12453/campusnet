using System.Runtime.InteropServices;
using System.Text;

namespace CampusNet.Core.Ras;

/// <summary>
/// Windows RAS（rasapi32）原生接口封装。拨号走 RasDial，密码不经过命令行。
/// </summary>
public static class NativeRas
{
    public const int MaxEntryName = 256;
    public const int MaxDeviceType = 16;
    public const int MaxDeviceName = 128;

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    public struct RasDialParams
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string szEntryName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] public string szPhoneNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] public string szCallbackNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string szUserName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string szPassword;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string szDomain;
        public uint dwSubEntry;
        public uint dwCallbackId;
        public uint dwIfIndex;
        public uint dwFlags;
        public uint dwSubAddress;
        public uint dwSubAddressLength;
    }

    /// <summary>
    /// 对应 ras.h 的 RASENTRYNAMEW。注意本机系统（Win10/11 的 rasapi32）
    /// 实际使用**不含 guidEntry** 的旧版布局，且要求 Pack = 4：
    ///     dwSize(4) + szEntryName[257]W(514) + dwFlags(4) + szPhonebookPath(524) = 1046
    ///     再按 4 字节补齐 -> 1048。
    /// 实测正确大小 = 1048 字节（含 guidEntry 会变成 1064，导致 632）。
    /// 因此 szPhonebookPath 取 262 个 WCHAR（524 字节）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    public struct RasEntryName
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxEntryName + 1)]
        public string szEntryName;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 262)]
        public string szPhonebookPath;
    }

    /// <summary>
    /// 对应 ras.h 的 RASCONNW。必须显式 Pack = 4：
    /// ras.h 以 #pragma pack(4) 声明，而 C# 默认按 8 字节对齐结构体，
    /// 会在 guidCorrelationId 之后多补 4 字节（1392 vs 系统要求的 1388），
    /// 导致 RasEnumConnections 返回 632 ERROR_INVALID_SIZE、枚举不到任何连接，
    /// 表现为「拨号成功却显示未连接」。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    public struct RasConn
    {
        public uint dwSize;
        public IntPtr hrasconn;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxEntryName + 1)]
        public string szEntryName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxDeviceType + 1)]
        public string szDeviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxDeviceName + 1)]
        public string szDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPhonebook;
        public uint dwSubEntry;
        public Guid guidEntry;
        public uint dwFlags;
        public long luid;
        public Guid guidCorrelationId;
    }

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RasDial(IntPtr lpRasDialExtensions, string? lpszPhonebook,
        ref RasDialParams lpRasDialParams, uint dwNotifierType, IntPtr lpvNotifier, ref IntPtr lphRasConn);

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasHangUp(IntPtr hrasconn);

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RasEnumEntries(string? reserved, string? lpszPhonebook,
        [In, Out] RasEntryName[]? lprasentryname, ref uint lpcb, ref uint lpcEntries);

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RasEnumConnections([In, Out] RasConn[]? lprasconn,
        ref uint lpcb, ref uint lpcConnections);

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RasGetErrorString(uint uErrorValue, StringBuilder lpszErrorString, uint cBufSize);

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RasValidateEntryName(string? lpszPhonebook, string lpszEntryName);

    /// <summary>对指定条目发起拨号。phonebook 为 null 时使用系统默认电话簿。返回 0 表示成功。</summary>
    public static uint Dial(string entry, string user, string pass, string? phonebook = null)
    {
        var p = new RasDialParams
        {
            dwSize = (uint)Marshal.SizeOf<RasDialParams>(),
            szEntryName = entry,
            szPhoneNumber = "",
            szCallbackNumber = "",
            szUserName = user,
            szPassword = pass,
            szDomain = ""
        };

        IntPtr h = IntPtr.Zero;
        uint rc;
        try
        {
            rc = RasDial(IntPtr.Zero, phonebook, ref p, 0u, IntPtr.Zero, ref h);
        }
        finally
        {
            p.szPassword = string.Empty;
            p.szUserName = string.Empty;
        }

        if (rc != 0 && h != IntPtr.Zero)
        {
            RasHangUp(h);
        }
        return rc;
    }

    /// <summary>挂断指定名称的活动连接；未找到返回 false。</summary>
    public static bool HangUp(string entry)
    {
        var conns = EnumConnections();
        bool any = false;
        foreach (var c in conns)
        {
            if (string.Equals(c.szEntryName, entry, StringComparison.OrdinalIgnoreCase))
            {
                RasHangUp(c.hrasconn);
                any = true;
            }
        }
        return any;
    }

    /// <summary>枚举系统电话簿里的所有拨号/VPN 条目名。</summary>
    public static List<string> EnumEntries(string? phonebook = null)
    {
        uint cb = 0, c = 0;
        _ = RasEnumEntries(null, phonebook, null, ref cb, ref c);
        if (cb == 0) return new List<string>();

        WarnIfSizeMismatch("RasEntryName", Marshal.SizeOf<RasEntryName>(), cb, c);

        var arr = new RasEntryName[Math.Max(1, c)];
        for (int i = 0; i < arr.Length; i++)
            arr[i].dwSize = (uint)Marshal.SizeOf<RasEntryName>();

        uint rc = RasEnumEntries(null, phonebook, arr, ref cb, ref c);
        var result = new List<string>();
        if (rc == 0)
        {
            for (int i = 0; i < c; i++)
            {
                var n = arr[i].szEntryName?.Trim();
                if (!string.IsNullOrEmpty(n)) result.Add(n);
            }
        }
        return result;
    }

    /// <summary>枚举当前活动的 RAS 连接。</summary>
    public static List<RasConn> EnumConnections()
    {
        uint cb = 0, c = 0;
        _ = RasEnumConnections(null, ref cb, ref c);
        if (cb == 0) return new List<RasConn>();

        WarnIfSizeMismatch("RasConn", Marshal.SizeOf<RasConn>(), cb, c);

        var arr = new RasConn[Math.Max(1, c)];
        for (int i = 0; i < arr.Length; i++)
            arr[i].dwSize = (uint)Marshal.SizeOf<RasConn>();

        uint rc = RasEnumConnections(arr, ref cb, ref c);
        var result = new List<RasConn>();
        if (rc == 0)
        {
            for (int i = 0; i < c; i++) result.Add(arr[i]);
        }
        return result;
    }

    public static bool IsConnected(string entry)
    {
        foreach (var c in EnumConnections())
        {
            if (string.Equals(c.szEntryName?.Trim(), entry, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static uint ValidateEntry(string entry, string? phonebook = null)
        => RasValidateEntryName(phonebook, entry);

    /// <summary>
    /// 自检：系统在首次探测时返回的 (cb / c) 就是它期望的单条结构体大小。
    /// 若与托管端不一致，说明结构体布局（Pack / 字段尺寸）写错了，
    /// 后续调用必然返回 632。这里显式记录，避免再次出现「静默枚举不到连接」。
    /// </summary>
    private static void WarnIfSizeMismatch(string structName, int managedSize, uint cb, uint entries)
    {
        if (entries == 0) return;
        int expected = (int)(cb / entries);
        if (expected == managedSize) return;
        try
        {
            new Logging.AppLogger(Logging.LogKind.Dial).Error(
                $"{structName} 布局与实际系统不符：C#={managedSize} 字节，系统期望={expected} 字节。" +
                "RasEnum* 将返回 632，连接状态判断会失效。请核对 Pack 与字段尺寸。");
        }
        catch { /* 日志不可用时不影响主流程 */ }
    }

    /// <summary>把 RAS 错误码翻译为中文说明；失败时返回通用文案。</summary>
    public static string GetErrorString(uint code)
    {
        if (code == 0) return "成功";
        var sb = new StringBuilder(512);
        uint rc = RasGetErrorString(code, sb, (uint)sb.Capacity);
        if (rc == 0 && sb.Length > 0) return sb.ToString();
        return $"拨号失败（错误码 {code}）";
    }
}
