using Microsoft.EntityFrameworkCore;
using Netipam.Data;

namespace Netipam.Services;

public sealed class DatabaseResetService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public DatabaseResetService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<int> ResetImportedDataAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var total = 0;

        // Delete in FK-safe order (devices reference subnets, access orders reference devices)
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"UserAccessItemOrders\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"ClientOfflineAlerts\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"ClientDiscoveryAlerts\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IgnoredDiscoveryMacs\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DeviceStatusEvents\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DeviceStatusDaily\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DeviceIpHistories\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"UpdaterChangeLogs\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"UpdaterRunLogs\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"WanInterfaceStatuses\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"IpAssignments\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Devices\";", ct);
        total += await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Subnets\";", ct);

        await tx.CommitAsync(ct);
        return total;
    }
}

