using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Netipam.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Subnet> Subnets => Set<Subnet>();
    public DbSet<ClientType> ClientTypes => Set<ClientType>();
    public DbSet<AccessCategory> AccessCategories => Set<AccessCategory>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Rack> Racks => Set<Rack>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<IpAssignment> IpAssignments => Set<IpAssignment>();
    public DbSet<UserColumnPreference> UserColumnPreferences => Set<UserColumnPreference>();
    public DbSet<UserAccessCategoryOrder> UserAccessCategoryOrders => Set<UserAccessCategoryOrder>();
    public DbSet<UserAccessItemOrder> UserAccessItemOrders => Set<UserAccessItemOrder>();
    public DbSet<DeviceStatusEvent> DeviceStatusEvents => Set<DeviceStatusEvent>();
    public DbSet<DeviceStatusDaily> DeviceStatusDaily => Set<DeviceStatusDaily>();
    public DbSet<DeviceIpHistory> DeviceIpHistories => Set<DeviceIpHistory>();
    public DbSet<UpdaterRunLog> UpdaterRunLogs => Set<UpdaterRunLog>();
    public DbSet<UpdaterChangeLog> UpdaterChangeLogs => Set<UpdaterChangeLog>();
    public DbSet<WanInterfaceStatus> WanInterfaceStatuses => Set<WanInterfaceStatus>();

    // Offline alerts
    public DbSet<ClientOfflineAlert> ClientOfflineAlerts => Set<ClientOfflineAlert>();
    public DbSet<ClientDiscoveryAlert> ClientDiscoveryAlerts => Set<ClientDiscoveryAlert>();
    public DbSet<DeviceFirmwareUpdateAlert> DeviceFirmwareUpdateAlerts => Set<DeviceFirmwareUpdateAlert>();
    public DbSet<IgnoredDiscoveryMac> IgnoredDiscoveryMacs => Set<IgnoredDiscoveryMac>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // IMPORTANT: Identity tables/config
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClientType>(entity =>
        {
            entity.Property(ct => ct.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(ct => ct.Name).IsUnique();
            entity.Property(ct => ct.Description).HasMaxLength(500);
            entity.Property(ct => ct.Icon).HasMaxLength(128);

            entity.Property(ct => ct.IsHost).HasDefaultValue(false);

            // You had IsHost twice; leaving one. If you meant IsDevice, set that here instead.
            // entity.Property(ct => ct.IsDevice).HasDefaultValue(false);
        });

        // --------------------
        // App Settings (singleton row)
        // --------------------
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(x => x.Id);

            // ---- UniFi updater ----
            entity.Property(x => x.UnifiUpdaterEnabled).HasDefaultValue(true);

            // Safety: even if UI enforces it, DB default should be sane.
            entity.Property(x => x.UnifiUpdaterIntervalSeconds).HasDefaultValue(60);

            entity.Property(x => x.UnifiUpdateConnectionFieldsWhenOnline).HasDefaultValue(true);
            entity.Property(x => x.UnifiSyncIpAddress).HasDefaultValue(true);
            entity.Property(x => x.UnifiSyncOnlineStatus).HasDefaultValue(true);
            entity.Property(x => x.UnifiSyncName).HasDefaultValue(false);
            entity.Property(x => x.UnifiSyncHostname).HasDefaultValue(false);
            entity.Property(x => x.UnifiSyncManufacturer).HasDefaultValue(false);
            entity.Property(x => x.UnifiSyncModel).HasDefaultValue(false);

            // ---- UniFi connection (DB-backed) ----
            entity.Property(x => x.UnifiBaseUrl).HasMaxLength(255);
            entity.Property(x => x.UnifiSiteName).HasMaxLength(64);
            entity.Property(x => x.UnifiUsername).HasMaxLength(128);
            entity.Property(x => x.UnifiAuthMode).HasMaxLength(16).HasDefaultValue("Session");

            // Protected/encrypted at rest (DataProtection)
            entity.Property(x => x.UnifiPasswordProtected).HasMaxLength(2048);
            entity.Property(x => x.UnifiApiKeyProtected).HasMaxLength(2048);

            // ---- UI / general ----
            entity.Property(x => x.ShowLastSeenTooltips).HasDefaultValue(true);
            entity.Property(x => x.UiAutoRefreshSeconds).HasDefaultValue(0);
            entity.Property(x => x.DarkMode).HasDefaultValue(true);
            entity.Property(x => x.ThemeName)
                  .HasMaxLength(64)
                  .HasDefaultValue("Default");

            entity.Property(x => x.SiteTitle)
                  .HasMaxLength(64)
                  .HasDefaultValue("Netipam");

            entity.Property(x => x.DateFormat)
                  .HasMaxLength(64)
                  .HasDefaultValue("MM-dd-yyyy HH:mm");

            entity.Property(x => x.UiShowWanStatus).HasDefaultValue(false);
        });

        // ---- Subnets ----
        modelBuilder.Entity<Subnet>(entity =>
        {
            entity.Property(s => s.Name).HasMaxLength(200);
            entity.Property(s => s.Cidr).HasMaxLength(64);
            entity.Property(s => s.Description).HasMaxLength(2000);
            entity.Property(s => s.DhcpRangeStart).HasMaxLength(64);
            entity.Property(s => s.DhcpRangeEnd).HasMaxLength(64);
            entity.Property(s => s.VlanId);
            entity.Property(s => s.Dns1).HasMaxLength(64);
            entity.Property(s => s.Dns2).HasMaxLength(64);

            // Recommended: prevent duplicate CIDRs
            entity.HasIndex(s => s.Cidr).IsUnique();
        });

        modelBuilder.Entity<AccessCategory>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.Property(l => l.Name).HasMaxLength(100).IsRequired();
            entity.Property(l => l.Description).HasMaxLength(500);
            entity.HasIndex(l => l.Name).IsUnique();
        });

        modelBuilder.Entity<Rack>(entity =>
        {
            entity.Property(r => r.Name).HasMaxLength(100).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(500);
            entity.Property(r => r.LocationId);
            entity.Property(r => r.RackUnits);
            entity.HasIndex(r => r.Name).IsUnique();

            entity.HasOne(r => r.LocationRef)
                  .WithMany()
                  .HasForeignKey(r => r.LocationId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(r => r.LocationId);
        });

        modelBuilder.Entity<UserColumnPreference>(entity =>
        {
            entity.Property(p => p.UserId).HasMaxLength(450).IsRequired();
            entity.Property(p => p.PageKey).HasMaxLength(64).IsRequired();
            entity.Property(p => p.ColumnKey).HasMaxLength(64).IsRequired();
            entity.Property(p => p.IsVisible).HasDefaultValue(true);
            entity.Property(p => p.UpdatedAtUtc);

            entity.HasIndex(p => new { p.UserId, p.PageKey });
            entity.HasIndex(p => new { p.UserId, p.PageKey, p.ColumnKey }).IsUnique();
        });

        modelBuilder.Entity<UserAccessCategoryOrder>(entity =>
        {
            entity.Property(p => p.UserId).HasMaxLength(450).IsRequired();
            entity.Property(p => p.SortOrder);

            entity.HasIndex(p => p.UserId);
            entity.HasIndex(p => new { p.UserId, p.AccessCategoryId }).IsUnique();

            entity.HasOne<AccessCategory>()
                  .WithMany()
                  .HasForeignKey(p => p.AccessCategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserAccessItemOrder>(entity =>
        {
            entity.Property(p => p.UserId).HasMaxLength(450).IsRequired();
            entity.Property(p => p.SortOrder);

            entity.HasIndex(p => p.UserId);
            entity.HasIndex(p => new { p.UserId, p.DeviceId }).IsUnique();

            entity.HasOne<Device>()
                  .WithMany()
                  .HasForeignKey(p => p.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceStatusEvent>(entity =>
        {
            entity.Property(e => e.Source).HasMaxLength(64);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.ChangedAtUtc);
        });

        modelBuilder.Entity<DeviceStatusDaily>(entity =>
        {
            entity.HasIndex(e => new { e.DeviceId, e.Date }).IsUnique();
            entity.HasIndex(e => e.Date);
        });

        modelBuilder.Entity<UpdaterRunLog>(entity =>
        {
            entity.Property(r => r.Error).HasMaxLength(1024);
            entity.Property(r => r.Source).HasMaxLength(64);
            entity.HasIndex(r => r.StartedAtUtc);
        });

        modelBuilder.Entity<UpdaterChangeLog>(entity =>
        {
            entity.Property(c => c.DeviceName).HasMaxLength(200);
            entity.Property(c => c.IpAddress).HasMaxLength(64);
            entity.Property(c => c.FieldName).HasMaxLength(64).IsRequired();
            entity.Property(c => c.OldValue).HasMaxLength(512);
            entity.Property(c => c.NewValue).HasMaxLength(512);
            entity.HasIndex(c => c.RunId);
            entity.HasIndex(c => c.DeviceId);
        });

        // ---- Devices ----
        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(d => d.Name).HasMaxLength(200).IsRequired();
            entity.Property(d => d.Hostname).HasMaxLength(255);
            entity.Property(d => d.MacAddress).HasMaxLength(32);
            entity.Property(d => d.IpAddress).HasMaxLength(64);
            entity.Property(d => d.AccessLink).HasMaxLength(2048);
            entity.Property(d => d.UpstreamDeviceName).HasMaxLength(255);
            entity.Property(d => d.UpstreamDeviceMac).HasMaxLength(64);
            entity.Property(d => d.UpstreamConnection).HasMaxLength(255);
            entity.Property(d => d.Manufacturer).HasMaxLength(200);
            entity.Property(d => d.Model).HasMaxLength(200);
            entity.Property(d => d.OperatingSystem).HasMaxLength(200);
            entity.Property(d => d.Usage).HasMaxLength(200);
            entity.Property(d => d.Description).HasMaxLength(2000);
            entity.Property(d => d.IsOnline).HasDefaultValue(false);
            entity.Property(d => d.IsStatusTracked).HasDefaultValue(true);
            entity.Property(d => d.IsCritical).HasDefaultValue(false);
            entity.Property(d => d.MonitorMode).HasDefaultValue(DeviceMonitorMode.Normal);
            entity.Property(d => d.MonitorUseHttps).HasDefaultValue(false);
            entity.Property(d => d.MonitorHttpPath).HasMaxLength(512);
            entity.Property(d => d.IgnoreOffline).HasDefaultValue(false);
            entity.Property(d => d.ConnectionType).HasMaxLength(32);
            entity.Property(d => d.ConnectionDetail).HasMaxLength(255);
            entity.Property(d => d.LocationId);
            entity.Property(d => d.RackId);
            entity.Property(d => d.RackUPosition);
            entity.Property(d => d.RackUSize);
            entity.Property(d => d.AccessCategoryId);
            entity.Property(d => d.LastStatusRollupAtUtc);
            entity.Property(d => d.ManualUpstreamDeviceId);
            entity.Property(d => d.IsTopologyRoot).HasDefaultValue(false);

            // Unique MAC
            entity.HasIndex(d => d.MacAddress).IsUnique();

            // Subnet FK
            entity.HasOne(d => d.Subnet)
                  .WithMany(s => s.Devices)
                  .HasForeignKey(d => d.SubnetId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(d => d.SubnetId);

            // ClientType FK
            entity.HasOne(d => d.ClientType)
                  .WithMany(ct => ct.Devices)
                  .HasForeignKey(d => d.ClientTypeId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(d => d.ClientTypeId);

            entity.HasOne(d => d.AccessCategory)
                  .WithMany(c => c.Devices)
                  .HasForeignKey(d => d.AccessCategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(d => d.AccessCategoryId);

            entity.HasOne(d => d.LocationRef)
                  .WithMany(l => l.Devices)
                  .HasForeignKey(d => d.LocationId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(d => d.LocationId);

            entity.HasOne(d => d.RackRef)
                  .WithMany(r => r.Devices)
                  .HasForeignKey(d => d.RackId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(d => d.RackId);

            entity.HasOne(d => d.HostDevice)
                  .WithMany()
                  .HasForeignKey(d => d.HostDeviceId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(d => d.HostDeviceId);

            entity.HasOne(d => d.ParentDevice)
                  .WithMany()
                  .HasForeignKey(d => d.ParentDeviceId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(d => d.ParentDeviceId);

            entity.HasOne(d => d.ManualUpstreamDevice)
                  .WithMany()
                  .HasForeignKey(d => d.ManualUpstreamDeviceId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(d => d.ManualUpstreamDeviceId);
        });

        // ---- IP Assignments (reservations) ----
        modelBuilder.Entity<IpAssignment>(entity =>
        {
            entity.Property(i => i.IpAddress).HasMaxLength(64).IsRequired();
            entity.Property(i => i.Notes).HasMaxLength(500);
            entity.Property(i => i.Status).HasDefaultValue(IpReservationStatus.Reserved);

            entity.HasIndex(i => new { i.SubnetId, i.IpAddress }).IsUnique();

            entity.HasOne(i => i.Subnet)
                  .WithMany()
                  .HasForeignKey(i => i.SubnetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Device)
                  .WithMany()
                  .HasForeignKey(i => i.DeviceId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeviceIpHistory>(entity =>
        {
            entity.Property(h => h.IpAddress).HasMaxLength(64).IsRequired();
            entity.Property(h => h.Source).HasMaxLength(64);

            entity.HasIndex(h => h.DeviceId);
            entity.HasIndex(h => new { h.DeviceId, h.IpAddress });

            entity.HasOne(h => h.Device)
                  .WithMany()
                  .HasForeignKey(h => h.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WanInterfaceStatus>(entity =>
        {
            entity.Property(w => w.GatewayName).HasMaxLength(200);
            entity.Property(w => w.GatewayMac).HasMaxLength(64);
            entity.Property(w => w.InterfaceName).HasMaxLength(64).IsRequired();
            entity.Property(w => w.IpAddress).HasMaxLength(64);
            entity.Property(w => w.UpdatedAtUtc).IsRequired();
            entity.HasIndex(w => w.GatewayMac);
            entity.HasIndex(w => w.InterfaceName);
        });

        // ---- Offline Alerts ----
        modelBuilder.Entity<ClientOfflineAlert>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.NameAtTime).HasMaxLength(200);
            entity.Property(a => a.IpAtTime).HasMaxLength(64);
            entity.Property(a => a.Source).HasMaxLength(64);

            entity.Property(a => a.IsAcknowledged).HasDefaultValue(false);

            entity.HasOne(a => a.Device)
                  .WithMany()
                  .HasForeignKey(a => a.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => a.DeviceId);
            entity.HasIndex(a => a.IsAcknowledged);
            entity.HasIndex(a => a.WentOfflineAtUtc);
        });

        modelBuilder.Entity<ClientDiscoveryAlert>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Mac).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Name).HasMaxLength(200);
            entity.Property(a => a.Hostname).HasMaxLength(255);
            entity.Property(a => a.IpAddress).HasMaxLength(64);
            entity.Property(a => a.ConnectionType).HasMaxLength(64);
            entity.Property(a => a.UpstreamDeviceName).HasMaxLength(255);
            entity.Property(a => a.UpstreamDeviceMac).HasMaxLength(64);
            entity.Property(a => a.UpstreamConnection).HasMaxLength(255);
            entity.Property(a => a.ConnectionDetail).HasMaxLength(255);
            entity.Property(a => a.Source).HasMaxLength(64);

            entity.Property(a => a.IsAcknowledged).HasDefaultValue(false);

            entity.HasIndex(a => a.Mac);
            entity.HasIndex(a => a.IsAcknowledged);
            entity.HasIndex(a => a.DetectedAtUtc);
        });

        modelBuilder.Entity<DeviceFirmwareUpdateAlert>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.NameAtTime).HasMaxLength(200);
            entity.Property(a => a.MacAtTime).HasMaxLength(64);
            entity.Property(a => a.ModelAtTime).HasMaxLength(200);
            entity.Property(a => a.CurrentVersion).HasMaxLength(64);
            entity.Property(a => a.TargetVersion).HasMaxLength(64);
            entity.Property(a => a.Source).HasMaxLength(64);

            entity.Property(a => a.IsAcknowledged).HasDefaultValue(false);

            entity.HasOne(a => a.Device)
                  .WithMany()
                  .HasForeignKey(a => a.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => a.DeviceId);
            entity.HasIndex(a => a.IsAcknowledged);
            entity.HasIndex(a => a.ResolvedAtUtc);
            entity.HasIndex(a => a.DetectedAtUtc);
        });

        modelBuilder.Entity<IgnoredDiscoveryMac>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Mac).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Source).HasMaxLength(64);

            entity.HasIndex(a => a.Mac).IsUnique();
        });
    }
}
