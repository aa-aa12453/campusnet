using System.IO;
using System.Text.Json;
using CampusNet.Core.Configuration;

namespace CampusNet.Core.Models;

/// <summary>守护进程实时状态，周期性写文件供 GUI 读取。</summary>
public class WatchdogState
{
    public int Pid { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastCheckTime { get; set; }
    public bool NetworkHealthy { get; set; }
    public int ConsecutiveFails { get; set; }
    public DateTime? LastDialTime { get; set; }
    public uint LastDialCode { get; set; }

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    public void Save()
    {
        try { File.WriteAllText(AppPaths.WatchdogStateFile, JsonSerializer.Serialize(this, Opts)); }
        catch { }
    }

    public static WatchdogState? Load()
    {
        try
        {
            if (File.Exists(AppPaths.WatchdogStateFile))
                return JsonSerializer.Deserialize<WatchdogState>(
                    File.ReadAllText(AppPaths.WatchdogStateFile), Opts);
        }
        catch { }
        return null;
    }
}
