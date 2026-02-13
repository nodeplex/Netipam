namespace Netipam.Data;

public sealed class Rack
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? LocationId { get; set; }
    public Location? LocationRef { get; set; }
    public int? RackUnits { get; set; }

    public List<Device> Devices { get; set; } = new();
}
