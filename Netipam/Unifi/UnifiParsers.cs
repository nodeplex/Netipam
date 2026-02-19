using System.Text.Json;

namespace Netipam.Unifi;

public static class UnifiParsers
{
    public sealed record UnifiNetwork(
        string? Name,
        string? Cidr,
        string? Purpose,
        int? VlanId,
        string? DhcpStart,
        string? DhcpEnd,
        string? Dns1,
        string? Dns2);

    // Includes upstream fields (for import + status updater)
    public sealed record UnifiClient(
        string Mac,
        string? Name,
        string? Hostname,
        string? IpAddress,
        string? Manufacturer,
        string? Model,
        string? OperatingSystem,
        bool IsOnline,
        DateTime? LastSeenUtc,
        string? ConnectionType,
        string? UpstreamDeviceName,
        string? UpstreamDeviceMac,
        string? UpstreamConnection,
        string? ConnectionDetail);

    // For the hosted-service "online status updater"
    public sealed record ActiveClientStatus(string? ConnectionType, string? ConnectionDetail);

    public sealed record UnifiInfraDevice(
        string Mac,
        string? Name,
        string? IpAddress,
        string? Model,
        string? Type,          // UniFi device type (uap/usw/ugw/...)
        string? Version,
        string? Serial,
        string? UplinkMac,
        int? UplinkPort,
        bool? IsUpgradable,
        string? UpgradeToVersion,
        bool IsOnline);

    public sealed record UnifiWanInterface(
        string? GatewayName,
        string? GatewayMac,
        string InterfaceName,
        bool? IsUp,
        string? IpAddress);

    /// <summary>
    /// Build a MAC -> name map from the UniFi devices response so we can resolve parent names.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildDeviceNameMap(JsonDocument doc)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!TryGetDataArray(doc, out var data))
            return map;

        foreach (var d in data.EnumerateArray())
        {
            var mac = GetStringStrict(d, "mac");
            if (string.IsNullOrWhiteSpace(mac))
                continue;

            var name =
                GetString(d, "name") ??
                GetString(d, "display_name") ??
                GetString(d, "adopted_name") ??
                mac;

            map[NormalizeMac(mac)] = name.Trim();
        }

        return map;
    }

    public static List<UnifiInfraDevice> ParseInfraDevices(JsonDocument doc)
    {
        var list = new List<UnifiInfraDevice>();
        if (!TryGetDataArray(doc, out var data))
            return list;

        foreach (var d in data.EnumerateArray())
        {
            var macRaw = GetString(d, "mac");
            if (string.IsNullOrWhiteSpace(macRaw))
                continue;

            var mac = NormalizeMac(macRaw);

            var name =
                GetString(d, "name") ??
                GetString(d, "display_name") ??
                GetString(d, "adopted_name");

            var ip =
                GetString(d, "ip") ??
                GetString(d, "ip_address");

            var model = GetString(d, "model");
            var type = GetString(d, "type");
            var version = GetString(d, "version");
            var serial = GetString(d, "serial");
            var isUpgradable = GetBool(d, "upgradable");
            var upgradeTo =
                GetString(d, "upgrade_to_firmware") ??
                GetString(d, "upgrade_to_version") ??
                GetString(d, "required_version");
            var isOnline =
                (GetBool(d, "is_connected") ?? GetBool(d, "connected")) ??
                ((GetInt(d, "state") ?? 0) == 1);

            // Uplink info
            string? uplinkMac = null;
            int? uplinkPort = null;
            if (d.TryGetProperty("uplink", out var uplink) && uplink.ValueKind == JsonValueKind.Object)
            {
                uplinkMac = GetStringStrict(uplink, "uplink_mac") ?? GetStringStrict(uplink, "remote_mac");
                uplinkPort = GetInt(uplink, "uplink_remote_port") ?? GetInt(uplink, "remote_port");
            }

            list.Add(new UnifiInfraDevice(
                mac,
                name,
                ip,
                model,
                type,
                version,
                serial,
                uplinkMac,
                uplinkPort,
                isUpgradable,
                upgradeTo,
                isOnline));
        }

        return list
            .OrderBy(x => x.Name ?? x.Mac, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<UnifiWanInterface> ParseWanInterfaces(JsonDocument doc)
    {
        var list = new List<UnifiWanInterface>();
        if (!TryGetDataArray(doc, out var data))
            return list;

        foreach (var d in data.EnumerateArray())
        {
            var type = GetString(d, "type");
            var model = GetString(d, "model");
            if (!IsGatewayDevice(type, model))
                continue;

            var mac = GetString(d, "mac");
            var name =
                GetString(d, "name") ??
                GetString(d, "display_name") ??
                GetString(d, "adopted_name");

            AddWanFromObject(list, d, name, mac, "wan1");
            AddWanFromObject(list, d, name, mac, "wan2");
            AddWanFromObject(list, d, name, mac, "wan");

            if (!d.TryGetProperty("uplink", out var uplink) || uplink.ValueKind != JsonValueKind.Object)
                continue;

            var uplinkIp = GetString(uplink, "ip") ?? GetString(uplink, "ip_address");
            var uplinkUp = GetBool(uplink, "up") ?? GetBool(uplink, "is_up");
            if (!string.IsNullOrWhiteSpace(uplinkIp) || uplinkUp is not null)
            {
        list.Add(new UnifiWanInterface(name, NormalizeMacOrNull(mac), "uplink", uplinkUp, uplinkIp));
            }
        }

        return list
            .OrderBy(x => x.GatewayName ?? x.GatewayMac ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<UnifiNetwork> ParseNetworks(JsonDocument doc)
    {
        var list = new List<UnifiNetwork>();
        if (!TryGetDataArray(doc, out var data))
            return list;

        foreach (var n in data.EnumerateArray())
        {
            var name = GetString(n, "name");
            var purpose = GetString(n, "purpose");

            var cidr = GetString(n, "ip_subnet");

            int? vlan =
                GetInt(n, "vlan") ??
                GetInt(n, "vlan_id") ??
                GetInt(n, "vlan_enabled");

            string? dhcpStart = null;
            string? dhcpEnd = null;
            string? dns1 = null;
            string? dns2 = null;

            var dhcpEnabled = GetBool(n, "dhcpd_enabled") == true;
            if (dhcpEnabled)
            {
                dhcpStart = GetString(n, "dhcpd_start");
                dhcpEnd = GetString(n, "dhcpd_stop") ?? GetString(n, "dhcpd_end");
            }

            var dnsEnabled = GetBool(n, "dhcpd_dns_enabled") == true;
            if (dnsEnabled)
            {
                dns1 = GetString(n, "dhcpd_dns_1");
                dns2 = GetString(n, "dhcpd_dns_2");
            }
            else
            {
                dns1 = "Auto";
                dns2 = null;
            }

            if (!string.IsNullOrWhiteSpace(cidr))
            {
                vlan = NormalizeDefaultVlan(vlan, name, purpose);
                list.Add(new UnifiNetwork(name, cidr, purpose, vlan, dhcpStart, dhcpEnd, dns1, dns2));
            }
        }

        return list;
    }

    private static int? NormalizeDefaultVlan(int? vlan, string? name, string? purpose)
    {
        if (vlan.HasValue)
            return vlan;

        if (!string.IsNullOrWhiteSpace(purpose) &&
            purpose.Equals("corporate", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var trimmed = name.Trim();
            if (trimmed.Equals("LAN", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Default LAN", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
        }

        return null;
    }

    /// <summary>
    /// Merge active + known client lists. Active brings connection/upstream info.
    /// </summary>
    public static List<UnifiClient> MergeClients(
        JsonDocument activeDoc,
        JsonDocument knownDoc,
        IReadOnlyDictionary<string, string>? deviceNameMap = null)
    {
        var byMac = new Dictionary<string, UnifiClient>(StringComparer.OrdinalIgnoreCase);

        // Known clients first
        foreach (var c in EnumerateData(knownDoc))
        {
            var macRaw = GetString(c, "mac");
            if (string.IsNullOrWhiteSpace(macRaw))
                continue;

            var mac = NormalizeMac(macRaw);

            var name =
                GetString(c, "name") ??
                GetString(c, "display_name") ??
                GetString(c, "device_name");

            var hostname =
                GetString(c, "hostname") ??
                GetString(c, "host");

            var fixedIp = GetString(c, "fixed_ip");
            var ip = GetString(c, "ip");
            var lastIp = GetString(c, "last_ip");
            var useFixed =
                GetBool(c, "use_fixedip") ??
                GetBool(c, "use_fixed_ip");

            var resolvedIp = useFixed == true && !string.IsNullOrWhiteSpace(fixedIp)
                ? fixedIp
                : (!string.IsNullOrWhiteSpace(ip)
                    ? ip
                    : (!string.IsNullOrWhiteSpace(lastIp) ? lastIp : fixedIp));
            var lastSeenUtc = ParseUnixTimestampToUtc(c, "last_seen");

            var (mfg, model, os) = ParseDeviceHints(c);

            byMac[mac] = new UnifiClient(
                mac,
                name,
                hostname,
                resolvedIp,
                mfg,
                model,
                os,
                IsOnline: false,
                LastSeenUtc: lastSeenUtc,
                ConnectionType: null,
                UpstreamDeviceName: null,
                UpstreamDeviceMac: null,
                UpstreamConnection: null,
                ConnectionDetail: null);
        }

        // Overlay active clients
        foreach (var c in EnumerateData(activeDoc))
        {
            var macRaw = GetString(c, "mac");
            if (string.IsNullOrWhiteSpace(macRaw))
                continue;

            var mac = NormalizeMac(macRaw);

            var activeName =
                GetString(c, "name") ??
                GetString(c, "display_name");

            var hostname =
                GetString(c, "hostname") ??
                GetString(c, "host");

            var ip = GetString(c, "ip");

            var (mfg, model, os) = ParseDeviceHints(c);

            var (connType, upstreamName, upstreamMac, upstreamConn, connDetail) = ParseConnectionFromActive(c, deviceNameMap);

            if (byMac.TryGetValue(mac, out var existing))
            {
                var mergedName = !string.IsNullOrWhiteSpace(existing.Name) ? existing.Name : activeName;
                var mergedHostname = !string.IsNullOrWhiteSpace(hostname) ? hostname : existing.Hostname;
                var mergedIp = !string.IsNullOrWhiteSpace(ip) ? ip : existing.IpAddress;

                var mergedMfg = !string.IsNullOrWhiteSpace(mfg) ? mfg : existing.Manufacturer;
                var mergedModel = !string.IsNullOrWhiteSpace(model) ? model : existing.Model;
                var mergedOs = !string.IsNullOrWhiteSpace(os) ? os : existing.OperatingSystem;

                byMac[mac] = existing with
                {
                    Name = mergedName,
                    Hostname = mergedHostname,
                    IpAddress = mergedIp,
                    Manufacturer = mergedMfg,
                    Model = mergedModel,
                    OperatingSystem = mergedOs,
                    IsOnline = true,
                    LastSeenUtc = DateTime.UtcNow,

                    ConnectionType = !string.IsNullOrWhiteSpace(connType) ? connType : existing.ConnectionType,
                    UpstreamDeviceName = !string.IsNullOrWhiteSpace(upstreamName) ? upstreamName : existing.UpstreamDeviceName,
                    UpstreamDeviceMac = !string.IsNullOrWhiteSpace(upstreamMac) ? upstreamMac : existing.UpstreamDeviceMac,
                    UpstreamConnection = !string.IsNullOrWhiteSpace(upstreamConn) ? upstreamConn : existing.UpstreamConnection,
                    ConnectionDetail = !string.IsNullOrWhiteSpace(connDetail) ? connDetail : existing.ConnectionDetail
                };
            }
            else
            {
                byMac[mac] = new UnifiClient(
                    mac,
                    activeName,
                    hostname,
                    ip,
                    mfg,
                    model,
                    os,
                    IsOnline: true,
                    LastSeenUtc: DateTime.UtcNow,
                    ConnectionType: connType,
                    UpstreamDeviceName: upstreamName,
                    UpstreamDeviceMac: upstreamMac,
                    UpstreamConnection: upstreamConn,
                    ConnectionDetail: connDetail);
            }
        }

        return byMac.Values
            .OrderBy(x => x.Name ?? x.Hostname ?? x.Mac, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Parse ONLY active-client info into a MAC->status map (for background updater).
    /// </summary>
    public static Dictionary<string, ActiveClientStatus> ParseActiveStatus(JsonDocument activeDoc)
    {
        var map = new Dictionary<string, ActiveClientStatus>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in EnumerateData(activeDoc))
        {
            var macRaw = GetString(c, "mac");
            if (string.IsNullOrWhiteSpace(macRaw))
                continue;

            var mac = NormalizeMac(macRaw);

            var (connType, _, _, _, connDetail) = ParseConnectionFromActive(c, null);
            map[mac] = new ActiveClientStatus(connType, connDetail);
        }

        return map;
    }

    // ---- connection parsing ----
    private static (string? ConnectionType, string? UpstreamDeviceName, string? UpstreamDeviceMac, string? UpstreamConnection, string? ConnectionDetail) ParseConnectionFromActive(
        JsonElement c,
        IReadOnlyDictionary<string, string>? deviceNameMap)
    {
        var isWired = GetBool(c, "is_wired") == true;

        var uplinkName = GetString(c, "last_uplink_name");

        var swMac = GetStringStrict(c, "sw_mac");
        var port = GetInt(c, "sw_port") ?? GetInt(c, "last_uplink_remote_port");

        var apMac = GetStringStrict(c, "ap_mac");
        var ssid = GetStringStrict(c, "essid");

        var radioTable = GetInt(c, "radio_table");
        var lastRadio = GetString(c, "last_radio");

        string? connType = isWired ? "Wired" : "WiFi";
        string? upstreamName = null;
        string? upstreamMac = null;
        string? upstreamConn = null;
        string? where = null;

        if (isWired)
        {
            upstreamMac = swMac;
            upstreamName = ResolveName(swMac, uplinkName, deviceNameMap);
            upstreamConn = port is not null ? $"port {port.Value}" : null;
            where = BuildWhere(upstreamName, upstreamConn);
        }
        else
        {
            upstreamMac = apMac;
            upstreamName = ResolveName(apMac, uplinkName, deviceNameMap) ?? "AP";
            var band = MapBand(radioTable, lastRadio);
            upstreamConn = MakeWifiConn(ssid, band);
            where = BuildWhere(upstreamName, upstreamConn);
        }

        return (connType, upstreamName, upstreamMac, upstreamConn, where);

        static string? ResolveName(string? mac, string? fallback, IReadOnlyDictionary<string, string>? deviceNameMap)
        {
            if (!string.IsNullOrWhiteSpace(mac) && deviceNameMap != null &&
                deviceNameMap.TryGetValue(NormalizeMac(mac), out var mapped))
            {
                return mapped;
            }

            return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
        }

        static string? BuildWhere(string? upstreamName, string? connPart)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(upstreamName))
                parts.Add(upstreamName.Trim());
            if (!string.IsNullOrWhiteSpace(connPart))
                parts.Add(connPart);
            return parts.Count == 0 ? null : string.Join(" | ", parts);
        }

        static string? MakeWifiConn(string? ssid, string? band)
        {
            if (string.IsNullOrWhiteSpace(ssid) && string.IsNullOrWhiteSpace(band))
                return null;

            if (string.IsNullOrWhiteSpace(ssid))
                return band;
            if (string.IsNullOrWhiteSpace(band))
                return ssid.Trim();
            return $"{ssid.Trim()} @ {band}";
        }
    }

    /// <summary>
    /// Map UniFi radio hints to a display band.
    /// </summary>
    private static string? MapBand(int? radioTable, string? lastRadio)
    {
        // Common UniFi radio_table values: 0=2.4GHz, 1=5GHz, 2=6GHz
        if (radioTable is 0) return "2.4 GHz";
        if (radioTable is 1) return "5 GHz";
        if (radioTable is 2) return "6 GHz";

        var radio = (lastRadio ?? "").ToLowerInvariant();
        if (radio.Contains("6")) return "6 GHz";
        if (radio.Contains("5") || radio.Contains("na")) return "5 GHz";
        if (radio.Contains("2") || radio.Contains("ng")) return "2.4 GHz";

        return null;
    }

    // ---------------- helpers ----------------
    private static IEnumerable<JsonElement> EnumerateData(JsonDocument doc)
    {
        if (!TryGetDataArray(doc, out var data))
            yield break;

        foreach (var e in data.EnumerateArray())
            yield return e;
    }

    private static bool TryGetDataArray(JsonDocument doc, out JsonElement data)
    {
        data = default;
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!doc.RootElement.TryGetProperty("data", out data))
            return false;

        return data.ValueKind == JsonValueKind.Array;
    }

    private static string? GetString(JsonElement obj, string prop)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

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

    private static string? GetStringStrict(JsonElement obj, string prop)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        if (!obj.TryGetProperty(prop, out var p))
            return null;

        return p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }

    private static int? GetInt(JsonElement obj, string prop)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        if (!obj.TryGetProperty(prop, out var p))
            return null;

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
            return i;

        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var j))
            return j;

        return null;
    }

    private static bool? GetBool(JsonElement obj, string prop)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        if (!obj.TryGetProperty(prop, out var p))
            return null;

        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(p.GetString(), out var b) ? b : null,
            JsonValueKind.Number => p.TryGetInt32(out var i) ? (i != 0) : null,
            _ => null
        };
    }

    private static DateTime? ParseUnixTimestampToUtc(JsonElement obj, string prop)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        if (!obj.TryGetProperty(prop, out var p))
            return null;

        long? seconds = null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n))
            seconds = n;
        else if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var s))
            seconds = s;

        if (seconds is null || seconds <= 0)
            return null;

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    private static (string? Manufacturer, string? Model, string? Os) ParseDeviceHints(JsonElement obj)
    {
        var vendor = GetStringStrict(obj, "oui") ?? GetStringStrict(obj, "vendor");
        var model = GetStringStrict(obj, "dev_id") ?? GetStringStrict(obj, "model");
        var os = GetStringStrict(obj, "os_name") ?? GetStringStrict(obj, "os_class") ?? GetStringStrict(obj, "os");

        return (vendor, model, os);
    }

    private static void AddWanFromObject(
        List<UnifiWanInterface> list,
        JsonElement device,
        string? gatewayName,
        string? gatewayMac,
        string interfaceName)
    {
        if (!device.TryGetProperty(interfaceName, out var wan) || wan.ValueKind != JsonValueKind.Object)
            return;

        var ip = GetString(wan, "ip") ?? GetString(wan, "ip_address") ?? GetString(wan, "ipaddr");
        var isUp =
            GetBool(wan, "up") ??
            GetBool(wan, "is_up") ??
            GetBool(wan, "link_up");

        var status = GetString(wan, "status");
        if (isUp is null && !string.IsNullOrWhiteSpace(status))
            isUp = status.Equals("up", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(ip) && isUp is null)
            return;

        list.Add(new UnifiWanInterface(
            gatewayName,
            NormalizeMacOrNull(gatewayMac),
            interfaceName,
            isUp,
            ip));
    }

    private static bool IsGatewayDevice(string? type, string? model)
    {
        var t = (type ?? "").ToLowerInvariant();
        var m = (model ?? "").ToLowerInvariant();

        if (t.Contains("ugw") || t.Contains("usg") || t.Contains("udm") || t.Contains("uxg"))
            return true;

        return m.Contains("ugw") || m.Contains("usg") || m.Contains("udm") || m.Contains("uxg");
    }

    private static string? NormalizeMacOrNull(string? mac)
        => string.IsNullOrWhiteSpace(mac) ? null : NormalizeMac(mac);

    public static string NormalizeMac(string mac)
        => mac.Trim().ToLowerInvariant().Replace("-", ":").Replace(".", ":");
}
