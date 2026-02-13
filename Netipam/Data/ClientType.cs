namespace Netipam.Data;

public class ClientType
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public string? Icon { get; set; }

    // Option A: this decides whether it should appear on the future Devices page
    public bool IsDevice { get; set; }

    public List<Device> Devices { get; set; } = new();

    // NEW: if true, devices of this type can be selected as "hosts"
    public bool IsHost { get; set; }
}
