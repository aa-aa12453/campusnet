namespace CampusNet.Core.Models;

/// <summary>拨号凭据。整个对象经 DPAPI 加密后落盘，不明文保存。</summary>
public class DialCredential
{
    public string ConnectionName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
