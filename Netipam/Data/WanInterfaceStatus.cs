namespace Netipam.Data;

public sealed class WanInterfaceStatus
{
    public int Id { get; set; }
    public string? GatewayName { get; set; }
    public string? GatewayMac { get; set; }
    public string InterfaceName { get; set; } = "";
    public bool? IsUp { get; set; }
    public string? IpAddress { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
