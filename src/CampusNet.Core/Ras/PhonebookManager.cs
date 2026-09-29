using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CampusNet.Core.Ras;

/// <summary>枚举本机拨号连接、创建标准 PPPoE 条目（等同系统新建宽带连接向导）。</summary>
public static class PhonebookManager
{
    private static string PbkDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Network", "Connections", "Pbk");

    private static string PbkFile => Path.Combine(PbkDir, "rasphone.pbk");

    /// <summary>列出系统电话簿里的所有拨号/VPN 条目。</summary>
    public static List<string> ListEntries()
    {
        var result = new List<string>();
        try
        {
            result.AddRange(NativeRas.EnumEntries());
        }
        catch { }

        // 兜底：直接解析 pbk 文本
        try
        {
            if (File.Exists(PbkFile))
            {
                string text = Encoding.UTF8.GetString(File.ReadAllBytes(PbkFile));
                if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
                foreach (Match m in Regex.Matches(text, @"(?m)^\[(.+?)\]\s*$"))
                {
                    string n = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(n) && n is not ("NETCOMPONENTS" or "MEDIA" or "DEVICE"))
                        result.Add(n);
                }
            }
        }
        catch { }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool EntryExists(string name)
        => ListEntries().Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>创建一个标准 PPPoE 连接。已存在则直接返回成功。</summary>
    public static void CreatePppoeEntry(string name)
    {
        Directory.CreateDirectory(PbkDir);

        byte[] existingBytes = File.Exists(PbkFile) ? File.ReadAllBytes(PbkFile) : Array.Empty<byte>();
        string existing = Encoding.UTF8.GetString(existingBytes);
        bool hasBom = existing.Length > 0 && existing[0] == '\uFEFF';
        if (hasBom) existing = existing[1..];

        if (Regex.IsMatch(existing, $@"(?m)^\[{Regex.Escape(name)}\]\s*$"))
            return;

        string guid = RandomNumberGenerator.GetBytes(16)
            .Select(b => b.ToString("X2")).Aggregate("", (a, b) => a + b);
        string port = GetFreePort(existing);
        int uid = RandomNumberGenerator.GetInt32(10_000_000, 99_999_999);

        string entry = BuildEntry(name, guid, port, uid);

        // 原文件若带 BOM，保留 BOM；条目本身是无 BOM 的 UTF-8 文本，追加在末尾
        using var ms = new MemoryStream();
        if (hasBom)
        {
            ms.WriteByte(0xEF);
            ms.WriteByte(0xBB);
            ms.WriteByte(0xBF);
        }
        if (existingBytes.Length > 0)
        {
            int start = hasBom ? 3 : 0;
            ms.Write(existingBytes, start, existingBytes.Length - start);
            // 保证条目之间换行
            if (existing.Length > 0 && !existing.EndsWith('\n'))
                ms.Write(Encoding.UTF8.GetBytes("\r\n"));
        }
        ms.Write(Encoding.UTF8.GetBytes(entry));
        File.WriteAllBytes(PbkFile, ms.ToArray());
    }

    private static string GetFreePort(string pbkText)
    {
        var used = new HashSet<string>();
        foreach (Match m in Regex.Matches(pbkText, @"(?m)^(?:Preferred)?Port=(PPPoE\d+-\d+)\s*$"))
            used.Add(m.Groups[1].Value);

        for (int i = 0; i < 32; i++)
        {
            string cand = $"PPPoE{i}-0";
            if (!used.Contains(cand)) return cand;
        }
        return "PPPoE0-0";
    }

    private static string BuildEntry(string name, string guid, string port, int uid)
    {
        string[] lines =
        {
            $"[{name}]",
            "Encoding=1",
            "PBVersion=8",
            "Type=5",
            "AutoLogon=0",
            "UseRasCredentials=0",
            $"LowDateTime={uid}",
            "HighDateTime=31279066",
            $"DialParamsUID={uid}",
            $"Guid={guid}",
            "VpnStrategy=0",
            "ExcludedProtocols=0",
            "LcpExtensions=1",
            "DataEncryption=8",
            "SwCompression=0",
            "NegotiateMultilinkAlways=0",
            "SkipDoubleDialDialog=0",
            "DialMode=0",
            "OverridePref=15",
            "RedialAttempts=3",
            "RedialSeconds=60",
            "IdleDisconnectSeconds=0",
            "RedialOnLinkFailure=1",
            "CallbackMode=0",
            "CustomDialDll=",
            "CustomDialFunc=",
            "CustomRasDialDll=",
            "ForceSecureCompartment=0",
            "DisableIKENameEkuCheck=0",
            "AuthenticateServer=0",
            "ShareMsFilePrint=0",
            "BindMsNetClient=0",
            "SharedPhoneNumbers=0",
            "GlobalDeviceSettings=0",
            "PrerequisiteEntry=",
            "PrerequisitePbk=",
            $"PreferredPort={port}",
            "PreferredDevice=WAN Miniport (PPPOE)",
            "PreferredBps=0",
            "PreferredHwFlow=0",
            "PreferredProtocol=0",
            "PreferredCompression=0",
            "PreferredSpeaker=0",
            "PreferredMdmProtocol=0",
            "PreviewUserPw=1",
            "PreviewDomain=0",
            "PreviewPhoneNumber=0",
            "ShowDialingProgress=1",
            "ShowMonitorIconInTaskBar=1",
            "CustomAuthKey=0",
            "AuthRestrictions=552",
            "IpPrioritizeRemote=1",
            "IpInterfaceMetric=0",
            "IpHeaderCompression=0",
            "IpAddress=0.0.0.0",
            "IpDnsAddress=0.0.0.0",
            "IpDns2Address=0.0.0.0",
            "IpWinsAddress=0.0.0.0",
            "IpWins2Address=0.0.0.0",
            "IpAssign=1",
            "IpNameAssign=1",
            "IpDnsFlags=0",
            "IpNBTFlags=0",
            "TcpWindowSize=0",
            "UseFlags=3",
            "IpSecFlags=0",
            "IpDnsSuffix=",
            "Ipv6Assign=1",
            "Ipv6Address=::",
            "Ipv6PrefixLength=0",
            "Ipv6PrioritizeRemote=1",
            "Ipv6InterfaceMetric=0",
            "Ipv6NameAssign=1",
            "Ipv6DnsAddress=::",
            "Ipv6Dns2Address=::",
            "Ipv6Prefix=0000000000000000",
            "Ipv6InterfaceId=0000000000000000",
            "DisableClassBasedDefaultRoute=0",
            "DisableMobility=0",
            "NetworkOutageTime=0",
            "IDI=",
            "IDR=",
            "ImsConfig=0",
            "IdiType=0",
            "IdrType=0",
            "ProvisionType=0",
            "PreSharedKey=",
            "CacheCredentials=1",
            "NumCustomPolicy=0",
            "NumEku=0",
            "UseMachineRootCert=0",
            "Disable_IKEv2_Fragmentation=0",
            "PlumbIKEv2TSAsRoutes=0",
            "NumServers=0",
            "RouteVersion=1",
            "NumRoutes=0",
            "NumNrptRules=0",
            "AutoTiggerCapable=0",
            "NumAppIds=0",
            "NumClassicAppIds=0",
            "ApnInfoProviderId=",
            "ApnInfoUsername=",
            "ApnInfoPassword=",
            "ApnInfoAccessPoint=",
            "ApnInfoAuthentication=1",
            "ApnInfoCompression=0",
            "DeviceComplianceEnabled=0",
            "DeviceComplianceSsoEnabled=0",
            "DeviceComplianceSsoEku=",
            "DeviceComplianceSsoIssuer=",
            "FlagsSet=0",
            "Options=0",
            "DisableDefaultDnsSuffixes=0",
            "NumTrustedNetworks=0",
            "NumDnsSearchSuffixes=0",
            "PowershellCreatedProfile=0",
            "ProxyFlags=0",
            "ProxySettingsModified=0",
            "ProvisioningAuthority=",
            "AuthTypeOTP=0",
            "GREKeyDefined=0",
            "NumPerAppTrafficFilters=0",
            "AlwaysOnCapable=0",
            "DeviceTunnel=0",
            "PrivateNetwork=0",
            "ManagementApp=",
            "",
            "NETCOMPONENTS=",
            "ms_msclient=0",
            "ms_server=0",
            "",
            "MEDIA=rastapi",
            $"Port={port}",
            "Device=WAN Miniport (PPPOE)",
            "",
            "DEVICE=PPPoE",
            "LastSelectedPhone=0",
            "PromoteAlternates=0",
            "TryNextAlternatesOnFail=1",
            ""
        };
        return string.Join("\r\n", lines);
    }
}
