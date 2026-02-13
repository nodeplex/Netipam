namespace Netipam.Data;

public sealed class DeviceStatusEvent
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    public bool IsOnline { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? Source { get; set; }
}
