namespace Netipam.Data;

public sealed class DeviceIpHistory
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public string IpAddress { get; set; } = "";
    public int? Port { get; set; }
    public string? Source { get; set; }

    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenUtc { get; set; }
}
