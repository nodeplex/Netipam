namespace Netipam.Data;

public class Device
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? IpAddress { get; set; }
    public string? AccessLink { get; set; }
    public int? AccessCategoryId { get; set; }
    public AccessCategory? AccessCategory { get; set; }
    public int? LocationId { get; set; }
    public Location? LocationRef { get; set; }
    public int? RackId { get; set; }
    public Rack? RackRef { get; set; }
    public int? RackUPosition { get; set; }
    public int? RackUSize { get; set; }

    public int? SubnetId { get; set; }
    public Subnet? Subnet { get; set; }

    // Network upstream info (from UniFi or manual)
    public string? UpstreamDeviceName { get; set; }
    public string? UpstreamDeviceMac { get; set; }
    public string? UpstreamConnection { get; set; } // port number or "SSID @ band"
    public int? ParentDeviceId { get; set; }
    public Device? ParentDevice { get; set; }
    public int? ManualUpstreamDeviceId { get; set; }
    public Device? ManualUpstreamDevice { get; set; }
    public bool IsTopologyRoot { get; set; }

    public string? ConnectionType { get; set; }
    public string? ConnectionDetail { get; set; }

    public int? ClientTypeId { get; set; }
    public ClientType? ClientType { get; set; }

    // NEW: Host relationship
    public int? HostDeviceId { get; set; }
    public Device? HostDevice { get; set; }
    public bool IsProxmoxHost { get; set; }
    public int? ProxmoxInstanceId { get; set; }
    public ProxmoxInstance? ProxmoxInstance { get; set; }
    public string? ProxmoxNodeIdentifier { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Usage { get; set; }
    public string? Description { get; set; }
    public string? AssetNumber { get; set; }
    public string? Location { get; set; }
    public string? Source { get; set; }
    public string? UsedNew { get; set; }
    public DateOnly? SourceDate { get; set; }
    public bool IsOnline { get; set; }
    public bool IsStatusTracked { get; set; } = true;
    public bool IsCritical { get; set; }
    public DeviceMonitorMode MonitorMode { get; set; }
    public int? MonitorPort { get; set; }
    public bool MonitorUseHttps { get; set; }
    public string? MonitorHttpPath { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastOnlineAt { get; set; }
    public DateTime? LastStatusRollupAtUtc { get; set; }
    public bool IgnoreOffline { get; set; }  // if true, exclude from offline counts/alerts/lists

}





