namespace Netipam.Data;

public class IpAssignment
{
    public int Id { get; set; }
    public string IpAddress { get; set; } = ""; // store as string to start
    public int SubnetId { get; set; }
    public Subnet? Subnet { get; set; }

    public int? DeviceId { get; set; }
    public Device? Device { get; set; }

    public IpReservationStatus Status { get; set; } = IpReservationStatus.Reserved;
    public string? Notes { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
