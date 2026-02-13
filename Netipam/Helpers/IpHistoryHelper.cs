using Microsoft.EntityFrameworkCore;
using Netipam.Data;

namespace Netipam.Helpers;

public static class IpHistoryHelper
{
    public static async Task TrackIpChangeAsync(
        AppDbContext db,
        Device device,
        string? source,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var ip = device.IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(ip))
            return;

        var port = (DisplayFormat.UsesPort(device.MonitorMode) || DisplayFormat.UsesHttp(device.MonitorMode))
            ? device.MonitorPort
            : null;

        var current = await db.DeviceIpHistories
            .Where(h => h.DeviceId == device.Id && h.LastSeenUtc == null)
            .OrderByDescending(h => h.FirstSeenUtc)
            .FirstOrDefaultAsync(ct);

        if (current is not null &&
            string.Equals(current.IpAddress, ip, StringComparison.OrdinalIgnoreCase) &&
            current.Port == port)
        {
            return;
        }

        if (current is not null)
            current.LastSeenUtc = nowUtc;

        db.DeviceIpHistories.Add(new DeviceIpHistory
        {
            DeviceId = device.Id,
            IpAddress = ip,
            Port = port,
            Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
            FirstSeenUtc = nowUtc
        });
    }
}
