using Microsoft.EntityFrameworkCore;
using Netipam.Data;

namespace Netipam.Services;

public static class SubnetResolver
{
    public static async Task<int?> ResolveSubnetIdForIpAsync(AppDbContext db, string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return null;

        // Ensure IPv4 only for now (matches your IpCidr helper)
        if (!IpCidr.TryParseIPv4(ip.Trim(), out var ipUInt, out _))
            return null;

        // Pull minimal subnet data once
        var subnets = await db.Subnets
            .AsNoTracking()
            .Select(s => new { s.Id, s.Cidr })
            .ToListAsync();

        foreach (var s in subnets)
        {
            if (string.IsNullOrWhiteSpace(s.Cidr))
                continue;

            if (!IpCidr.TryParseIPv4Cidr(s.Cidr, out var info, out _))
                continue;

            // Match: ip between network and broadcast inclusive
            if (ipUInt >= info.NetworkUInt && ipUInt <= info.BroadcastUInt)
                return s.Id;
        }

        return null;
    }
}

