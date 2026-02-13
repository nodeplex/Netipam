using System.ComponentModel.DataAnnotations;

namespace Netipam.Data;

public sealed class ClientOfflineAlert
{
    public int Id { get; set; }

    // FK to Device (your "clients" are in Devices table too)
    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    // Snapshot fields (helpful if device fields later change)
    [MaxLength(200)]
    public string? NameAtTime { get; set; }

    [MaxLength(64)]
    public string? IpAtTime { get; set; }

    // Offline lifecycle
    public DateTime WentOfflineAtUtc { get; set; }
    public DateTime? CameOnlineAtUtc { get; set; }

    // Acknowledge lifecycle
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }

    // Optional: free-form reason/source (e.g., "UniFi")
    [MaxLength(64)]
    public string? Source { get; set; }
}
