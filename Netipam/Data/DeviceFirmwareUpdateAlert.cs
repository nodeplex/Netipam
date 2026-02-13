using System.ComponentModel.DataAnnotations;

namespace Netipam.Data;

public sealed class DeviceFirmwareUpdateAlert
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    [MaxLength(200)]
    public string? NameAtTime { get; set; }

    [MaxLength(64)]
    public string? MacAtTime { get; set; }

    [MaxLength(200)]
    public string? ModelAtTime { get; set; }

    [MaxLength(64)]
    public string? CurrentVersion { get; set; }

    [MaxLength(64)]
    public string? TargetVersion { get; set; }

    public DateTime DetectedAtUtc { get; set; }

    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    [MaxLength(64)]
    public string? Source { get; set; }
}
