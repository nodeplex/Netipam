using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netipam.Data;
using Netipam.Helpers;
using Netipam.Unifi;

namespace Netipam.Services;

public sealed class UnifiOnlineStatusUpdater : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UnifiOnlineStatusUpdater> _logger;
    private readonly AppSettingsService _settingsService;
    private readonly UnifiUpdaterControl _control;
    private readonly object _enableTriggerLock = new();
    private CancellationTokenSource? _enableTriggerCts;

    private volatile bool _enabled = true;
    private volatile int _intervalSeconds = 60;
    private volatile bool _updateConnectionFieldsWhenOnline = true;
    private volatile bool _syncIpAddress = true;
    private volatile bool _syncOnlineStatus = true;
    private volatile bool _syncName = false;
    private volatile bool _syncHostname = false;
    private volatile bool _syncManufacturer = false;
    private volatile bool _syncModel = false;

    private const int StatusEventRetentionDays = 30;
    private const int StatusRollupRetentionDays = 180;
    private const int EnableTransitionDelaySeconds = 30;

    // Ping tuning (simple defaults; later you can put these in AppSetting)
    private const int PingTimeoutMs = 1000;
    private const int PingAttempts = 1;
    private const int PingConcurrency = 20;

    public UnifiOnlineStatusUpdater(
        IServiceScopeFactory scopeFactory,
        AppSettingsService settingsService,
        UnifiUpdaterControl control,
        ILogger<UnifiOnlineStatusUpdater> logger)
    {
        _scopeFactory = scopeFactory;
        _settingsService = settingsService;
        _control = control;
        _logger = logger;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Load initial settings
        var initial = await _settingsService.LoadAsync(stoppingToken);
        ApplySettings(initial);

        // Small startup delay so the app fully initializes
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        // 🔹 NEW: trigger an initial run shortly after startup
        if (_enabled && _intervalSeconds > 0)
        {
            _control.TriggerNow();
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Task triggerTask = _control.WaitForTriggerAsync(stoppingToken);

                Task completed;
                if (!_enabled || _intervalSeconds <= 0)
                {
                    // Disabled → only respond to manual triggers
                    await triggerTask;
                    completed = triggerTask;
                }
                else
                {
                    var delay = TimeSpan.FromSeconds(Math.Clamp(_intervalSeconds, 10, 3600));
                    Task delayTask = Task.Delay(delay, stoppingToken);
                    completed = await Task.WhenAny(delayTask, triggerTask);
                }

                var triggered = completed == triggerTask;

                // If disabled and not manually triggered, skip
                if (!_enabled && !triggered)
                    continue;

                await _control.RunAsync(async ct => await RunOnce(ct), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _control.LastError = ex.Message;
                _logger.LogError(ex, "UniFi status updater failed.");
            }
        }
    }


    private void OnSettingsChanged(AppSetting s) => ApplySettings(s);

    private void ApplySettings(AppSetting s)
    {
        var wasEnabled = _enabled;

        _enabled = s.UnifiUpdaterEnabled;
        _intervalSeconds = s.UnifiUpdaterIntervalSeconds;
        _updateConnectionFieldsWhenOnline = s.UnifiUpdateConnectionFieldsWhenOnline;
        _syncIpAddress = s.UnifiSyncIpAddress;
        _syncOnlineStatus = s.UnifiSyncOnlineStatus;
        _syncName = s.UnifiSyncName;
        _syncHostname = s.UnifiSyncHostname;
        _syncManufacturer = s.UnifiSyncManufacturer;
        _syncModel = s.UnifiSyncModel;

        _logger.LogInformation(
            "UniFi updater settings applied: Enabled={Enabled}, Interval={Interval}s, UpdateConnectionFields={UpdateConn}",
            _enabled, _intervalSeconds, _updateConnectionFieldsWhenOnline);

        if (!wasEnabled && _enabled && _intervalSeconds > 0)
        {
            ScheduleEnableTransitionRun();
        }
        else if (!_enabled || _intervalSeconds <= 0)
        {
            CancelEnableTransitionRun();
        }
    }

    private void ScheduleEnableTransitionRun()
    {
        CancellationTokenSource cts;
        lock (_enableTriggerLock)
        {
            _enableTriggerCts?.Cancel();
            _enableTriggerCts?.Dispose();
            _enableTriggerCts = new CancellationTokenSource();
            cts = _enableTriggerCts;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(EnableTransitionDelaySeconds), cts.Token);
                if (_enabled && _intervalSeconds > 0 && !cts.Token.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "UniFi updater enabled. Triggering first run after {DelaySeconds}s warm-up.",
                        EnableTransitionDelaySeconds);
                    _control.TriggerNow();
                }
            }
            catch (OperationCanceledException)
            {
                // expected when updater is disabled before delayed run fires
            }
        }, cts.Token);
    }

    private void CancelEnableTransitionRun()
    {
        lock (_enableTriggerLock)
        {
            _enableTriggerCts?.Cancel();
            _enableTriggerCts?.Dispose();
            _enableTriggerCts = null;
        }
    }

    private async Task RunOnce(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unifi = scope.ServiceProvider.GetRequiredService<UnifiApiClient>();
        var pingSvc = scope.ServiceProvider.GetRequiredService<PingStatusService>();

        using JsonDocument activeDoc = await unifi.GetActiveClientsAsync(ct);
        using JsonDocument knownDoc = await unifi.GetKnownClientsAsync(ct);
        using JsonDocument devicesDoc = await unifi.GetDevicesAsync(ct);
        using JsonDocument networksDoc = await unifi.GetNetworksAsync(ct);

        var onlineMap = _syncOnlineStatus
            ? UnifiParsers.ParseActiveStatus(activeDoc)
            : new Dictionary<string, UnifiParsers.ActiveClientStatus>(StringComparer.OrdinalIgnoreCase);
        var deviceNameMap = UnifiParsers.BuildDeviceNameMap(devicesDoc);
        var unifiClients = UnifiParsers.MergeClients(activeDoc, knownDoc, deviceNameMap);
        var unifiByMac = unifiClients
            .Where(u => !string.IsNullOrWhiteSpace(u.Mac))
            .GroupBy(u => UnifiParsers.NormalizeMac(u.Mac))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var infraDevices = UnifiParsers.ParseInfraDevices(devicesDoc);
        var infraByMac = infraDevices
            .Where(d => !string.IsNullOrWhiteSpace(d.Mac))
            .ToDictionary(d => d.Mac, d => d, StringComparer.OrdinalIgnoreCase);

        // Devices we can match by MAC, plus any manual monitor devices
        var devices = await db.Devices
            .Where(d => !string.IsNullOrWhiteSpace(d.MacAddress) || d.MonitorMode != DeviceMonitorMode.Normal)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var runLog = new UpdaterRunLog
        {
            StartedAtUtc = now,
            Source = "UniFi"
        };
        db.UpdaterRunLogs.Add(runLog);

        var wanInterfaces = UnifiParsers.ParseWanInterfaces(devicesDoc);
        if (wanInterfaces.Count > 0)
        {
            var existing = await db.WanInterfaceStatuses.ToListAsync(ct);
            db.WanInterfaceStatuses.RemoveRange(existing);

            db.WanInterfaceStatuses.AddRange(wanInterfaces.Select(w => new WanInterfaceStatus
            {
                GatewayName = w.GatewayName,
                GatewayMac = w.GatewayMac,
                InterfaceName = w.InterfaceName,
                IsUp = w.IsUp,
                IpAddress = w.IpAddress,
                UpdatedAtUtc = now
            }));
        }

        // Update subnets from UniFi networks
        var networks = UnifiParsers.ParseNetworks(networksDoc)
            .Where(n => !string.IsNullOrWhiteSpace(n.Cidr))
            .ToList();
        if (networks.Count > 0)
        {
            var subnets = await db.Subnets.ToListAsync(ct);
            var subnetByCidr = subnets
                .Where(s => !string.IsNullOrWhiteSpace(s.Cidr))
                .ToDictionary(s => s.Cidr!.Trim(), s => s, StringComparer.OrdinalIgnoreCase);

            foreach (var n in networks)
            {
                var cidr = n.Cidr!.Trim();
                if (!subnetByCidr.TryGetValue(cidr, out var entity))
                {
                    // Only update existing subnets during updater runs
                    continue;
                }

                entity.Name = n.Name?.Trim() ?? "";
                entity.Cidr = cidr;
                entity.DhcpRangeStart = n.DhcpStart?.Trim();
                entity.DhcpRangeEnd = n.DhcpEnd?.Trim();
                entity.VlanId = n.VlanId;
                entity.Dns1 = n.Dns1?.Trim();
                entity.Dns2 = n.Dns2?.Trim();
            }
        }

        // Preload OPEN alerts for devices
        var deviceIds = devices.Select(d => d.Id).ToList();

        var openAlerts = await db.ClientOfflineAlerts
            .Where(a => deviceIds.Contains(a.DeviceId) && a.CameOnlineAtUtc == null)
            .ToListAsync(ct);

        var openAlertByDeviceId = openAlerts
            .GroupBy(a => a.DeviceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.WentOfflineAtUtc).First());

        var openFirmwareAlerts = await db.DeviceFirmwareUpdateAlerts
            .Where(a => deviceIds.Contains(a.DeviceId) && a.ResolvedAtUtc == null)
            .ToListAsync(ct);

        var openFirmwareByDeviceId = openFirmwareAlerts
            .GroupBy(a => a.DeviceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ignoredMacs = await db.IgnoredDiscoveryMacs
            .AsNoTracking()
            .Select(i => i.Mac)
            .ToListAsync(ct);
        var ignoredSet = new HashSet<string>(ignoredMacs, StringComparer.OrdinalIgnoreCase);

        var knownMacs = new HashSet<string>(
            devices
                .Where(d => !string.IsNullOrWhiteSpace(d.MacAddress))
                .Select(d => UnifiParsers.NormalizeMac(d.MacAddress!)),
            StringComparer.OrdinalIgnoreCase);

        var openDiscoveryMacs = await db.ClientDiscoveryAlerts
            .Where(a => !a.IsAcknowledged)
            .Select(a => a.Mac)
            .ToListAsync(ct);
        var openDiscoverySet = new HashSet<string>(openDiscoveryMacs, StringComparer.OrdinalIgnoreCase);

        int changedCount = 0;
        bool anyDbChanges = false;
        var statusEvents = new List<DeviceStatusEvent>();
        var onlineIncrements = new Dictionary<(int DeviceId, DateOnly Date), int>();
        var observedIncrements = new Dictionary<(int DeviceId, DateOnly Date), int>();
        var changeLogs = new List<UpdaterChangeLog>();
        var devicesWithHistory = await db.DeviceIpHistories
            .AsNoTracking()
            .Where(h => deviceIds.Contains(h.DeviceId))
            .Select(h => h.DeviceId)
            .Distinct()
            .ToHashSetAsync(ct);

        foreach (var d in devices)
        {
            if (!devicesWithHistory.Contains(d.Id) && !string.IsNullOrWhiteSpace(d.IpAddress))
            {
                await IpHistoryHelper.TrackIpChangeAsync(db, d, "Initial", now, ct);
                devicesWithHistory.Add(d.Id);
                anyDbChanges = true;
            }

            if (string.IsNullOrWhiteSpace(d.MacAddress))
                continue;

            var mac = UnifiParsers.NormalizeMac(d.MacAddress!);
            unifiByMac.TryGetValue(mac, out var u);

            if (u is not null && _syncIpAddress)
            {
                var newIp = u.IpAddress?.Trim();
                if (!string.IsNullOrWhiteSpace(newIp) &&
                    !string.Equals(d.IpAddress, newIp, StringComparison.OrdinalIgnoreCase))
                {
                    var oldValue = d.IpAddress;
                    d.IpAddress = newIp;
                    changeLogs.Add(BuildChangeLog(runLog, d, "IpAddress", oldValue, newIp));
                    await IpHistoryHelper.TrackIpChangeAsync(db, d, "UniFi", now, ct);
                    anyDbChanges = true;
                    changedCount++;
                }
            }

            if (u is not null && _syncName)
            {
                var newName = u.Name?.Trim();
                if (!string.IsNullOrWhiteSpace(newName) &&
                    !string.Equals(d.Name, newName, StringComparison.OrdinalIgnoreCase))
                {
                    var oldValue = d.Name;
                    d.Name = newName;
                    changeLogs.Add(BuildChangeLog(runLog, d, "Name", oldValue, newName));
                    anyDbChanges = true;
                    changedCount++;
                }
            }

            if (u is not null && _syncHostname)
            {
                var newHostname = u.Hostname?.Trim();
                if (!string.IsNullOrWhiteSpace(newHostname) &&
                    !string.Equals(d.Hostname, newHostname, StringComparison.OrdinalIgnoreCase))
                {
                    var oldValue = d.Hostname;
                    d.Hostname = newHostname;
                    changeLogs.Add(BuildChangeLog(runLog, d, "Hostname", oldValue, newHostname));
                    anyDbChanges = true;
                    changedCount++;
                }
            }

            if (u is not null && _syncManufacturer)
            {
                var newManufacturer = u.Manufacturer?.Trim();
                if (!string.IsNullOrWhiteSpace(newManufacturer) &&
                    !string.Equals(d.Manufacturer, newManufacturer, StringComparison.OrdinalIgnoreCase))
                {
                    var oldValue = d.Manufacturer;
                    d.Manufacturer = newManufacturer;
                    changeLogs.Add(BuildChangeLog(runLog, d, "Manufacturer", oldValue, newManufacturer));
                    anyDbChanges = true;
                    changedCount++;
                }
            }

            if (u is not null && _syncModel)
            {
                var newModel = u.Model?.Trim();
                if (!string.IsNullOrWhiteSpace(newModel) &&
                    !string.Equals(d.Model, newModel, StringComparison.OrdinalIgnoreCase))
                {
                    var oldValue = d.Model;
                    d.Model = newModel;
                    changeLogs.Add(BuildChangeLog(runLog, d, "Model", oldValue, newModel));
                    anyDbChanges = true;
                    changedCount++;
                }
            }

            if (infraByMac.TryGetValue(mac, out var infra))
            {
                var target = infra.UpgradeToVersion?.Trim();
                var current = infra.Version?.Trim();
                var isUpgradable = infra.IsUpgradable == true;

                if (isUpgradable && !string.IsNullOrWhiteSpace(target))
                {
                    if (openFirmwareByDeviceId.TryGetValue(d.Id, out var existingAlerts))
                    {
                        var matching = existingAlerts.FirstOrDefault(a =>
                            a.ResolvedAtUtc == null &&
                            string.Equals(a.TargetVersion, target, StringComparison.OrdinalIgnoreCase));

                        if (matching != null)
                        {
                            if (!string.Equals(matching.CurrentVersion, current, StringComparison.OrdinalIgnoreCase))
                            {
                                matching.CurrentVersion = current;
                                anyDbChanges = true;
                            }
                        }
                        else
                        {
                            foreach (var alert in existingAlerts.Where(a => a.ResolvedAtUtc == null))
                            {
                                alert.ResolvedAtUtc = now;
                                anyDbChanges = true;
                            }

                            db.DeviceFirmwareUpdateAlerts.Add(new DeviceFirmwareUpdateAlert
                            {
                                DeviceId = d.Id,
                                NameAtTime = d.Name,
                                MacAtTime = d.MacAddress,
                                ModelAtTime = d.Model,
                                CurrentVersion = current,
                                TargetVersion = target,
                                DetectedAtUtc = now,
                                Source = "UniFi"
                            });

                            anyDbChanges = true;
                        }
                    }
                    else
                    {
                        db.DeviceFirmwareUpdateAlerts.Add(new DeviceFirmwareUpdateAlert
                        {
                            DeviceId = d.Id,
                            NameAtTime = d.Name,
                            MacAtTime = d.MacAddress,
                            ModelAtTime = d.Model,
                            CurrentVersion = current,
                            TargetVersion = target,
                            DetectedAtUtc = now,
                            Source = "UniFi"
                        });

                        anyDbChanges = true;
                    }
                }
                else if (openFirmwareByDeviceId.TryGetValue(d.Id, out var openAlertsForDevice))
                {
                    foreach (var alert in openAlertsForDevice.Where(a => a.ResolvedAtUtc == null))
                    {
                        alert.ResolvedAtUtc = now;
                        anyDbChanges = true;
                    }
                }
            }
        }

        foreach (var u in unifiClients)
        {
            if (!u.IsOnline)
                continue;

            var mac = UnifiParsers.NormalizeMac(u.Mac);
            if (knownMacs.Contains(mac) || ignoredSet.Contains(mac) || openDiscoverySet.Contains(mac))
                continue;

            var alert = new ClientDiscoveryAlert
            {
                Mac = mac,
                Name = u.Name?.Trim(),
                Hostname = u.Hostname?.Trim(),
                IpAddress = u.IpAddress?.Trim(),
                ConnectionType = u.ConnectionType?.Trim(),
                UpstreamDeviceName = u.UpstreamDeviceName?.Trim(),
                UpstreamDeviceMac = u.UpstreamDeviceMac?.Trim(),
                UpstreamConnection = u.UpstreamConnection?.Trim(),
                ConnectionDetail = u.ConnectionDetail?.Trim(),
                IsOnline = u.IsOnline,
                DetectedAtUtc = now,
                IsAcknowledged = false,
                Source = "UniFi"
            };

            db.ClientDiscoveryAlerts.Add(alert);
            anyDbChanges = true;
        }

        if (!_syncOnlineStatus)
        {
            runLog.FinishedAtUtc = DateTime.UtcNow;
            runLog.DurationMs = (int)Math.Max(0, (runLog.FinishedAtUtc.Value - runLog.StartedAtUtc).TotalMilliseconds);
            runLog.ChangedCount = changeLogs.Count;

            await db.SaveChangesAsync(ct);

            _control.LastRunUtc = DateTime.UtcNow;
            _control.LastChangedCount = changedCount;
            _control.LastError = null;

            _control.NotifyStatusChanged();

            _logger.LogInformation(
                "UniFi status updater completed (online status sync disabled). Changed={Count}.",
                changedCount);
            return;
        }

        // Decide who needs ping (UniFi says offline) or custom monitor checks
        var finalOnline = new Dictionary<int, bool>(devices.Count);
        var statusSourceByDeviceId = new Dictionary<int, string>(devices.Count);

        var customMonitor = devices
            .Where(d => d.IsStatusTracked && d.MonitorMode != DeviceMonitorMode.Normal)
            .ToList();

        var toPing = new List<Device>();
        foreach (var d in devices)
        {
            if (!d.IsStatusTracked)
                continue;

            if (d.MonitorMode != DeviceMonitorMode.Normal)
                continue;

            var mac = string.IsNullOrWhiteSpace(d.MacAddress)
                ? null
                : UnifiParsers.NormalizeMac(d.MacAddress!);

            // Prefer explicit infra "connected" state from UniFi /devices when available.
            if (mac is not null && infraByMac.TryGetValue(mac, out var infraStatus) && infraStatus.IsOnline)
            {
                finalOnline[d.Id] = true;
                statusSourceByDeviceId[d.Id] = "InfraConnected";
            }
            else if (mac is not null && onlineMap.TryGetValue(mac, out _))
            {
                finalOnline[d.Id] = true;
                statusSourceByDeviceId[d.Id] = "ActiveClient";
            }
            else
            {
                toPing.Add(d);
                statusSourceByDeviceId[d.Id] = "PingFallback";
            }
        }

        if (customMonitor.Count > 0)
        {
            using var throttler = new SemaphoreSlim(PingConcurrency);

            var monitorTasks = customMonitor.Select(async d =>
            {
                await throttler.WaitAsync(ct);
                try
                {
                    var alive = await EvaluateMonitorAsync(d, pingSvc, ct);
                    return (d.Id, alive);
                }
                finally
                {
                    throttler.Release();
                }
            });

            foreach (var t in await Task.WhenAll(monitorTasks))
            {
                finalOnline[t.Id] = t.alive;
                statusSourceByDeviceId[t.Id] = "CustomMonitor";
            }
        }

        // Ping pass with limited concurrency
        if (toPing.Count > 0)
        {
            using var throttler = new SemaphoreSlim(PingConcurrency);

            var pingTasks = toPing.Select(async d =>
            {
                await throttler.WaitAsync(ct);
                try
                {
                    var alive = await pingSvc.IsAliveAsync(d.IpAddress, PingTimeoutMs, PingAttempts, ct);
                    return (d.Id, alive);
                }
                finally
                {
                    throttler.Release();
                }
            });

            foreach (var t in await Task.WhenAll(pingTasks))
            {
                finalOnline[t.Id] = t.alive;
                statusSourceByDeviceId[t.Id] = "Ping";
            }
        }

        foreach (var d in devices)
        {
            if (!d.IsStatusTracked)
            {
                if (openAlertByDeviceId.TryGetValue(d.Id, out var openIgnored))
                {
                    if (openIgnored.CameOnlineAtUtc == null)
                    {
                        openIgnored.CameOnlineAtUtc = now;
                        anyDbChanges = true;
                    }
                }
                continue;
            }

            var mac = string.IsNullOrWhiteSpace(d.MacAddress)
                ? null
                : UnifiParsers.NormalizeMac(d.MacAddress!);

            var wasOnline = d.IsOnline;
            var isNowOnline = finalOnline.TryGetValue(d.Id, out var v) && v;
            var statusSource = statusSourceByDeviceId.TryGetValue(d.Id, out var source) ? source : "Unknown";

            _logger.LogDebug(
                "Status resolve: DeviceId={DeviceId}, Name={Name}, Mac={Mac}, Source={Source}, Previous={Previous}, Current={Current}",
                d.Id, d.Name, d.MacAddress, statusSource, wasOnline ? "Online" : "Offline", isNowOnline ? "Online" : "Offline");

            if (wasOnline != isNowOnline)
                changedCount++;

            var lastRollupAt = d.LastStatusRollupAtUtc;
            if (lastRollupAt is null)
            {
                d.LastStatusRollupAtUtc = now;
                anyDbChanges = true;
            }
            else if (lastRollupAt < now)
            {
                AddSeconds(observedIncrements, d.Id, lastRollupAt.Value, now);
                if (wasOnline)
                    AddSeconds(onlineIncrements, d.Id, lastRollupAt.Value, now);

                d.LastStatusRollupAtUtc = now;
                anyDbChanges = true;
            }

            if (wasOnline != isNowOnline)
            {
                statusEvents.Add(new DeviceStatusEvent
                {
                    DeviceId = d.Id,
                    IsOnline = isNowOnline,
                    ChangedAtUtc = now,
                    Source = d.MonitorMode == DeviceMonitorMode.Normal ? "UniFi+Ping" : "Monitor"
                });
                changeLogs.Add(BuildChangeLog(runLog, d, "OnlineStatus",
                    wasOnline ? "Online" : "Offline",
                    isNowOnline ? "Online" : "Offline"));
                anyDbChanges = true;
            }

            if (isNowOnline)
            {
                if (mac is not null && onlineMap.TryGetValue(mac, out var status))
                {
                    if (_updateConnectionFieldsWhenOnline)
                    {
                        d.ConnectionType = status.ConnectionType;
                        d.ConnectionDetail = status.ConnectionDetail;
                    }
                    if (_updateConnectionFieldsWhenOnline && d.HostDeviceId is null && unifiByMac.TryGetValue(mac, out var u))
                    {
                        d.UpstreamDeviceName = u.UpstreamDeviceName;
                        d.UpstreamDeviceMac = u.UpstreamDeviceMac;
                        d.UpstreamConnection = u.UpstreamConnection;
                    }
                }

                d.IsOnline = true;
                d.LastSeenAt = now;
                d.LastOnlineAt = now;

                if (openAlertByDeviceId.TryGetValue(d.Id, out var open))
                {
                    open.CameOnlineAtUtc = now;
                    anyDbChanges = true;
                }
            }
            else
            {
                d.IsOnline = false;

                if (wasOnline && !d.IgnoreOffline)
                {
                    if (!openAlertByDeviceId.ContainsKey(d.Id))
                    {
                        db.ClientOfflineAlerts.Add(new ClientOfflineAlert
                        {
                            DeviceId = d.Id,
                            NameAtTime = d.Name,
                            IpAtTime = d.IpAddress,
                            WentOfflineAtUtc = now,
                            CameOnlineAtUtc = null,
                            IsAcknowledged = false,
                            AcknowledgedAtUtc = null,
                            Source = "UniFi+Ping"
                        });

                        anyDbChanges = true;
                    }
                }

                if (d.IgnoreOffline && openAlertByDeviceId.TryGetValue(d.Id, out var openIgnored))
                {
                    if (openIgnored.CameOnlineAtUtc == null)
                    {
                        openIgnored.CameOnlineAtUtc = now;
                        anyDbChanges = true;
                    }
                }
            }
        }

        if (statusEvents.Count > 0)
            db.DeviceStatusEvents.AddRange(statusEvents);

        if (changeLogs.Count > 0)
            db.UpdaterChangeLogs.AddRange(changeLogs);

        if (onlineIncrements.Count > 0 || observedIncrements.Count > 0)
        {
            var rollupKeys = new HashSet<(int DeviceId, DateOnly Date)>();
            foreach (var key in onlineIncrements.Keys)
                rollupKeys.Add(key);
            foreach (var key in observedIncrements.Keys)
                rollupKeys.Add(key);

            var deviceIdsForRollup = rollupKeys.Select(k => k.DeviceId).Distinct().ToList();
            var datesForRollup = rollupKeys.Select(k => k.Date).Distinct().ToList();

            var existingRollups = await db.DeviceStatusDaily
                .Where(r => deviceIdsForRollup.Contains(r.DeviceId) && datesForRollup.Contains(r.Date))
                .ToListAsync(ct);

            var existingMap = existingRollups.ToDictionary(r => (r.DeviceId, r.Date));

            foreach (var key in rollupKeys)
            {
                onlineIncrements.TryGetValue(key, out var onlineSeconds);
                observedIncrements.TryGetValue(key, out var observedSeconds);

                if (onlineSeconds <= 0 && observedSeconds <= 0)
                    continue;

                if (existingMap.TryGetValue(key, out var row))
                {
                    row.OnlineSeconds += onlineSeconds;
                    row.ObservedSeconds += observedSeconds;
                    if (row.ObservedSeconds < row.OnlineSeconds)
                        row.ObservedSeconds = row.OnlineSeconds;
                    row.UpdatedAtUtc = now;
                }
                else
                {
                    var safeObserved = Math.Max(observedSeconds, onlineSeconds);
                    db.DeviceStatusDaily.Add(new DeviceStatusDaily
                    {
                        DeviceId = key.DeviceId,
                        Date = key.Date,
                        OnlineSeconds = onlineSeconds,
                        ObservedSeconds = safeObserved,
                        UpdatedAtUtc = now
                    });
                }
            }

            anyDbChanges = true;
        }

        var eventCutoff = now.AddDays(-StatusEventRetentionDays);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.Local);
        var rollupCutoff = DateOnly.FromDateTime(localNow.AddDays(-StatusRollupRetentionDays));
        var runCutoff = now.AddHours(-24);

        var oldEvents = await db.DeviceStatusEvents
            .Where(e => e.ChangedAtUtc < eventCutoff)
            .ToListAsync(ct);

        if (oldEvents.Count > 0)
        {
            db.DeviceStatusEvents.RemoveRange(oldEvents);
            anyDbChanges = true;
        }

        var oldRollups = await db.DeviceStatusDaily
            .Where(r => r.Date < rollupCutoff)
            .ToListAsync(ct);

        if (oldRollups.Count > 0)
        {
            db.DeviceStatusDaily.RemoveRange(oldRollups);
            anyDbChanges = true;
        }

        var oldRuns = await db.UpdaterRunLogs
            .Where(r => r.StartedAtUtc < runCutoff)
            .ToListAsync(ct);

        if (oldRuns.Count > 0)
        {
            db.UpdaterRunLogs.RemoveRange(oldRuns);
            anyDbChanges = true;
        }

        runLog.FinishedAtUtc = DateTime.UtcNow;
        runLog.DurationMs = (int)Math.Max(0, (runLog.FinishedAtUtc.Value - runLog.StartedAtUtc).TotalMilliseconds);
        runLog.ChangedCount = changeLogs.Count;

        await db.SaveChangesAsync(ct);

        _control.LastRunUtc = DateTime.UtcNow;
        _control.LastChangedCount = changedCount;
        _control.LastError = null;

        _control.NotifyStatusChanged(); // NEW: push UI refresh

        var sourceSummary = statusSourceByDeviceId
            .GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Count()}")
            .ToList();

        _logger.LogInformation(
            "UniFi status updater completed. Changed={Count}. UnifiOfflinePinged={Pinged}. StatusSources=[{Sources}]",
            changedCount, toPing.Count, string.Join(", ", sourceSummary));
    }


    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
        CancelEnableTransitionRun();
        return base.StopAsync(cancellationToken);
    }

    private static UpdaterChangeLog BuildChangeLog(
        UpdaterRunLog run,
        Device device,
        string fieldName,
        string? oldValue,
        string? newValue)
    {
        return new UpdaterChangeLog
        {
            Run = run,
            DeviceId = device.Id,
            DeviceName = device.Name,
            IpAddress = device.IpAddress,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue
        };
    }


    private static void AddSeconds(
        Dictionary<(int DeviceId, DateOnly Date), int> increments,
        int deviceId,
        DateTime startUtc,
        DateTime endUtc)
    {
        if (endUtc <= startUtc)
            return;

        var tz = TimeZoneInfo.Local;
        var cursorUtc = startUtc;
        while (cursorUtc < endUtc)
        {
            var localCursor = TimeZoneInfo.ConvertTimeFromUtc(cursorUtc, tz);
            var date = DateOnly.FromDateTime(localCursor);
            var localEndOfDay = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var endOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(localEndOfDay, tz);
            var segmentEndUtc = endUtc < endOfDayUtc ? endUtc : endOfDayUtc;
            var seconds = (int)Math.Max(0, (segmentEndUtc - cursorUtc).TotalSeconds);

            if (seconds > 0)
            {
                var key = (deviceId, date);
                increments[key] = increments.TryGetValue(key, out var current)
                    ? current + seconds
                    : seconds;
            }

            cursorUtc = segmentEndUtc;
        }
    }

    private static bool RequiresPing(DeviceMonitorMode mode)
        => mode is DeviceMonitorMode.PingOnly
            or DeviceMonitorMode.PingAndPort
            or DeviceMonitorMode.PingAndHttp;

    private void LogIpDebugForMac(JsonDocument knownDoc, string targetMac)
    {
        var normalized = UnifiParsers.NormalizeMac(targetMac);

        if (!knownDoc.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            return;

        foreach (var c in data.EnumerateArray())
        {
            if (!c.TryGetProperty("mac", out var macProp) || macProp.ValueKind != JsonValueKind.String)
                continue;

            var mac = UnifiParsers.NormalizeMac(macProp.GetString() ?? "");
            if (!string.Equals(mac, normalized, StringComparison.OrdinalIgnoreCase))
                continue;

            string? GetString(JsonElement obj, string prop)
            {
                if (!obj.TryGetProperty(prop, out var p))
                    return null;
                return p.ValueKind switch
                {
                    JsonValueKind.String => p.GetString(),
                    JsonValueKind.Number => p.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }

            string? GetBoolString(string prop)
            {
                if (!c.TryGetProperty(prop, out var p))
                    return null;
                return p.ValueKind switch
                {
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.String => p.GetString(),
                    JsonValueKind.Number => p.GetRawText(),
                    _ => null
                };
            }

            var ip = GetString(c, "ip");
            var fixedIp = GetString(c, "fixed_ip");
            var lastIp = GetString(c, "last_ip");
            var useFixed = GetBoolString("use_fixedip") ?? GetBoolString("use_fixed_ip");

            _logger.LogInformation(
                "UniFi client IP debug for {Mac}: ip={Ip}, fixed_ip={Fixed}, last_ip={Last}, use_fixedip={UseFixed}",
                mac, ip, fixedIp, lastIp, useFixed);
            return;
        }
    }

    private static bool RequiresPort(DeviceMonitorMode mode)
        => mode is DeviceMonitorMode.PortOnly
            or DeviceMonitorMode.PingAndPort;

    private static bool RequiresHttp(DeviceMonitorMode mode)
        => mode is DeviceMonitorMode.HttpOnly
            or DeviceMonitorMode.PingAndHttp;

    private static async Task<bool> EvaluateMonitorAsync(Device device, PingStatusService pingSvc, CancellationToken ct)
    {
        var mode = device.MonitorMode;

        var pingOk = !RequiresPing(mode) ||
                     await pingSvc.IsAliveAsync(device.IpAddress, PingTimeoutMs, PingAttempts, ct);

        var portOk = !RequiresPort(mode) ||
                     await pingSvc.IsTcpOpenAsync(device.IpAddress, device.MonitorPort ?? 0, PingTimeoutMs, ct);

        var httpOk = !RequiresHttp(mode) ||
                     await pingSvc.IsHttpOkAsync(
                         device.IpAddress,
                         device.MonitorPort,
                         device.MonitorUseHttps,
                         device.MonitorHttpPath,
                         PingTimeoutMs,
                         ct);

        return pingOk && portOk && httpOk;
    }
}
