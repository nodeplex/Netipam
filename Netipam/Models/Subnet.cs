namespace Netipam.Data;

public class Subnet
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    // Store CIDR as a string for now, e.g. "192.168.1.0/24"
    public string Cidr { get; set; } = "";

    public string? Description { get; set; }
    public string? DhcpRangeStart { get; set; }
    public string? DhcpRangeEnd { get; set; }
    public int? VlanId { get; set; }
    public string? Dns1 { get; set; }
    public string? Dns2 { get; set; }

    public List<Device> Devices { get; set; } = new();

}

