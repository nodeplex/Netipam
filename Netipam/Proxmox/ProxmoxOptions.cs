namespace Netipam.Proxmox;

public sealed class ProxmoxOptions
{
    public bool Enabled { get; set; } = false;
    public string? BaseUrl { get; set; }
    public string? ApiTokenId { get; set; }
    public string? ApiTokenSecret { get; set; }
    public string? HostDeviceMac { get; set; }
    public int IntervalSeconds { get; set; } = 300;
    public bool UpdateExistingHostAssignments { get; set; } = true;
}
