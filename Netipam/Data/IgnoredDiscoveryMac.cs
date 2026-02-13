using System.ComponentModel.DataAnnotations;

namespace Netipam.Data;

public sealed class IgnoredDiscoveryMac
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Mac { get; set; } = "";

    public DateTime IgnoredAtUtc { get; set; }

    [MaxLength(64)]
    public string? Source { get; set; }
}
