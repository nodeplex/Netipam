namespace Netipam.Data;

public class Location
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public List<Device> Devices { get; set; } = new();
}
