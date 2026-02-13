using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Netipam.Data;

namespace Netipam.Services;

public sealed class BackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new DateOnlyJsonConverter() }
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public BackupService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var payload = new BackupEnvelope
        {
            FormatVersion = 1,
            CreatedUtc = DateTime.UtcNow,
            AppVersion = typeof(BackupService).Assembly.GetName().Version?.ToString(),
            Data = new BackupData
            {
                AppSetting = await db.AppSettings.AsNoTracking().SingleOrDefaultAsync(ct),
                ClientTypes = await db.ClientTypes.AsNoTracking().OrderBy(c => c.Id).Select(c => new ClientTypeDto(c)).ToListAsync(ct),
                AccessCategories = await db.AccessCategories.AsNoTracking().OrderBy(c => c.Id).Select(c => new AccessCategoryDto(c)).ToListAsync(ct),
                Locations = await db.Locations.AsNoTracking().OrderBy(l => l.Id).Select(l => new LocationDto(l)).ToListAsync(ct),
                Racks = await db.Racks.AsNoTracking().OrderBy(r => r.Id).Select(r => new RackDto(r)).ToListAsync(ct),
                Subnets = await db.Subnets.AsNoTracking().OrderBy(s => s.Id).Select(s => new SubnetDto(s)).ToListAsync(ct),
                IpAssignments = await db.IpAssignments.AsNoTracking().OrderBy(i => i.Id).Select(i => new IpAssignmentDto(i)).ToListAsync(ct),
                Devices = await db.Devices.AsNoTracking().OrderBy(d => d.Id).Select(d => new DeviceDto(d)).ToListAsync(ct),
                DeviceStatusEvents = await db.DeviceStatusEvents.AsNoTracking().OrderBy(e => e.Id).Select(e => new DeviceStatusEventDto(e)).ToListAsync(ct),
                DeviceStatusDaily = await db.DeviceStatusDaily.AsNoTracking().OrderBy(d => d.Id).Select(d => new DeviceStatusDailyDto(d)).ToListAsync(ct),
                DeviceIpHistories = await db.DeviceIpHistories.AsNoTracking().OrderBy(h => h.Id).Select(h => new DeviceIpHistoryDto(h)).ToListAsync(ct),
                ClientOfflineAlerts = await db.ClientOfflineAlerts.AsNoTracking().OrderBy(a => a.Id).Select(a => new ClientOfflineAlertDto(a)).ToListAsync(ct),
                ClientDiscoveryAlerts = await db.ClientDiscoveryAlerts.AsNoTracking().OrderBy(a => a.Id).Select(a => new ClientDiscoveryAlertDto(a)).ToListAsync(ct),
                IgnoredDiscoveryMacs = await db.IgnoredDiscoveryMacs.AsNoTracking().OrderBy(i => i.Id).Select(i => new IgnoredDiscoveryMacDto(i)).ToListAsync(ct),
                UpdaterRunLogs = await db.UpdaterRunLogs.AsNoTracking().OrderBy(r => r.Id).Select(r => new UpdaterRunLogDto(r)).ToListAsync(ct),
                UpdaterChangeLogs = await db.UpdaterChangeLogs.AsNoTracking().OrderBy(c => c.Id).Select(c => new UpdaterChangeLogDto(c)).ToListAsync(ct),
                Users = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new AppUserDto(u)).ToListAsync(ct),
                Roles = await db.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new RoleDto(r)).ToListAsync(ct),
                UserRoles = await db.UserRoles.AsNoTracking().OrderBy(ur => ur.UserId).ThenBy(ur => ur.RoleId).Select(ur => new UserRoleDto(ur)).ToListAsync(ct),
                UserClaims = await db.UserClaims.AsNoTracking().OrderBy(uc => uc.Id).Select(uc => new UserClaimDto(uc)).ToListAsync(ct),
                RoleClaims = await db.RoleClaims.AsNoTracking().OrderBy(rc => rc.Id).Select(rc => new RoleClaimDto(rc)).ToListAsync(ct),
                UserLogins = await db.UserLogins.AsNoTracking().OrderBy(ul => ul.UserId).ThenBy(ul => ul.LoginProvider).Select(ul => new UserLoginDto(ul)).ToListAsync(ct),
                UserTokens = await db.UserTokens.AsNoTracking().OrderBy(ut => ut.UserId).ThenBy(ut => ut.LoginProvider).ThenBy(ut => ut.Name).Select(ut => new UserTokenDto(ut)).ToListAsync(ct)
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
    }

    public async Task<int> ImportAsync(Stream jsonStream, CancellationToken ct = default)
    {
        var payload = await JsonSerializer.DeserializeAsync<BackupEnvelope>(jsonStream, JsonOptions, ct);
        if (payload is null || payload.Data is null)
            throw new InvalidOperationException("Invalid backup file.");

        if (payload.FormatVersion != 1)
            throw new InvalidOperationException($"Unsupported backup format version: {payload.FormatVersion}.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var total = 0;

        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"UpdaterChangeLogs\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"UpdaterRunLogs\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DeviceStatusEvents\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DeviceStatusDaily\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DeviceIpHistories\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"ClientOfflineAlerts\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"ClientDiscoveryAlerts\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IgnoredDiscoveryMacs\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IpAssignments\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Devices\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Subnets\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Racks\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Locations\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AccessCategories\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"ClientTypes\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AppSettings\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserTokens\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserLogins\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserClaims\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserRoles\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetRoleClaims\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUsers\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetRoles\";", ct);

        if (payload.Data.AppSetting is not null)
            db.AppSettings.Add(payload.Data.AppSetting);

        db.ClientTypes.AddRange(payload.Data.ClientTypes.Select(c => c.ToEntity()));
        db.AccessCategories.AddRange(payload.Data.AccessCategories.Select(c => c.ToEntity()));
        db.Locations.AddRange(payload.Data.Locations.Select(l => l.ToEntity()));
        db.Racks.AddRange(payload.Data.Racks.Select(r => r.ToEntity()));
        db.Subnets.AddRange(payload.Data.Subnets.Select(s => s.ToEntity()));
        db.IpAssignments.AddRange(payload.Data.IpAssignments.Select(i => i.ToEntity()));
        db.Devices.AddRange(payload.Data.Devices.Select(d => d.ToEntity()));
        db.DeviceStatusEvents.AddRange(payload.Data.DeviceStatusEvents.Select(e => e.ToEntity()));
        db.DeviceStatusDaily.AddRange(payload.Data.DeviceStatusDaily.Select(d => d.ToEntity()));
        db.DeviceIpHistories.AddRange(payload.Data.DeviceIpHistories.Select(h => h.ToEntity()));
        db.ClientOfflineAlerts.AddRange(payload.Data.ClientOfflineAlerts.Select(a => a.ToEntity()));
        db.ClientDiscoveryAlerts.AddRange(payload.Data.ClientDiscoveryAlerts.Select(a => a.ToEntity()));
        db.IgnoredDiscoveryMacs.AddRange(payload.Data.IgnoredDiscoveryMacs.Select(i => i.ToEntity()));
        db.UpdaterRunLogs.AddRange(payload.Data.UpdaterRunLogs.Select(r => r.ToEntity()));
        db.UpdaterChangeLogs.AddRange(payload.Data.UpdaterChangeLogs.Select(c => c.ToEntity()));
        db.Roles.AddRange(payload.Data.Roles.Select(r => r.ToEntity()));
        db.Users.AddRange(payload.Data.Users.Select(u => u.ToEntity()));
        db.RoleClaims.AddRange(payload.Data.RoleClaims.Select(rc => rc.ToEntity()));
        db.UserClaims.AddRange(payload.Data.UserClaims.Select(uc => uc.ToEntity()));
        db.UserLogins.AddRange(payload.Data.UserLogins.Select(ul => ul.ToEntity()));
        db.UserTokens.AddRange(payload.Data.UserTokens.Select(ut => ut.ToEntity()));
        db.UserRoles.AddRange(payload.Data.UserRoles.Select(ur => ur.ToEntity()));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return total;
    }

    public sealed class BackupEnvelope
    {
        public int FormatVersion { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string? AppVersion { get; set; }
        public BackupData Data { get; set; } = new();
    }

    public sealed class BackupData
    {
        public AppSetting? AppSetting { get; set; }
        public List<ClientTypeDto> ClientTypes { get; set; } = new();
        public List<AccessCategoryDto> AccessCategories { get; set; } = new();
        public List<LocationDto> Locations { get; set; } = new();
        public List<RackDto> Racks { get; set; } = new();
        public List<SubnetDto> Subnets { get; set; } = new();
        public List<IpAssignmentDto> IpAssignments { get; set; } = new();
        public List<DeviceDto> Devices { get; set; } = new();
        public List<DeviceStatusEventDto> DeviceStatusEvents { get; set; } = new();
        public List<DeviceStatusDailyDto> DeviceStatusDaily { get; set; } = new();
        public List<DeviceIpHistoryDto> DeviceIpHistories { get; set; } = new();
        public List<ClientOfflineAlertDto> ClientOfflineAlerts { get; set; } = new();
        public List<ClientDiscoveryAlertDto> ClientDiscoveryAlerts { get; set; } = new();
        public List<IgnoredDiscoveryMacDto> IgnoredDiscoveryMacs { get; set; } = new();
        public List<UpdaterRunLogDto> UpdaterRunLogs { get; set; } = new();
        public List<UpdaterChangeLogDto> UpdaterChangeLogs { get; set; } = new();
        public List<AppUserDto> Users { get; set; } = new();
        public List<RoleDto> Roles { get; set; } = new();
        public List<UserRoleDto> UserRoles { get; set; } = new();
        public List<UserClaimDto> UserClaims { get; set; } = new();
        public List<RoleClaimDto> RoleClaims { get; set; } = new();
        public List<UserLoginDto> UserLogins { get; set; } = new();
        public List<UserTokenDto> UserTokens { get; set; } = new();
    }

    [method: JsonConstructor]
    public sealed record ClientTypeDto(
        int Id,
        string Name,
        string? Description,
        string? Icon,
        bool IsDevice,
        bool IsHost)
    {
        public ClientTypeDto(ClientType e) : this(e.Id, e.Name, e.Description, e.Icon, e.IsDevice, e.IsHost) { }
        public ClientType ToEntity() => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Icon = Icon,
            IsDevice = IsDevice,
            IsHost = IsHost
        };
    }

    [method: JsonConstructor]
    public sealed record AccessCategoryDto(
        int Id,
        string Name,
        string? Description)
    {
        public AccessCategoryDto(AccessCategory e) : this(e.Id, e.Name, e.Description) { }
        public AccessCategory ToEntity() => new() { Id = Id, Name = Name, Description = Description };
    }

    [method: JsonConstructor]
    public sealed record LocationDto(
        int Id,
        string Name,
        string? Description)
    {
        public LocationDto(Location e) : this(e.Id, e.Name, e.Description) { }
        public Location ToEntity() => new() { Id = Id, Name = Name, Description = Description };
    }

    [method: JsonConstructor]
    public sealed record RackDto(
        int Id,
        string Name,
        string? Description,
        int? LocationId,
        int? RackUnits)
    {
        public RackDto(Rack e) : this(e.Id, e.Name, e.Description, e.LocationId, e.RackUnits) { }
        public Rack ToEntity() => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            LocationId = LocationId,
            RackUnits = RackUnits
        };
    }

    [method: JsonConstructor]
    public sealed record SubnetDto(
        int Id,
        string Name,
        string Cidr,
        string? Description,
        string? DhcpRangeStart,
        string? DhcpRangeEnd,
        int? VlanId,
        string? Dns1,
        string? Dns2)
    {
        public SubnetDto(Subnet e) : this(e.Id, e.Name, e.Cidr, e.Description, e.DhcpRangeStart, e.DhcpRangeEnd, e.VlanId, e.Dns1, e.Dns2) { }
        public Subnet ToEntity() => new()
        {
            Id = Id,
            Name = Name,
            Cidr = Cidr,
            Description = Description,
            DhcpRangeStart = DhcpRangeStart,
            DhcpRangeEnd = DhcpRangeEnd,
            VlanId = VlanId,
            Dns1 = Dns1,
            Dns2 = Dns2
        };
    }

    [method: JsonConstructor]
    public sealed record IpAssignmentDto(
        int Id,
        string IpAddress,
        int SubnetId,
        int? DeviceId,
        IpReservationStatus Status,
        string? Notes,
        DateTime UpdatedUtc)
    {
        public IpAssignmentDto(IpAssignment e) : this(e.Id, e.IpAddress, e.SubnetId, e.DeviceId, e.Status, e.Notes, e.UpdatedUtc) { }
        public IpAssignment ToEntity() => new()
        {
            Id = Id,
            IpAddress = IpAddress,
            SubnetId = SubnetId,
            DeviceId = DeviceId,
            Status = Status,
            Notes = Notes,
            UpdatedUtc = UpdatedUtc
        };
    }

    [method: JsonConstructor]
    public sealed record DeviceDto(
        int Id,
        string Name,
        string? Hostname,
        string? MacAddress,
        string? IpAddress,
        string? AccessLink,
        int? AccessCategoryId,
        int? LocationId,
        int? RackId,
        int? RackUPosition,
        int? RackUSize,
        int? SubnetId,
        string? UpstreamDeviceName,
        string? UpstreamDeviceMac,
        string? UpstreamConnection,
        int? ParentDeviceId,
        int? ManualUpstreamDeviceId,
        bool IsTopologyRoot,
        string? ConnectionType,
        string? ConnectionDetail,
        int? ClientTypeId,
        int? HostDeviceId,
        string? Manufacturer,
        string? Model,
        string? OperatingSystem,
        string? Usage,
        string? Description,
        string? AssetNumber,
        string? Location,
        string? Source,
        string? UsedNew,
        DateOnly? SourceDate,
        bool IsOnline,
        bool IsStatusTracked,
        bool IsCritical,
        DeviceMonitorMode MonitorMode,
        int? MonitorPort,
        bool MonitorUseHttps,
        string? MonitorHttpPath,
        DateTime? LastSeenAt,
        DateTime? LastOnlineAt,
        DateTime? LastStatusRollupAtUtc,
        bool IgnoreOffline)
    {
        public DeviceDto(Device e) : this(
            e.Id,
            e.Name,
            e.Hostname,
            e.MacAddress,
            e.IpAddress,
            e.AccessLink,
            e.AccessCategoryId,
            e.LocationId,
            e.RackId,
            e.RackUPosition,
            e.RackUSize,
            e.SubnetId,
            e.UpstreamDeviceName,
            e.UpstreamDeviceMac,
            e.UpstreamConnection,
            e.ParentDeviceId,
            e.ManualUpstreamDeviceId,
            e.IsTopologyRoot,
            e.ConnectionType,
            e.ConnectionDetail,
            e.ClientTypeId,
            e.HostDeviceId,
            e.Manufacturer,
            e.Model,
            e.OperatingSystem,
            e.Usage,
            e.Description,
            e.AssetNumber,
            e.Location,
            e.Source,
            e.UsedNew,
            e.SourceDate,
            e.IsOnline,
            e.IsStatusTracked,
            e.IsCritical,
            e.MonitorMode,
            e.MonitorPort,
            e.MonitorUseHttps,
            e.MonitorHttpPath,
            e.LastSeenAt,
            e.LastOnlineAt,
            e.LastStatusRollupAtUtc,
            e.IgnoreOffline)
        { }

        public Device ToEntity() => new()
        {
            Id = Id,
            Name = Name,
            Hostname = Hostname,
            MacAddress = MacAddress,
            IpAddress = IpAddress,
            AccessLink = AccessLink,
            AccessCategoryId = AccessCategoryId,
            LocationId = LocationId,
            RackId = RackId,
            RackUPosition = RackUPosition,
            RackUSize = RackUSize,
            SubnetId = SubnetId,
            UpstreamDeviceName = UpstreamDeviceName,
            UpstreamDeviceMac = UpstreamDeviceMac,
            UpstreamConnection = UpstreamConnection,
            ParentDeviceId = ParentDeviceId,
            ManualUpstreamDeviceId = ManualUpstreamDeviceId,
            IsTopologyRoot = IsTopologyRoot,
            ConnectionType = ConnectionType,
            ConnectionDetail = ConnectionDetail,
            ClientTypeId = ClientTypeId,
            HostDeviceId = HostDeviceId,
            Manufacturer = Manufacturer,
            Model = Model,
            OperatingSystem = OperatingSystem,
            Usage = Usage,
            Description = Description,
            AssetNumber = AssetNumber,
            Location = Location,
            Source = Source,
            UsedNew = UsedNew,
            SourceDate = SourceDate,
            IsOnline = IsOnline,
            IsStatusTracked = IsStatusTracked,
            IsCritical = IsCritical,
            MonitorMode = MonitorMode,
            MonitorPort = MonitorPort,
            MonitorUseHttps = MonitorUseHttps,
            MonitorHttpPath = MonitorHttpPath,
            LastSeenAt = LastSeenAt,
            LastOnlineAt = LastOnlineAt,
            LastStatusRollupAtUtc = LastStatusRollupAtUtc,
            IgnoreOffline = IgnoreOffline
        };
    }

    [method: JsonConstructor]
    public sealed record DeviceStatusEventDto(
        int Id,
        int DeviceId,
        bool IsOnline,
        DateTime ChangedAtUtc,
        string? Source)
    {
        public DeviceStatusEventDto(DeviceStatusEvent e) : this(e.Id, e.DeviceId, e.IsOnline, e.ChangedAtUtc, e.Source) { }
        public DeviceStatusEvent ToEntity() => new()
        {
            Id = Id,
            DeviceId = DeviceId,
            IsOnline = IsOnline,
            ChangedAtUtc = ChangedAtUtc,
            Source = Source
        };
    }

    [method: JsonConstructor]
    public sealed record DeviceStatusDailyDto(
        int Id,
        int DeviceId,
        DateOnly Date,
        int OnlineSeconds,
        int ObservedSeconds)
    {
        public DeviceStatusDailyDto(DeviceStatusDaily e) : this(e.Id, e.DeviceId, e.Date, e.OnlineSeconds, e.ObservedSeconds) { }
        public DeviceStatusDaily ToEntity() => new()
        {
            Id = Id,
            DeviceId = DeviceId,
            Date = Date,
            OnlineSeconds = OnlineSeconds,
            ObservedSeconds = ObservedSeconds
        };
    }

    [method: JsonConstructor]
    public sealed record DeviceIpHistoryDto(
        int Id,
        int DeviceId,
        string IpAddress,
        int? Port,
        string? Source,
        DateTime FirstSeenUtc,
        DateTime? LastSeenUtc)
    {
        public DeviceIpHistoryDto(DeviceIpHistory e) : this(
            e.Id,
            e.DeviceId,
            e.IpAddress,
            e.Port,
            e.Source,
            e.FirstSeenUtc,
            e.LastSeenUtc)
        { }

        public DeviceIpHistory ToEntity() => new()
        {
            Id = Id,
            DeviceId = DeviceId,
            IpAddress = IpAddress,
            Port = Port,
            Source = Source,
            FirstSeenUtc = FirstSeenUtc,
            LastSeenUtc = LastSeenUtc
        };
    }

    [method: JsonConstructor]
    public sealed record ClientOfflineAlertDto(
        int Id,
        int DeviceId,
        string? NameAtTime,
        string? IpAtTime,
        DateTime WentOfflineAtUtc,
        DateTime? CameOnlineAtUtc,
        bool IsAcknowledged,
        DateTime? AcknowledgedAtUtc,
        string? Source)
    {
        public ClientOfflineAlertDto(ClientOfflineAlert e) : this(
            e.Id,
            e.DeviceId,
            e.NameAtTime,
            e.IpAtTime,
            e.WentOfflineAtUtc,
            e.CameOnlineAtUtc,
            e.IsAcknowledged,
            e.AcknowledgedAtUtc,
            e.Source)
        { }

        public ClientOfflineAlert ToEntity() => new()
        {
            Id = Id,
            DeviceId = DeviceId,
            NameAtTime = NameAtTime,
            IpAtTime = IpAtTime,
            WentOfflineAtUtc = WentOfflineAtUtc,
            CameOnlineAtUtc = CameOnlineAtUtc,
            IsAcknowledged = IsAcknowledged,
            AcknowledgedAtUtc = AcknowledgedAtUtc,
            Source = Source
        };
    }

    [method: JsonConstructor]
    public sealed record ClientDiscoveryAlertDto(
        int Id,
        string Mac,
        string? Name,
        string? Hostname,
        string? IpAddress,
        string? ConnectionType,
        string? UpstreamDeviceName,
        string? UpstreamDeviceMac,
        string? UpstreamConnection,
        string? ConnectionDetail,
        bool IsOnline,
        DateTime DetectedAtUtc,
        bool IsAcknowledged,
        DateTime? AcknowledgedAtUtc,
        string? Source)
    {
        public ClientDiscoveryAlertDto(ClientDiscoveryAlert e) : this(
            e.Id,
            e.Mac,
            e.Name,
            e.Hostname,
            e.IpAddress,
            e.ConnectionType,
            e.UpstreamDeviceName,
            e.UpstreamDeviceMac,
            e.UpstreamConnection,
            e.ConnectionDetail,
            e.IsOnline,
            e.DetectedAtUtc,
            e.IsAcknowledged,
            e.AcknowledgedAtUtc,
            e.Source)
        { }

        public ClientDiscoveryAlert ToEntity() => new()
        {
            Id = Id,
            Mac = Mac,
            Name = Name,
            Hostname = Hostname,
            IpAddress = IpAddress,
            ConnectionType = ConnectionType,
            UpstreamDeviceName = UpstreamDeviceName,
            UpstreamDeviceMac = UpstreamDeviceMac,
            UpstreamConnection = UpstreamConnection,
            ConnectionDetail = ConnectionDetail,
            IsOnline = IsOnline,
            DetectedAtUtc = DetectedAtUtc,
            IsAcknowledged = IsAcknowledged,
            AcknowledgedAtUtc = AcknowledgedAtUtc,
            Source = Source
        };
    }

    [method: JsonConstructor]
    public sealed record IgnoredDiscoveryMacDto(
        int Id,
        string Mac,
        DateTime IgnoredAtUtc,
        string? Source)
    {
        public IgnoredDiscoveryMacDto(IgnoredDiscoveryMac e) : this(e.Id, e.Mac, e.IgnoredAtUtc, e.Source) { }
        public IgnoredDiscoveryMac ToEntity() => new()
        {
            Id = Id,
            Mac = Mac,
            IgnoredAtUtc = IgnoredAtUtc,
            Source = Source
        };
    }

    [method: JsonConstructor]
    public sealed record UpdaterRunLogDto(
        int Id,
        DateTime StartedAtUtc,
        DateTime? FinishedAtUtc,
        int ChangedCount,
        string? Error,
        string? Source)
    {
        public UpdaterRunLogDto(UpdaterRunLog e) : this(e.Id, e.StartedAtUtc, e.FinishedAtUtc, e.ChangedCount, e.Error, e.Source) { }
        public UpdaterRunLog ToEntity() => new()
        {
            Id = Id,
            StartedAtUtc = StartedAtUtc,
            FinishedAtUtc = FinishedAtUtc,
            ChangedCount = ChangedCount,
            Error = Error,
            Source = Source
        };
    }

    [method: JsonConstructor]
    public sealed record UpdaterChangeLogDto(
        int Id,
        int RunId,
        int? DeviceId,
        string? DeviceName,
        string? IpAddress,
        string FieldName,
        string? OldValue,
        string? NewValue)
    {
        public UpdaterChangeLogDto(UpdaterChangeLog e) : this(
            e.Id,
            e.RunId,
            e.DeviceId,
            e.DeviceName,
            e.IpAddress,
            e.FieldName,
            e.OldValue,
            e.NewValue)
        { }

        public UpdaterChangeLog ToEntity() => new()
        {
            Id = Id,
            RunId = RunId,
            DeviceId = DeviceId,
            DeviceName = DeviceName,
            IpAddress = IpAddress,
            FieldName = FieldName,
            OldValue = OldValue,
            NewValue = NewValue
        };
    }

    [method: JsonConstructor]
    public sealed record AppUserDto(
        string Id,
        string? UserName,
        string? NormalizedUserName,
        string? Email,
        string? NormalizedEmail,
        bool EmailConfirmed,
        string? PasswordHash,
        string? SecurityStamp,
        string? ConcurrencyStamp,
        string? PhoneNumber,
        bool PhoneNumberConfirmed,
        bool TwoFactorEnabled,
        DateTimeOffset? LockoutEnd,
        bool LockoutEnabled,
        int AccessFailedCount)
    {
        public AppUserDto(ApplicationUser u) : this(
            u.Id,
            u.UserName,
            u.NormalizedUserName,
            u.Email,
            u.NormalizedEmail,
            u.EmailConfirmed,
            u.PasswordHash,
            u.SecurityStamp,
            u.ConcurrencyStamp,
            u.PhoneNumber,
            u.PhoneNumberConfirmed,
            u.TwoFactorEnabled,
            u.LockoutEnd,
            u.LockoutEnabled,
            u.AccessFailedCount)
        { }

        public ApplicationUser ToEntity() => new()
        {
            Id = Id,
            UserName = UserName,
            NormalizedUserName = NormalizedUserName,
            Email = Email,
            NormalizedEmail = NormalizedEmail,
            EmailConfirmed = EmailConfirmed,
            PasswordHash = PasswordHash,
            SecurityStamp = SecurityStamp,
            ConcurrencyStamp = ConcurrencyStamp,
            PhoneNumber = PhoneNumber,
            PhoneNumberConfirmed = PhoneNumberConfirmed,
            TwoFactorEnabled = TwoFactorEnabled,
            LockoutEnd = LockoutEnd,
            LockoutEnabled = LockoutEnabled,
            AccessFailedCount = AccessFailedCount
        };
    }

    [method: JsonConstructor]
    public sealed record RoleDto(
        string Id,
        string? Name,
        string? NormalizedName,
        string? ConcurrencyStamp)
    {
        public RoleDto(IdentityRole r) : this(r.Id, r.Name, r.NormalizedName, r.ConcurrencyStamp) { }
        public IdentityRole ToEntity() => new()
        {
            Id = Id,
            Name = Name,
            NormalizedName = NormalizedName,
            ConcurrencyStamp = ConcurrencyStamp
        };
    }

    [method: JsonConstructor]
    public sealed record UserRoleDto(
        string UserId,
        string RoleId)
    {
        public UserRoleDto(IdentityUserRole<string> ur) : this(ur.UserId, ur.RoleId) { }
        public IdentityUserRole<string> ToEntity() => new()
        {
            UserId = UserId,
            RoleId = RoleId
        };
    }

    [method: JsonConstructor]
    public sealed record UserClaimDto(
        int Id,
        string UserId,
        string? ClaimType,
        string? ClaimValue)
    {
        public UserClaimDto(IdentityUserClaim<string> uc) : this(uc.Id, uc.UserId, uc.ClaimType, uc.ClaimValue) { }
        public IdentityUserClaim<string> ToEntity() => new()
        {
            Id = Id,
            UserId = UserId,
            ClaimType = ClaimType,
            ClaimValue = ClaimValue
        };
    }

    [method: JsonConstructor]
    public sealed record RoleClaimDto(
        int Id,
        string RoleId,
        string? ClaimType,
        string? ClaimValue)
    {
        public RoleClaimDto(IdentityRoleClaim<string> rc) : this(rc.Id, rc.RoleId, rc.ClaimType, rc.ClaimValue) { }
        public IdentityRoleClaim<string> ToEntity() => new()
        {
            Id = Id,
            RoleId = RoleId,
            ClaimType = ClaimType,
            ClaimValue = ClaimValue
        };
    }

    [method: JsonConstructor]
    public sealed record UserLoginDto(
        string LoginProvider,
        string ProviderKey,
        string? ProviderDisplayName,
        string UserId)
    {
        public UserLoginDto(IdentityUserLogin<string> ul) : this(ul.LoginProvider, ul.ProviderKey, ul.ProviderDisplayName, ul.UserId) { }
        public IdentityUserLogin<string> ToEntity() => new()
        {
            LoginProvider = LoginProvider,
            ProviderKey = ProviderKey,
            ProviderDisplayName = ProviderDisplayName,
            UserId = UserId
        };
    }

    [method: JsonConstructor]
    public sealed record UserTokenDto(
        string UserId,
        string LoginProvider,
        string Name,
        string? Value)
    {
        public UserTokenDto(IdentityUserToken<string> ut) : this(ut.UserId, ut.LoginProvider, ut.Name, ut.Value) { }
        public IdentityUserToken<string> ToEntity() => new()
        {
            UserId = UserId,
            LoginProvider = LoginProvider,
            Name = Name,
            Value = Value
        };
    }

    private sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        private const string Format = "yyyy-MM-dd";

        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return default;
            return DateOnly.Parse(value);
        }

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString(Format));
    }
}
