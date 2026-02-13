using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Netipam.Data;
using Netipam.Helpers;
using Netipam.Services; // for IpCidr
using System.Text.RegularExpressions;

namespace Netipam.Services;

public sealed class ExcelExportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private static readonly Regex FirstIPv4Regex =
        new(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b",
            RegexOptions.Compiled);

    public ExcelExportService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // ---------------------------
    // Public exports
    // ---------------------------

    public Task<byte[]> ExportClientsAsync(CancellationToken ct = default)
        => BuildWorkbookBytesAsync(async (wb, db) =>
        {
            var clients = await QueryClientsAsync(db, ct);
            AddClientsOrDevicesSheet(wb, "Clients", clients, includeHost: true);
        }, ct);

    public Task<byte[]> ExportDevicesAsync(CancellationToken ct = default)
        => BuildWorkbookBytesAsync(async (wb, db) =>
        {
            var devices = await QueryDevicesOnlyAsync(db, ct);
            AddClientsOrDevicesSheet(wb, "Devices", devices, includeHost: false);
        }, ct);

    public Task<byte[]> ExportSubnetsAsync(CancellationToken ct = default)
        => BuildWorkbookBytesAsync(async (wb, db) =>
        {
            var subnets = await QuerySubnetsAsync(db, ct);
            var summaries = await BuildSubnetSummariesAsync(db, subnets, ct);
            AddSubnetsSheet(wb, "Subnets", subnets, summaries);
        }, ct);

    // This matches SubnetIps.razor behavior (derive usage from Device.IpAddress)
    public Task<byte[]> ExportSubnetIpsForSubnetAsync(int subnetId, CancellationToken ct = default)
        => BuildWorkbookBytesAsync(async (wb, db) =>
        {
            var subnet = await db.Subnets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subnetId, ct);
            if (subnet is null || string.IsNullOrWhiteSpace(subnet.Cidr))
            {
                var ws = wb.AddWorksheet("SubnetIps");
                ws.Cell(1, 1).Value = "Subnet not found or CIDR blank.";
                return;
            }

            var rows = await BuildSubnetIpRowsAsync(db, subnet, ct);
            AddSubnetIpsSheet(wb, SafeSheetName($"Subnet {subnet.Id}"), subnet, rows);
        }, ct);

    public Task<byte[]> ExportAllAsync(bool includePerSubnetTabs, CancellationToken ct = default)
        => BuildWorkbookBytesAsync(async (wb, db) =>
        {
            var clients = await QueryClientsAsync(db, ct);
            var devices = await QueryDevicesOnlyAsync(db, ct);
            var subnets = await QuerySubnetsAsync(db, ct);
            var summaries = await BuildSubnetSummariesAsync(db, subnets, ct);

            AddClientsOrDevicesSheet(wb, "Clients", clients, includeHost: true);
            AddClientsOrDevicesSheet(wb, "Devices", devices, includeHost: false);
            AddSubnetsSheet(wb, "Subnets", subnets, summaries);

            // A single SubnetIps summary tab (optional but handy)
            // We’ll include only “Used” IPs across all subnets to keep it reasonable.
            var allUsed = new List<(Subnet subnet, SubnetIpRow row)>();
            foreach (var s in subnets)
            {
                if (string.IsNullOrWhiteSpace(s.Cidr)) continue;
                if (!IpCidr.TryParseIPv4Cidr(s.Cidr, out var info, out _)) continue;
                if (info.UsableAddresses > 8192) continue; // mirror your safety limit idea

                var rows = await BuildSubnetIpRowsAsync(db, s, ct);
                allUsed.AddRange(rows.Where(r => r.IsUsed).Select(r => (s, r)));
            }
            AddSubnetIpsSummarySheet(wb, "SubnetIps (Used)", allUsed);

            if (includePerSubnetTabs)
            {
                foreach (var s in subnets.OrderBy(s => s.Name))
                {
                    if (string.IsNullOrWhiteSpace(s.Cidr)) continue;

                    if (!IpCidr.TryParseIPv4Cidr(s.Cidr, out var info, out _))
                        continue;

                    if (info.UsableAddresses > 8192)
                        continue;

                    var rows = await BuildSubnetIpRowsAsync(db, s, ct);
                    var sheetName = SafeSheetName($"{s.Name} ({s.Cidr})");
                    AddSubnetIpsSheet(wb, sheetName, s, rows);
                }
            }
        }, ct);

    public Task<byte[]> ExportTopologyAsync(CancellationToken ct = default)
        => BuildWorkbookBytesAsync(async (wb, db) =>
        {
            var devices = await QueryTopologyDevicesAsync(db, ct);
            AddTopologySheets(wb, "Topology (Tree)", "Topology (Links)", devices);
        }, ct);

    public Task<byte[]> ExportUpdaterLogsAsync(CancellationToken ct = default)
        => BuildWorkbookBytesAsync(async (wb, db) =>
        {
            var runs = await db.UpdaterRunLogs
                .AsNoTracking()
                .OrderByDescending(r => r.StartedAtUtc)
                .ToListAsync(ct);

            var changes = await db.UpdaterChangeLogs
                .AsNoTracking()
                .OrderByDescending(c => c.RunId)
                .ThenBy(c => c.Id)
                .ToListAsync(ct);

            var runStartedById = runs.ToDictionary(r => r.Id, r => r.StartedAtUtc);
            AddUpdaterLogsSheet(wb, "Updater Runs", runs);
            AddUpdaterChangesSheet(wb, "Updater Changes", changes, runStartedById);
        }, ct);

    public Task<byte[]> ExportIpHistoryAsync(CancellationToken ct = default)
        => BuildWorkbookBytesAsync(async (wb, db) =>
        {
            var history = await db.DeviceIpHistories
                .AsNoTracking()
                .Include(h => h.Device)
                    .ThenInclude(d => d!.ClientType)
                .Include(h => h.Device)
                    .ThenInclude(d => d!.Subnet)
                .OrderByDescending(h => h.FirstSeenUtc)
                .ToListAsync(ct);

            AddIpHistorySheet(wb, "IP History", history);
        }, ct);

    // ---------------------------
    // Queries
    // ---------------------------

    private static Task<List<Device>> QueryClientsAsync(AppDbContext db, CancellationToken ct)
        => db.Devices
            .AsNoTracking()
            .Include(d => d.Subnet)
            .Include(d => d.ClientType)
            .Include(d => d.HostDevice) // ✅ include host for Clients export
            .Include(d => d.LocationRef)
            .Include(d => d.RackRef)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

    private static Task<List<Device>> QueryDevicesOnlyAsync(AppDbContext db, CancellationToken ct)
        => db.Devices
            .AsNoTracking()
            .Include(d => d.Subnet)
            .Include(d => d.ClientType)
            .Include(d => d.LocationRef)
            .Include(d => d.RackRef)
            .Where(d => d.ClientType != null && d.ClientType.IsDevice)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

    private static Task<List<Device>> QueryTopologyDevicesAsync(AppDbContext db, CancellationToken ct)
        => db.Devices
            .AsNoTracking()
            .Include(d => d.Subnet)
            .Include(d => d.ClientType)
            .Include(d => d.HostDevice)
            .Include(d => d.LocationRef)
            .Include(d => d.RackRef)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

    private static Task<List<Subnet>> QuerySubnetsAsync(AppDbContext db, CancellationToken ct)
        => db.Subnets
            .AsNoTracking()
            .OrderBy(s => s.VlanId ?? int.MaxValue)
            .ThenBy(s => s.Cidr)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

    // ---------------------------
    // Subnet IP rows (match SubnetIps.razor)
    // ---------------------------

    private static async Task<List<SubnetIpRow>> BuildSubnetIpRowsAsync(AppDbContext db, Subnet subnet, CancellationToken ct)
    {
        if (!IpCidr.TryParseIPv4Cidr(subnet.Cidr, out var info, out _))
            return new();

        var reservations = await db.IpAssignments
            .AsNoTracking()
            .Where(a => a.SubnetId == subnet.Id)
            .ToListAsync(ct);

        var reservationsByIp = new Dictionary<uint, IpAssignment>();
        foreach (var r in reservations)
        {
            var extracted = ExtractFirstIPv4(r.IpAddress);
            if (extracted is null)
                continue;

            if (!IpCidr.TryParseIPv4(extracted, out var ipUInt, out _))
                continue;

            if (ipUInt < info.NetworkUInt || ipUInt > info.BroadcastUInt)
                continue;

            if (!reservationsByIp.ContainsKey(ipUInt))
                reservationsByIp[ipUInt] = r;
        }

        // Same input source as your page: devices with a non-empty IpAddress string
        var devicesWithIp = await db.Devices
            .AsNoTracking()
            .Where(d => !string.IsNullOrWhiteSpace(d.IpAddress))
            .ToListAsync(ct);

        // Build map ipUInt -> devices
        var byIp = new Dictionary<uint, List<Device>>();

        foreach (var d in devicesWithIp)
        {
            var ipText = (d.IpAddress ?? "").Trim();

            // If IpAddress can contain extra text (like "192.168.1.10 (DHCP)"),
            // swap to the same regex approach used in SubnetIps.razor.
            if (!IpCidr.TryParseIPv4(ipText, out var ipUInt, out _))
                continue;

            if (ipUInt < info.NetworkUInt || ipUInt > info.BroadcastUInt)
                continue;

            if (!byIp.TryGetValue(ipUInt, out var list))
            {
                list = new List<Device>();
                byIp[ipUInt] = list;
            }
            list.Add(d);
        }

        var usableIps = GetUsableIpUInts(info);

        var rows = new List<SubnetIpRow>(usableIps.Count);
        foreach (var u in usableIps)
        {
            byIp.TryGetValue(u, out var devs);
            devs ??= new List<Device>();

            rows.Add(new SubnetIpRow
            {
                IpUInt = u,
                Ip = IpCidr.UIntToIPv4(u),
                Devices = devs,
                Reservation = reservationsByIp.TryGetValue(u, out var resv) ? resv : null
            });
        }

        return rows;
    }

    private static List<uint> GetUsableIpUInts(IpCidr.CidrInfo info)
    {
        if (info.PrefixLength == 32)
            return new List<uint> { info.NetworkUInt };

        if (info.PrefixLength == 31)
            return new List<uint> { info.NetworkUInt, info.BroadcastUInt };

        if (string.IsNullOrWhiteSpace(info.FirstUsable) || string.IsNullOrWhiteSpace(info.LastUsable))
            return new List<uint>();

        if (!IpCidr.TryParseIPv4(info.FirstUsable, out var start, out _))
            return new List<uint>();

        if (!IpCidr.TryParseIPv4(info.LastUsable, out var end, out _))
            return new List<uint>();

        if (end < start)
            return new List<uint>();

        var list = new List<uint>();
        for (uint ip = start; ip <= end; ip++)
        {
            list.Add(ip);
            if (ip == uint.MaxValue) break;
        }
        return list;
    }

    private static bool IsRowOnline(SubnetIpRow r)
        => r.IsUsed && r.Devices.Any(d => d.IsOnline);

    private static IpDisplayStatus GetRowStatus(Subnet subnet, SubnetIpRow row)
    {
        if (row.IsUsed)
            return IsInDhcpRange(subnet, row) ? IpDisplayStatus.AssignedDhcp : IpDisplayStatus.AssignedStatic;

        if (row.Reservation is not null)
        {
            return row.Reservation.Status switch
            {
                IpReservationStatus.Reserved => IpDisplayStatus.Reserved,
                IpReservationStatus.Dhcp => IpDisplayStatus.Dhcp,
                IpReservationStatus.Deprecated => IpDisplayStatus.Deprecated,
                _ => IpDisplayStatus.Available
            };
        }

        var start = subnet.DhcpRangeStart;
        var end = subnet.DhcpRangeEnd;
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            return IpDisplayStatus.Available;

        if (!IpCidr.TryParseIPv4(start, out var startVal, out _))
            return IpDisplayStatus.Available;
        if (!IpCidr.TryParseIPv4(end, out var endVal, out _))
            return IpDisplayStatus.Available;

        var min = Math.Min(startVal, endVal);
        var max = Math.Max(startVal, endVal);
        return row.IpUInt >= min && row.IpUInt <= max ? IpDisplayStatus.Dhcp : IpDisplayStatus.Available;
    }

    private static bool IsInDhcpRange(Subnet subnet, SubnetIpRow row)
    {
        var start = subnet.DhcpRangeStart;
        var end = subnet.DhcpRangeEnd;
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            return false;

        if (!IpCidr.TryParseIPv4(start, out var startVal, out _))
            return false;
        if (!IpCidr.TryParseIPv4(end, out var endVal, out _))
            return false;

        var min = Math.Min(startVal, endVal);
        var max = Math.Max(startVal, endVal);
        return row.IpUInt >= min && row.IpUInt <= max;
    }

    private static string FormatIpStatus(Subnet subnet, SubnetIpRow row)
        => GetRowStatus(subnet, row) switch
        {
            IpDisplayStatus.AssignedDhcp => "Assigned (DHCP)",
            IpDisplayStatus.AssignedStatic => "Assigned (Static)",
            IpDisplayStatus.Reserved => "Reserved",
            IpDisplayStatus.Deprecated => "Deprecated",
            IpDisplayStatus.Dhcp => "DHCP",
            _ => "Available"
        };

    // ---------------------------
    // Subnet summary (match Subnets.razor columns)
    // ---------------------------

    private static async Task<Dictionary<int, SubnetSummary>> BuildSubnetSummariesAsync(
        AppDbContext db,
        List<Subnet> subnets,
        CancellationToken ct)
    {
        var summaries = new Dictionary<int, SubnetSummary>();

        var subnetInfos = new Dictionary<int, IpCidr.CidrInfo>();
        foreach (var s in subnets)
        {
            if (string.IsNullOrWhiteSpace(s.Cidr))
                continue;

            if (!IpCidr.TryParseIPv4Cidr(s.Cidr.Trim(), out var info, out _))
                continue;

            subnetInfos[s.Id] = info;
        }

        var devices = await db.Devices
            .AsNoTracking()
            .Include(d => d.ClientType)
            .Select(d => new DeviceLite(
                d.SubnetId,
                d.IpAddress,
                d.IsOnline,
                d.ClientType != null && d.ClientType.IsDevice,
                d.IgnoreOffline,
                d.IsStatusTracked))
            .ToListAsync(ct);

        var assignments = await db.IpAssignments
            .AsNoTracking()
            .ToListAsync(ct);

        var usedIpBySubnet = new Dictionary<int, HashSet<uint>>();
        var reservedIpBySubnet = new Dictionary<int, HashSet<uint>>();
        foreach (var subnetId in subnetInfos.Keys)
        {
            usedIpBySubnet[subnetId] = new HashSet<uint>();
            reservedIpBySubnet[subnetId] = new HashSet<uint>();
        }

        foreach (var d in devices)
        {
            if (d.SubnetId is null)
                continue;

            if (!subnetInfos.TryGetValue(d.SubnetId.Value, out var info))
                continue;

            var extracted = ExtractFirstIPv4(d.IpAddress);
            if (extracted is null)
                continue;

            if (!IpCidr.TryParseIPv4(extracted, out var ipUInt, out _))
                continue;

            if (ipUInt < info.NetworkUInt || ipUInt > info.BroadcastUInt)
                continue;

            usedIpBySubnet[d.SubnetId.Value].Add(ipUInt);
        }

        foreach (var a in assignments)
        {
            if (!subnetInfos.TryGetValue(a.SubnetId, out var info))
                continue;

            var extracted = ExtractFirstIPv4(a.IpAddress);
            if (extracted is null)
                continue;

            if (!IpCidr.TryParseIPv4(extracted, out var ipUInt, out _))
                continue;

            if (ipUInt < info.NetworkUInt || ipUInt > info.BroadcastUInt)
                continue;

            if (!reservedIpBySubnet.TryGetValue(a.SubnetId, out var set))
                continue;

            set.Add(ipUInt);
        }

        var clientCounts = devices
            .Where(d => d.SubnetId != null && !d.IsDevice)
            .GroupBy(d => d.SubnetId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var deviceCounts = devices
            .Where(d => d.SubnetId != null && d.IsDevice)
            .GroupBy(d => d.SubnetId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var offlineCounts = devices
            .Where(d => d.SubnetId != null && d.IsStatusTracked && !d.IsOnline && !d.IgnoreOffline)
            .GroupBy(d => d.SubnetId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var s in subnets)
        {
            summaries[s.Id] = new SubnetSummary(
                UsableRange: FormatUsableRange(s.Cidr),
                UsableCount: FormatUsableCount(s.Cidr),
                UsedIps: GetCount(usedIpBySubnet, s.Id),
                Clients: GetCount(clientCounts, s.Id),
                Devices: GetCount(deviceCounts, s.Id),
                Offline: GetCount(offlineCounts, s.Id),
                DhcpRange: FormatDhcpRange(s),
                Vlan: FormatVlan(s.VlanId),
                Dns: FormatDns(s),
                NextFreeIps: FormatNextFreeIps(
                    s.Id,
                    s.DhcpRangeStart,
                    s.DhcpRangeEnd,
                    subnetInfos,
                    usedIpBySubnet,
                    reservedIpBySubnet));
        }

        return summaries;
    }

    // ---------------------------
    // Sheets (match your visible columns)
    // ---------------------------

    private static void AddClientsOrDevicesSheet(XLWorkbook wb, string sheetName, List<Device> rows, bool includeHost)
    {
        var ws = wb.AddWorksheet(SafeSheetName(sheetName));

        // Clients gets Host, Devices does not.
        var headers = includeHost
            ? new[]
            {
                "Name", "Type", "Hostname", "IP", "Subnet", "MAC", "Conn", "Upstream", "Host",
                "Location", "Rack", "Manufacturer", "Model", "OS", "Status", "Last Online", "AccessLink"
            }
            : new[]
            {
                "Name", "Type", "Hostname", "IP", "Subnet", "MAC", "Conn", "Upstream",
                "Location", "Rack", "Asset #", "Manufacturer", "Model", "OS",
                "Source", "Used/New", "Acquire Date", "Status", "Last Online", "AccessLink"
            };

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed().SetAutoFilter();

        int r = 2;
        foreach (var d in rows)
        {
            int c = 1;

            ws.Cell(r, c++).Value = d.Name;
            ws.Cell(r, c++).Value = d.ClientType?.Name;

            ws.Cell(r, c++).Value = d.Hostname;
            ws.Cell(r, c++).Value = d.IpAddress;
            ws.Cell(r, c++).Value = d.Subnet?.Name;
            ws.Cell(r, c++).Value = d.MacAddress;
            ws.Cell(r, c++).Value = DisplayFormat.GetConnTooltip(d);
            ws.Cell(r, c++).Value = FormatUpstream(d);
            if (includeHost)
                ws.Cell(r, c++).Value = d.HostDevice?.Name ?? "";

            ws.Cell(r, c++).Value = d.LocationRef?.Name ?? d.Location;
            ws.Cell(r, c++).Value = FormatRack(d);
            if (!includeHost)
                ws.Cell(r, c++).Value = d.AssetNumber;

            ws.Cell(r, c++).Value = d.Manufacturer;
            ws.Cell(r, c++).Value = d.Model;
            ws.Cell(r, c++).Value = d.OperatingSystem;

            if (!includeHost)
            {
                ws.Cell(r, c++).Value = d.Source;
                ws.Cell(r, c++).Value = d.UsedNew;
                ws.Cell(r, c++).Value = d.SourceDate?.ToString("MMM-yyyy");
                ws.Cell(r, c++).Value = d.IsOnline ? "Online" : "Offline";
                ws.Cell(r, c++).Value = d.LastOnlineAt;
            }
            else
            {
                ws.Cell(r, c++).Value = d.IsOnline ? "Online" : "Offline";
                ws.Cell(r, c++).Value = d.LastOnlineAt;
            }
            ws.Cell(r, c++).Value = d.AccessLink;

            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddSubnetsSheet(XLWorkbook wb, string sheetName, List<Subnet> rows, IReadOnlyDictionary<int, SubnetSummary> summaries)
    {
        var ws = wb.AddWorksheet(SafeSheetName(sheetName));

        var headers = new[]
        {
            "Name", "CIDR", "Usable Range", "Usable", "Used IPs", "Clients", "Devices", "Offline",
            "DHCP Range", "VLAN", "DNS", "Next Available IPs", "Description"
        };

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed().SetAutoFilter();

        int r = 2;
        foreach (var s in rows)
        {
            summaries.TryGetValue(s.Id, out var summary);
            summary ??= SubnetSummary.Empty;

            int c = 1;
            ws.Cell(r, c++).Value = s.Name;
            ws.Cell(r, c++).Value = s.Cidr;
            ws.Cell(r, c++).Value = summary.UsableRange;
            ws.Cell(r, c++).Value = summary.UsableCount;
            ws.Cell(r, c++).Value = summary.UsedIps;
            ws.Cell(r, c++).Value = summary.Clients;
            ws.Cell(r, c++).Value = summary.Devices;
            ws.Cell(r, c++).Value = summary.Offline;
            ws.Cell(r, c++).Value = summary.DhcpRange;
            ws.Cell(r, c++).Value = summary.Vlan;
            ws.Cell(r, c++).Value = summary.Dns;
            ws.Cell(r, c++).Value = summary.NextFreeIps;
            ws.Cell(r, c++).Value = string.IsNullOrWhiteSpace(s.Description) ? "-" : s.Description;
            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddTopologySheets(XLWorkbook wb, string treeSheetName, string linkSheetName, List<Device> devices)
    {
        var byId = devices.ToDictionary(d => d.Id);
        var byName = BuildNameLookup(devices);
        var byMac = BuildMacLookup(devices);

        var items = devices.Select(d =>
        {
            var parent = ResolveParent(d, byId, byName, byMac);
            var upstreamName = parent?.Name ?? d.UpstreamDeviceName ?? ExtractUpstreamNameFromDetail(d.ConnectionDetail);
            var upstreamConn = d.HostDeviceId is not null ? "Hosted" : d.UpstreamConnection ?? d.ConnectionDetail;
            return new TopoItem
            {
                Device = d,
                ParentId = parent?.Id,
                ParentName = parent?.Name,
                UpstreamName = upstreamName,
                UpstreamConn = upstreamConn
            };
        }).ToList();

        var unassigned = items.Where(IsUnassigned).ToList();
        var roots = items.Where(i =>
            !IsUnassigned(i) &&
            (i.Device.IsTopologyRoot ||
             i.ParentId is null ||
             !byId.ContainsKey(i.ParentId.Value) ||
             (IsGateway(i.Device) && i.ParentId is null))).ToList();

        var childrenByParent = items
            .Where(i => i.ParentId is not null)
            .GroupBy(i => i.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Device.Name, StringComparer.OrdinalIgnoreCase).ToList());

        var treeWs = wb.AddWorksheet(SafeSheetName(treeSheetName));
        var treeHeaders = new[]
        {
            "Level", "Parent", "Device", "Type", "Hostname", "IP", "MAC", "Conn", "Upstream", "Location", "Rack", "Status"
        };
        for (int i = 0; i < treeHeaders.Length; i++)
            treeWs.Cell(1, i + 1).Value = treeHeaders[i];
        treeWs.Range(1, 1, 1, treeHeaders.Length).Style.Font.Bold = true;
        treeWs.SheetView.FreezeRows(1);
        treeWs.RangeUsed().SetAutoFilter();

        var row = 2;
        var visited = new HashSet<int>();

        void WriteNode(TopoItem item, int level, string? parentLabel)
        {
            if (!visited.Add(item.Device.Id))
                return;

            int c = 1;
            treeWs.Cell(row, c++).Value = level;
            treeWs.Cell(row, c++).Value = parentLabel ?? "";
            treeWs.Cell(row, c++).Value = item.Device.Name;
            treeWs.Cell(row, c++).Value = item.Device.ClientType?.Name;
            treeWs.Cell(row, c++).Value = item.Device.Hostname;
            treeWs.Cell(row, c++).Value = item.Device.IpAddress;
            treeWs.Cell(row, c++).Value = item.Device.MacAddress;
            treeWs.Cell(row, c++).Value = item.Device.ConnectionType;
            treeWs.Cell(row, c++).Value = FormatUpstream(item.Device) ?? item.UpstreamName ?? "";
            treeWs.Cell(row, c++).Value = item.Device.LocationRef?.Name ?? item.Device.Location;
            treeWs.Cell(row, c++).Value = FormatRack(item.Device);
            treeWs.Cell(row, c++).Value = FormatStatus(item.Device);
            row++;

            if (childrenByParent.TryGetValue(item.Device.Id, out var children))
            {
                foreach (var child in children)
                    WriteNode(child, level + 1, item.Device.Name);
            }
        }

        foreach (var root in roots.OrderBy(r => r.Device.Name, StringComparer.OrdinalIgnoreCase))
            WriteNode(root, 0, "");

        if (unassigned.Count > 0)
        {
            treeWs.Cell(row, 1).Value = 0;
            treeWs.Cell(row, 2).Value = "";
            treeWs.Cell(row, 3).Value = "Unassigned / Unknown";
            row++;

            foreach (var item in unassigned.OrderBy(r => r.Device.Name, StringComparer.OrdinalIgnoreCase))
                WriteNode(item, 1, "Unassigned / Unknown");
        }

        treeWs.Columns().AdjustToContents();

        var linkWs = wb.AddWorksheet(SafeSheetName(linkSheetName));
        var linkHeaders = new[]
        {
            "Parent", "Child", "Conn", "Upstream", "Type", "IP", "Status"
        };
        for (int i = 0; i < linkHeaders.Length; i++)
            linkWs.Cell(1, i + 1).Value = linkHeaders[i];
        linkWs.Range(1, 1, 1, linkHeaders.Length).Style.Font.Bold = true;
        linkWs.SheetView.FreezeRows(1);
        linkWs.RangeUsed().SetAutoFilter();

        var linkRow = 2;
        foreach (var item in items.Where(i => i.ParentId is not null && byId.ContainsKey(i.ParentId.Value)))
        {
            var parent = byId[item.ParentId!.Value];
            linkWs.Cell(linkRow, 1).Value = parent.Name;
            linkWs.Cell(linkRow, 2).Value = item.Device.Name;
            linkWs.Cell(linkRow, 3).Value = item.Device.ConnectionType;
            linkWs.Cell(linkRow, 4).Value = item.UpstreamConn ?? "";
            linkWs.Cell(linkRow, 5).Value = item.Device.ClientType?.Name;
            linkWs.Cell(linkRow, 6).Value = item.Device.IpAddress;
            linkWs.Cell(linkRow, 7).Value = FormatStatus(item.Device);
            linkRow++;
        }

        linkWs.Columns().AdjustToContents();
    }

    private static void AddUpdaterLogsSheet(XLWorkbook wb, string sheetName, List<UpdaterRunLog> rows)
    {
        var ws = wb.AddWorksheet(SafeSheetName(sheetName));
        var headers = new[] { "Started", "Finished", "Duration", "Changes", "Error", "Source" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed().SetAutoFilter();

        int r = 2;
        foreach (var row in rows)
        {
            int c = 1;
            ws.Cell(r, c++).Value = row.StartedAtUtc;
            ws.Cell(r, c++).Value = row.FinishedAtUtc;
            ws.Cell(r, c++).Value = FormatDuration(row.DurationMs);
            ws.Cell(r, c++).Value = row.ChangedCount;
            ws.Cell(r, c++).Value = row.Error;
            ws.Cell(r, c++).Value = row.Source;
            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddUpdaterChangesSheet(
        XLWorkbook wb,
        string sheetName,
        List<UpdaterChangeLog> rows,
        IReadOnlyDictionary<int, DateTime> runStartedById)
    {
        var ws = wb.AddWorksheet(SafeSheetName(sheetName));
        var headers = new[] { "RunId", "Changed", "Device", "IP", "Field", "Old", "New" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed().SetAutoFilter();

        int r = 2;
        foreach (var row in rows)
        {
            int c = 1;
            ws.Cell(r, c++).Value = row.RunId;
            ws.Cell(r, c++).Value = runStartedById.TryGetValue(row.RunId, out var startedAtUtc)
                ? DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc).ToLocalTime()
                : "";
            ws.Cell(r, c++).Value = row.DeviceName;
            ws.Cell(r, c++).Value = row.IpAddress;
            ws.Cell(r, c++).Value = row.FieldName;
            ws.Cell(r, c++).Value = row.OldValue;
            ws.Cell(r, c++).Value = row.NewValue;
            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddIpHistorySheet(XLWorkbook wb, string sheetName, List<DeviceIpHistory> rows)
    {
        var ws = wb.AddWorksheet(SafeSheetName(sheetName));
        var headers = new[] { "IP", "Device", "MAC", "Type", "Subnet", "First Seen", "Last Seen", "Source" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed().SetAutoFilter();

        int r = 2;
        foreach (var row in rows)
        {
            int c = 1;
            ws.Cell(r, c++).Value = FormatIpHistory(row);
            ws.Cell(r, c++).Value = row.Device?.Name ?? "";
            ws.Cell(r, c++).Value = row.Device?.MacAddress ?? "";
            ws.Cell(r, c++).Value = row.Device?.ClientType?.Name ?? "";
            ws.Cell(r, c++).Value = row.Device?.Subnet?.Name ?? "";
            ws.Cell(r, c++).Value = ToLocalTime(row.FirstSeenUtc);
            ws.Cell(r, c++).Value = row.LastSeenUtc is null ? "Current" : ToLocalTime(row.LastSeenUtc.Value);
            ws.Cell(r, c++).Value = row.Source ?? "";
            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddSubnetIpsSheet(XLWorkbook wb, string sheetName, Subnet subnet, List<SubnetIpRow> rows)
    {
        var ws = wb.AddWorksheet(SafeSheetName(sheetName));

        // Matches your SubnetIps table intent
        var headers = new[]
        {
            "IP", "IP Status", "Notes", "Status", "Name", "Hostname", "MAC", "Conn", "Upstream"
        };

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed().SetAutoFilter();

        int r = 2;
        foreach (var row in rows.OrderBy(x => x.IpUInt))
        {
            int c = 1;

            ws.Cell(r, c++).Value = row.Ip;
            ws.Cell(r, c++).Value = FormatIpStatus(subnet, row);
            ws.Cell(r, c++).Value = row.Reservation?.Notes ?? "";

            if (!row.IsUsed)
                ws.Cell(r, c++).Value = "";           // blank cell
            else
                ws.Cell(r, c++).Value = IsRowOnline(row) ? "Online" : "Offline";

            if (!row.IsUsed)
            {
                Blank(ws.Cell(r, c++));
                Blank(ws.Cell(r, c++));
                Blank(ws.Cell(r, c++));
                Blank(ws.Cell(r, c++));
                Blank(ws.Cell(r, c++));
            }
            else if (row.Devices.Count == 1)
            {
                var d = row.Devices[0];
                ws.Cell(r, c++).Value = d.Name;
                ws.Cell(r, c++).Value = d.Hostname;
                ws.Cell(r, c++).Value = d.MacAddress;
                ws.Cell(r, c++).Value = DisplayFormat.GetConnTooltip(d);
                ws.Cell(r, c++).Value = FormatUpstream(d) ?? d.UpstreamConnection ?? d.ConnectionDetail ?? "";
            }
            else
            {
                // DUP: join distinct values like your UI “(+N)” concept
                ws.Cell(r, c++).Value = string.Join("; ", DistinctNonEmpty(row.Devices.Select(d => d.Name)));
                ws.Cell(r, c++).Value = string.Join("; ", DistinctNonEmpty(row.Devices.Select(d => d.Hostname)));
                ws.Cell(r, c++).Value = string.Join("; ", DistinctNonEmpty(row.Devices.Select(d => d.MacAddress)));
                ws.Cell(r, c++).Value = string.Join("; ", DistinctNonEmpty(row.Devices.Select(d => DisplayFormat.GetConnTooltip(d))));
                ws.Cell(r, c++).Value = string.Join("; ",
                    DistinctNonEmpty(row.Devices.Select(d => FormatUpstream(d) ?? d.UpstreamConnection ?? d.ConnectionDetail)));
            }

            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddSubnetIpsSummarySheet(XLWorkbook wb, string sheetName, List<(Subnet subnet, SubnetIpRow row)> usedRows)
    {
        var ws = wb.AddWorksheet(SafeSheetName(sheetName));

        var headers = new[]
        {
            "Subnet", "CIDR", "IP", "Status", "Name", "Hostname", "MAC", "Conn", "Upstream"
        };

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed().SetAutoFilter();

        int r = 2;
        foreach (var (subnet, row) in usedRows)
        {
            int c = 1;
            ws.Cell(r, c++).Value = subnet.Name;
            ws.Cell(r, c++).Value = subnet.Cidr;
            ws.Cell(r, c++).Value = row.Ip;
            ws.Cell(r, c++).Value = IsRowOnline(row) ? "Online" : "Offline";

            if (row.Devices.Count == 1)
            {
                var d = row.Devices[0];
                ws.Cell(r, c++).Value = d.Name;
                ws.Cell(r, c++).Value = d.Hostname;
                ws.Cell(r, c++).Value = d.MacAddress;
                ws.Cell(r, c++).Value = DisplayFormat.GetConnTooltip(d);
                ws.Cell(r, c++).Value = FormatUpstream(d) ?? d.UpstreamConnection ?? d.ConnectionDetail ?? "";
            }
            else
            {
                ws.Cell(r, c++).Value = string.Join("; ", DistinctNonEmpty(row.Devices.Select(d => d.Name)));
                ws.Cell(r, c++).Value = string.Join("; ", DistinctNonEmpty(row.Devices.Select(d => d.Hostname)));
                ws.Cell(r, c++).Value = string.Join("; ", DistinctNonEmpty(row.Devices.Select(d => d.MacAddress)));
                ws.Cell(r, c++).Value = string.Join("; ", DistinctNonEmpty(row.Devices.Select(d => DisplayFormat.GetConnTooltip(d))));
                ws.Cell(r, c++).Value = string.Join("; ",
                    DistinctNonEmpty(row.Devices.Select(d => FormatUpstream(d) ?? d.UpstreamConnection ?? d.ConnectionDetail)));
            }

            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static string FormatIpHistory(DeviceIpHistory h)
        => h.Port is null || h.Port <= 0 ? h.IpAddress : $"{h.IpAddress}:{h.Port}";

    private static DateTime ToLocalTime(DateTime utc)
        => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    private static void Blank(IXLCell cell) => cell.Value = "";

    private static IEnumerable<string> DistinctNonEmpty(IEnumerable<string?> values)
        => values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

    // ---------------------------
    // Workbook plumbing
    // ---------------------------

    private async Task<byte[]> BuildWorkbookBytesAsync(
        Func<XLWorkbook, AppDbContext, Task> build,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        using var wb = new XLWorkbook();

        await build(wb, db);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string SafeSheetName(string name)
    {
        var invalid = new[] { '[', ']', ':', '*', '?', '/', '\\' };
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "Sheet";

        if (cleaned.Length > 31)
            cleaned = cleaned[..31];

        return cleaned;
    }

    private static string? FormatRack(Device d)
    {
        var name = d.RackRef?.Name;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (d.RackUPosition is null)
            return name;

        return d.RackUSize is null
            ? $"{name} U{d.RackUPosition}"
            : $"{name} U{d.RackUPosition} ({d.RackUSize}U)";
    }

    private static string? FormatUpstream(Device d)
    {
        var upstreamName = d.HostDevice?.Name ?? d.UpstreamDeviceName;
        var upstreamConn = d.HostDevice is not null ? "Hosted" : d.UpstreamConnection;
        if (string.IsNullOrWhiteSpace(upstreamName) && string.IsNullOrWhiteSpace(upstreamConn))
            return null;

        if (string.IsNullOrWhiteSpace(upstreamName))
            return upstreamConn;

        if (string.IsNullOrWhiteSpace(upstreamConn))
            return upstreamName;

        return $"{upstreamName} | {upstreamConn}";
    }

    private static string FormatStatus(Device d)
        => !d.IsStatusTracked ? "Not Tracked" : d.IsOnline ? "Online" : "Offline";

    private static bool IsGateway(Device d)
    {
        var typeName = d.ClientType?.Name ?? "";
        return typeName.Contains("gateway", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("router", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnassigned(TopoItem item)
        => item.ParentId is null &&
           string.IsNullOrWhiteSpace(item.UpstreamName) &&
           string.IsNullOrWhiteSpace(item.UpstreamConn) &&
           !item.Device.IsTopologyRoot &&
           !IsGateway(item.Device);

    private static Device? ResolveParent(
        Device d,
        IDictionary<int, Device> byId,
        IDictionary<string, Device> byName,
        IDictionary<string, Device> byMac)
    {
        if (d.IsTopologyRoot)
            return null;

        if (d.HostDeviceId is int hid && byId.TryGetValue(hid, out var host))
            return host;

        if (d.ManualUpstreamDeviceId is int mid && byId.TryGetValue(mid, out var manual))
            return manual;

        if (d.ParentDeviceId is int pid && byId.TryGetValue(pid, out var p1))
            return p1;

        if (!string.IsNullOrWhiteSpace(d.UpstreamDeviceName))
        {
            var key = d.UpstreamDeviceName.Trim();
            if (byName.TryGetValue(key, out var p2))
                return p2;
        }

        var parsedName = ExtractUpstreamNameFromDetail(d.ConnectionDetail);
        if (!string.IsNullOrWhiteSpace(parsedName) && byName.TryGetValue(parsedName, out var p4))
            return p4;

        var upstreamMac = NormalizeMac(d.UpstreamDeviceMac);
        if (!string.IsNullOrWhiteSpace(upstreamMac) && byMac.TryGetValue(upstreamMac, out var p3))
            return p3;

        return null;
    }

    private static Dictionary<string, Device> BuildNameLookup(IEnumerable<Device> devices)
    {
        var dict = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in devices.Where(d => !string.IsNullOrWhiteSpace(d.Name)))
        {
            var key = d.Name!.Trim();
            if (!dict.ContainsKey(key))
                dict[key] = d;
        }
        return dict;
    }

    private static Dictionary<string, Device> BuildMacLookup(IEnumerable<Device> devices)
    {
        var dict = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in devices)
        {
            var key = NormalizeMac(d.MacAddress);
            if (string.IsNullOrWhiteSpace(key) || dict.ContainsKey(key))
                continue;
            dict[key] = d;
        }
        return dict;
    }

    private static string NormalizeMac(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        Span<char> buffer = stackalloc char[raw.Length];
        var len = 0;
        foreach (var ch in raw)
        {
            if (char.IsLetterOrDigit(ch))
                buffer[len++] = char.ToUpperInvariant(ch);
        }

        return new string(buffer[..len]);
    }

    private static string? ExtractUpstreamNameFromDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return null;

        var trimmed = detail.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var pipeIndex = trimmed.IndexOf('|', StringComparison.Ordinal);
        if (pipeIndex > 0)
            return trimmed[..pipeIndex].Trim();

        var parenIndex = trimmed.IndexOf(" (", StringComparison.Ordinal);
        if (parenIndex > 0)
            return trimmed[..parenIndex].Trim();

        return null;
    }

    private static string? FormatDuration(int? ms)
    {
        if (ms is null) return null;
        var t = TimeSpan.FromMilliseconds(ms.Value);
        return t.TotalSeconds < 60
            ? $"{(int)t.TotalSeconds}s"
            : $"{(int)t.TotalMinutes}m {t.Seconds}s";
    }

    private static string FormatUsableRange(string? cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
            return "—";

        if (!IpCidr.TryParseIPv4Cidr(cidr, out var info, out _))
            return "—";

        if (info.PrefixLength == 32)
            return $"{info.Network} (single host)";

        if (info.PrefixLength == 31)
            return $"{info.Network} – {info.Broadcast} (/31)";

        if (info.FirstUsable is null || info.LastUsable is null)
            return "—";

        return $"{info.FirstUsable} – {info.LastUsable}";
    }

    private static string FormatUsableCount(string? cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
            return "-";

        if (!IpCidr.TryParseIPv4Cidr(cidr, out var info, out _))
            return "-";

        return $"{info.UsableAddresses} / {info.TotalAddresses}";
    }

    private static string FormatDhcpRange(Subnet s)
    {
        var start = s.DhcpRangeStart?.Trim();
        var end = s.DhcpRangeEnd?.Trim();

        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end))
            return "-";

        if (!string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(end))
            return $"{start} - {end}";

        return start ?? end ?? "-";
    }

    private static string FormatVlan(int? vlan)
        => vlan.HasValue ? vlan.Value.ToString() : "-";

    private static string FormatDns(Subnet s)
    {
        var d1 = s.Dns1?.Trim();
        var d2 = s.Dns2?.Trim();

        if (string.IsNullOrWhiteSpace(d1) && string.IsNullOrWhiteSpace(d2))
            return "—";

        if (!string.IsNullOrWhiteSpace(d1) &&
            string.Equals(d1, "Auto", StringComparison.OrdinalIgnoreCase))
            return "Auto";

        return string.IsNullOrWhiteSpace(d2) ? d1 ?? "—" : $"{d1} | {d2}";
    }

    private static string FormatNextFreeIps(
        int subnetId,
        string? dhcpStart,
        string? dhcpEnd,
        IReadOnlyDictionary<int, IpCidr.CidrInfo> subnetInfos,
        IReadOnlyDictionary<int, HashSet<uint>> usedIpBySubnet,
        IReadOnlyDictionary<int, HashSet<uint>> reservedIpBySubnet)
    {
        if (!subnetInfos.TryGetValue(subnetId, out var info))
            return "-";

        usedIpBySubnet.TryGetValue(subnetId, out var usedSet);
        reservedIpBySubnet.TryGetValue(subnetId, out var reservedSet);
        usedSet ??= new HashSet<uint>();
        reservedSet ??= new HashSet<uint>();

        var ips = FindNextFreeIps(info, dhcpStart, dhcpEnd, usedSet, reservedSet, 3);
        return ips.Count == 0 ? "-" : string.Join(", ", ips);
    }

    private static List<string> FindNextFreeIps(
        IpCidr.CidrInfo info,
        string? dhcpStart,
        string? dhcpEnd,
        HashSet<uint> usedSet,
        HashSet<uint> reservedSet,
        int count)
    {
        var results = new List<string>();

        if (string.IsNullOrWhiteSpace(info.FirstUsable) || string.IsNullOrWhiteSpace(info.LastUsable))
            return results;

        if (!IpCidr.TryParseIPv4(info.FirstUsable, out var start, out _))
            return results;
        if (!IpCidr.TryParseIPv4(info.LastUsable, out var end, out _))
            return results;

        uint dhcpStartVal = 0;
        uint dhcpEndVal = 0;
        var hasDhcpStart = IpCidr.TryParseIPv4(dhcpStart ?? "", out dhcpStartVal, out _);
        var hasDhcpEnd = IpCidr.TryParseIPv4(dhcpEnd ?? "", out dhcpEndVal, out _);
        if (hasDhcpStart && hasDhcpEnd && dhcpEndVal < dhcpStartVal)
        {
            var tmp = dhcpStartVal;
            dhcpStartVal = dhcpEndVal;
            dhcpEndVal = tmp;
        }

        for (uint ip = start; ip <= end && results.Count < count; ip++)
        {
            if (usedSet.Contains(ip))
                continue;
            if (reservedSet.Contains(ip))
                continue;
            if (hasDhcpStart && hasDhcpEnd && ip >= dhcpStartVal && ip <= dhcpEndVal)
                continue;

            results.Add(IpCidr.UIntToIPv4(ip));
        }

        return results;
    }

    private static string? ExtractFirstIPv4(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var m = FirstIPv4Regex.Match(text);
        return m.Success ? m.Value : null;
    }

    private static int GetCount(IReadOnlyDictionary<int, int> map, int subnetId)
        => map.TryGetValue(subnetId, out var count) ? count : 0;

    private static int GetCount(IReadOnlyDictionary<int, HashSet<uint>> map, int subnetId)
        => map.TryGetValue(subnetId, out var set) ? set.Count : 0;

    private enum IpDisplayStatus
    {
        Available,
        Reserved,
        Dhcp,
        AssignedStatic,
        AssignedDhcp,
        Deprecated
    }

    private sealed class SubnetIpRow
    {
        public uint IpUInt { get; set; }
        public string Ip { get; set; } = "";
        public List<Device> Devices { get; set; } = new();
        public IpAssignment? Reservation { get; set; }
        public bool IsUsed => Devices.Count > 0;
    }

    private sealed record DeviceLite(
        int? SubnetId,
        string? IpAddress,
        bool IsOnline,
        bool IsDevice,
        bool IgnoreOffline,
        bool IsStatusTracked);

    private sealed record SubnetSummary(
        string UsableRange,
        string UsableCount,
        int UsedIps,
        int Clients,
        int Devices,
        int Offline,
        string DhcpRange,
        string Vlan,
        string Dns,
        string NextFreeIps)
    {
        public static readonly SubnetSummary Empty = new(
            UsableRange: "—",
            UsableCount: "-",
            UsedIps: 0,
            Clients: 0,
            Devices: 0,
            Offline: 0,
            DhcpRange: "-",
            Vlan: "-",
            Dns: "—",
            NextFreeIps: "-");
    }

    private sealed class TopoItem
    {
        public required Device Device { get; init; }
        public int? ParentId { get; init; }
        public string? ParentName { get; init; }
        public string? UpstreamName { get; init; }
        public string? UpstreamConn { get; init; }
    }
}
