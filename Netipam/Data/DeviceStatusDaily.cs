namespace Netipam.Data;

public sealed class DeviceStatusDaily
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateOnly Date { get; set; }
    public int OnlineSeconds { get; set; }
    public int ObservedSeconds { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
