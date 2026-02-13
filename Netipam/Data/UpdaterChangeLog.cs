namespace Netipam.Data;

public sealed class UpdaterChangeLog
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public UpdaterRunLog? Run { get; set; }
    public int? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public string FieldName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
