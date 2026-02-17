using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Netipam.Data;
using Netipam.Proxmox;
using Netipam.Unifi;

namespace Netipam.Services;

public sealed class ProxmoxHostMappingUpdater : BackgroundService
{
    private sealed record HostRef(
        int Id,
        string? Name,
        int? ProxmoxInstanceId,
        string? ProxmoxNodeIdentifier);

    private static readonly Regex MacRegex = new(
        @"([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}",
        RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppSettingsService _settingsService;
    private readonly ProxmoxUpdaterControl _control;
    private readonly ILogger<ProxmoxHostMappingUpdater> _logger;

    public ProxmoxHostMappingUpdater(
        IServiceScopeFactory scopeFactory,
        AppSettingsService settingsService,
        ProxmoxUpdaterControl control,
        ILogger<ProxmoxHostMappingUpdater> logger)
    {
        _scopeFactory = scopeFactory;
        _settingsService = settingsService;
        _control = control;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        _control.TriggerNow();

        var delaySeconds = 120;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var wait = TimeSpan.FromSeconds(Math.Clamp(delaySeconds, 30, 86400));
                var delayTask = Task.Delay(wait, stoppingToken);
                var triggerTask = _control.WaitForTriggerAsync(stoppingToken);
                await Task.WhenAny(delayTask, triggerTask);

                delaySeconds = await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Proxmox host mapping updater failed.");
            }
        }
    }

    private async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var proxmox = scope.ServiceProvider.GetRequiredService<ProxmoxApiClient>();

        var profiles = await db.ProxmoxInstances
            .AsNoTracking()
            .Where(p => p.Enabled)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        if (profiles.Count == 0)
            return 300;

        var hosts = await db.Devices
            .AsNoTracking()
            .Where(d => d.IsProxmoxHost &&
                        d.ProxmoxInstanceId != null &&
                        d.ProxmoxNodeIdentifier != null)
            .Select(d => new HostRef(
                d.Id,
                d.Name,
                d.ProxmoxInstanceId,
                d.ProxmoxNodeIdentifier))
            .ToListAsync(ct);

        if (hosts.Count == 0)
            return profiles.Min(p => NormalizeInterval(p.IntervalSeconds));

        _logger.LogDebug(
            "Proxmox host mapping scan starting. EnabledProfiles={Profiles}, HostDevicesConfigured={Hosts}.",
            profiles.Count,
            hosts.Count);

        var clients = await db.Devices
            .Include(d => d.ClientType)
            .Where(d => d.MacAddress != null)
            .ToListAsync(ct);

        var proxmoxVmTypeId = await db.ClientTypes
            .AsNoTracking()
            .Where(t => t.Name == "Proxmox VM")
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);

        var proxmoxLxcTypeId = await db.ClientTypes
            .AsNoTracking()
            .Where(t => t.Name == "Proxmox LXC")
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);

        var clientByMac = clients
            .Where(c => !string.IsNullOrWhiteSpace(c.MacAddress))
            .GroupBy(c => UnifiParsers.NormalizeMac(c.MacAddress!))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var changes = 0;

        foreach (var profile in profiles)
        {
            var apiTokenSecret = _settingsService.Unprotect(profile.ApiTokenSecretProtected);
            if (string.IsNullOrWhiteSpace(profile.BaseUrl) ||
                string.IsNullOrWhiteSpace(profile.ApiTokenId) ||
                string.IsNullOrWhiteSpace(apiTokenSecret))
            {
                _logger.LogWarning(
                    "Skipping Proxmox profile '{Profile}' because URL/token settings are incomplete.",
                    profile.Name);
                continue;
            }

            var hostByNode = new Dictionary<string, HostRef>(StringComparer.OrdinalIgnoreCase);
            foreach (var host in hosts.Where(h => h.ProxmoxInstanceId == profile.Id))
            {
                foreach (var key in ExpandNodeKeys(host.ProxmoxNodeIdentifier))
                {
                    if (!hostByNode.ContainsKey(key))
                        hostByNode[key] = host;
                }
            }

            if (hostByNode.Count == 0)
            {
                _logger.LogDebug(
                    "Skipping Proxmox profile '{Profile}' because no host devices are assigned to this profile.",
                    profile.Name);
                continue;
            }

            var vmRefs = await DiscoverVmRefsAsync(
                proxmox,
                profile.BaseUrl,
                profile.ApiTokenId,
                apiTokenSecret,
                ct);

            _logger.LogDebug(
                "Proxmox profile '{Profile}' discovered {VmRefs} VM/CT resource entries and {HostNodeKeys} configured host node keys.",
                profile.Name,
                vmRefs.Count,
                hostByNode.Count);

            if (vmRefs.Count > 0)
            {
                var distinctVmNodes = vmRefs
                    .Select(v => NormalizeNode(v.Node))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                _logger.LogDebug(
                    "Proxmox profile '{Profile}' VM nodes discovered: {VmNodes}. Configured host node keys: {HostNodes}.",
                    profile.Name,
                    string.Join(", ", distinctVmNodes),
                    string.Join(", ", hostByNode.Keys.OrderBy(k => k)));
            }

            var matched = 0;
            var configErrors = 0;
            foreach (var vm in vmRefs)
            {
                if (!TryResolveHost(hostByNode, vm.Node, out var host))
                    continue;
                try
                {
                    using var configDoc = await proxmox.GetVmConfigAsync(
                        profile.BaseUrl,
                        profile.ApiTokenId,
                        apiTokenSecret,
                        vm.Node,
                        vm.VmType,
                        vm.Vmid,
                        ct);

                    if (!TryGetDataObject(configDoc.RootElement, out var config))
                        continue;

                    foreach (var mac in ExtractMacs(config))
                    {
                        if (!clientByMac.TryGetValue(mac, out var client))
                            continue;

                        // Keep this automation client-focused.
                        if (client.ClientType?.IsDevice == true)
                            continue;

                        matched++;
                        if (!profile.UpdateExistingHostAssignments &&
                            client.HostDeviceId is not null &&
                            client.HostDeviceId != host.Id)
                        {
                            continue;
                        }

                        var changed = false;

                        if (client.HostDeviceId != host.Id)
                        {
                            client.HostDeviceId = host.Id;
                            changed = true;
                        }

                        var proxmoxGuestTypeId = GetProxmoxGuestTypeId(vm.VmType, proxmoxVmTypeId, proxmoxLxcTypeId);
                        if (profile.UpdateGuestClientType &&
                            proxmoxGuestTypeId is not null &&
                            client.ClientTypeId != proxmoxGuestTypeId.Value)
                        {
                            client.ClientTypeId = proxmoxGuestTypeId.Value;
                            changed = true;
                        }

                        if (changed)
                            changes++;
                    }
                }
                catch (Exception ex)
                {
                    configErrors++;
                    _logger.LogWarning(
                        ex,
                        "Proxmox profile '{Profile}' could not read config for {VmType}/{Vmid} on node '{Node}'.",
                        profile.Name,
                        vm.VmType,
                        vm.Vmid,
                        vm.Node);
                    continue;
                }
            }

            _logger.LogDebug(
                "Proxmox profile '{Profile}' scan completed. Hosts={Hosts} VMRefs={VmRefs} MatchedClients={Matched}.",
                profile.Name,
                hostByNode.Count,
                vmRefs.Count,
                matched);

            if (configErrors > 0)
            {
                _logger.LogWarning(
                    "Proxmox profile '{Profile}' had {ConfigErrors} VM config read error(s); mapping may be incomplete.",
                    profile.Name,
                    configErrors);
            }
        }

        if (changes > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Proxmox host mapping updated {Count} client host assignment(s).", changes);
        }

        return profiles.Min(p => NormalizeInterval(p.IntervalSeconds));
    }

    private static int NormalizeInterval(int seconds)
        => seconds <= 0 ? 300 : Math.Clamp(seconds, 30, 86400);

    private static string NormalizeNode(string? node)
        => string.IsNullOrWhiteSpace(node) ? "" : node.Trim().ToLowerInvariant();

    private static string NormalizeNodeShort(string? node)
    {
        var normalized = NormalizeNode(node);
        if (string.IsNullOrWhiteSpace(normalized))
            return normalized;

        var dot = normalized.IndexOf('.');
        return dot <= 0 ? normalized : normalized[..dot];
    }

    private static IEnumerable<string> ExpandNodeKeys(string? node)
    {
        var full = NormalizeNode(node);
        if (string.IsNullOrWhiteSpace(full))
            yield break;

        yield return full;

        var shortName = NormalizeNodeShort(full);
        if (!string.Equals(shortName, full, StringComparison.OrdinalIgnoreCase))
            yield return shortName;
    }

    private static bool TryResolveHost(
        IReadOnlyDictionary<string, HostRef> hostByNode,
        string vmNode,
        out HostRef host)
    {
        var full = NormalizeNode(vmNode);
        if (hostByNode.TryGetValue(full, out host))
            return true;

        var shortName = NormalizeNodeShort(full);
        if (hostByNode.TryGetValue(shortName, out host))
            return true;

        host = default!;
        return false;
    }

    private static IEnumerable<string> ExtractMacs(JsonElement config)
    {
        foreach (var prop in config.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
                continue;

            var text = prop.Value.GetString();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            foreach (Match match in MacRegex.Matches(text))
                yield return UnifiParsers.NormalizeMac(match.Value);
        }
    }

    private static bool TryGetDataArray(JsonElement root, out JsonElement data)
    {
        data = default;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (!root.TryGetProperty("data", out data))
            return false;

        return data.ValueKind == JsonValueKind.Array;
    }

    private static bool TryGetDataObject(JsonElement root, out JsonElement data)
    {
        data = default;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (!root.TryGetProperty("data", out data))
            return false;

        return data.ValueKind == JsonValueKind.Object;
    }

    private static string? ReadString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? ReadInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i))
            return i;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out i))
            return i;

        return null;
    }

    private async Task<List<(string Node, string VmType, int Vmid)>> DiscoverVmRefsAsync(
        ProxmoxApiClient proxmox,
        string baseUrl,
        string apiTokenId,
        string apiTokenSecret,
        CancellationToken ct)
    {
        var vmRefs = new List<(string Node, string VmType, int Vmid)>();

        // 1) filtered cluster resources.
        using (var resourcesDoc = await proxmox.GetVmResourcesAsync(baseUrl, apiTokenId, apiTokenSecret, ct))
        {
            if (TryGetDataArray(resourcesDoc.RootElement, out var resources))
                AddVmRefs(resources, vmRefs);
        }

        // 2) unfiltered cluster resources.
        if (vmRefs.Count == 0)
        {
            using var allDoc = await proxmox.GetClusterResourcesAsync(baseUrl, apiTokenId, apiTokenSecret, null, ct);
            if (TryGetDataArray(allDoc.RootElement, out var allResources))
                AddVmRefs(allResources, vmRefs);
        }

        // 3) node-level listing.
        if (vmRefs.Count == 0)
        {
            using var nodesDoc = await proxmox.GetNodesAsync(baseUrl, apiTokenId, apiTokenSecret, ct);
            if (TryGetDataArray(nodesDoc.RootElement, out var nodes))
            {
                foreach (var nodeItem in nodes.EnumerateArray())
                {
                    var node = ReadString(nodeItem, "node");
                    if (string.IsNullOrWhiteSpace(node))
                        continue;

                    using var qemuDoc = await proxmox.GetQemuVmsAsync(baseUrl, apiTokenId, apiTokenSecret, node, ct);
                    if (TryGetDataArray(qemuDoc.RootElement, out var qemus))
                        AddNodeVmRefs(node, "qemu", qemus, vmRefs);

                    using var lxcDoc = await proxmox.GetLxcContainersAsync(baseUrl, apiTokenId, apiTokenSecret, node, ct);
                    if (TryGetDataArray(lxcDoc.RootElement, out var lxcs))
                        AddNodeVmRefs(node, "lxc", lxcs, vmRefs);
                }
            }
        }

        return vmRefs
            .Distinct()
            .ToList();
    }

    private static void AddVmRefs(JsonElement rows, List<(string Node, string VmType, int Vmid)> target)
    {
        foreach (var item in rows.EnumerateArray())
        {
            var vmType = ReadString(item, "type");
            var node = ReadString(item, "node");
            var vmid = ReadInt(item, "vmid");

            if (string.IsNullOrWhiteSpace(vmType) ||
                string.IsNullOrWhiteSpace(node) ||
                vmid is null)
            {
                continue;
            }

            if (!vmType.Equals("qemu", StringComparison.OrdinalIgnoreCase) &&
                !vmType.Equals("lxc", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target.Add((node, vmType, vmid.Value));
        }
    }

    private static void AddNodeVmRefs(
        string node,
        string vmType,
        JsonElement rows,
        List<(string Node, string VmType, int Vmid)> target)
    {
        foreach (var item in rows.EnumerateArray())
        {
            var vmid = ReadInt(item, "vmid");
            if (vmid is null)
                continue;
            target.Add((node, vmType, vmid.Value));
        }
    }

    private static int? GetProxmoxGuestTypeId(
        string? vmType,
        int? proxmoxVmTypeId,
        int? proxmoxLxcTypeId)
    {
        if (string.Equals(vmType, "qemu", StringComparison.OrdinalIgnoreCase))
            return proxmoxVmTypeId;

        if (string.Equals(vmType, "lxc", StringComparison.OrdinalIgnoreCase))
            return proxmoxLxcTypeId;

        return null;
    }
}
