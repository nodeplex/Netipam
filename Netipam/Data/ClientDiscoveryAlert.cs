using System.ComponentModel.DataAnnotations;

namespace Netipam.Data;

public sealed class ClientDiscoveryAlert
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Mac { get; set; } = "";

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(255)]
    public string? Hostname { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(64)]
    public string? ConnectionType { get; set; }

    [MaxLength(255)]
    public string? UpstreamDeviceName { get; set; }

    [MaxLength(64)]
    public string? UpstreamDeviceMac { get; set; }

    [MaxLength(255)]
    public string? UpstreamConnection { get; set; }

    [MaxLength(255)]
    public string? ConnectionDetail { get; set; }

    public bool IsOnline { get; set; }
    public DateTime DetectedAtUtc { get; set; }

    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }

    [MaxLength(64)]
    public string? Source { get; set; }
}
