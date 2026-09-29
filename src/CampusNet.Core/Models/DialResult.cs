namespace CampusNet.Core.Models;

public class DialResult
{
    public bool Success { get; init; }
    public uint ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? IpAddress { get; init; }

    public static DialResult Ok(string? ip) => new()
    {
        Success = true, ErrorCode = 0, Message = "拨号成功", IpAddress = ip
    };

    public static DialResult Fail(uint code, string message) => new()
    {
        Success = false, ErrorCode = code, Message = message
    };
}
