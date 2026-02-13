using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Netipam.Data;
using System.Threading;

namespace Netipam.Services;

public sealed class AppSettingsService
{
    private const int SingletonId = 1;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDataProtector _protector;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Fired after a successful persisted update (broadcast to all listeners)
    public event Action<AppSetting>? SettingsChanged;

    public AppSettingsService(IServiceScopeFactory scopeFactory, IDataProtectionProvider dp)
    {
        _scopeFactory = scopeFactory;
        _protector = dp.CreateProtector("Netipam.AppSettings.v1");
    }

    private AppDbContext CreateDb(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    public async Task<AppSetting> LoadAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = CreateDb(scope);

        var s = await db.AppSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == SingletonId, ct);
        if (s is not null)
        {
            s.UnifiAuthMode = NormalizeUnifiAuthMode(s.UnifiAuthMode);
            return s;
        }

        // Seed singleton row if missing
        var created = new AppSetting
        {
            Id = SingletonId,

            // sensible defaults
            DarkMode = true,
            ThemeName = "High Contrast",
            DateFormat = "MM-dd-yyyy HH:mm",
            UiAutoRefreshSeconds = 0,
            ShowLastSeenTooltips = true,
            SiteTitle = "Netipam",
            UiShowWanStatus = true,

            UnifiUpdaterEnabled = false,
            UnifiUpdaterIntervalSeconds = 60,
            UnifiUpdateConnectionFieldsWhenOnline = true,
            UnifiSyncIpAddress = true,
            UnifiSyncOnlineStatus = true,
            UnifiSyncName = false,
            UnifiSyncHostname = false,
            UnifiSyncManufacturer = false,
            UnifiSyncModel = false,
            UnifiAuthMode = "Session",
        };

        db.AppSettings.Add(created);
        await db.SaveChangesAsync(ct);

        return created;
    }

    /// <summary>
    /// Preferred: Patch-style mutation inside a gate, then emits SettingsChanged with persisted snapshot.
    /// </summary>
    public async Task UpdateAsync(Action<AppSetting> mutate, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = CreateDb(scope);

            var current = await db.AppSettings.SingleOrDefaultAsync(x => x.Id == SingletonId, ct)
                          ?? await EnsureSingletonRowAsync(db, ct);

            mutate(current);
            // Always keep tooltips enabled.
            current.ShowLastSeenTooltips = true;
            await db.SaveChangesAsync(ct);

            var fresh = await db.AppSettings.AsNoTracking().SingleAsync(x => x.Id == SingletonId, ct);
            SettingsChanged?.Invoke(fresh);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Save a full model snapshot by copying values onto the tracked singleton row.
    /// (Used by Settings.razor after edits.)
    /// </summary>
    public async Task SaveAsync(AppSetting incoming, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = CreateDb(scope);

            var current = await db.AppSettings.SingleOrDefaultAsync(x => x.Id == SingletonId, ct)
                          ?? await EnsureSingletonRowAsync(db, ct);

            // Copy persisted fields (add/remove as your AppSetting grows)
            current.UnifiUpdaterEnabled = incoming.UnifiUpdaterEnabled;
            current.UnifiUpdaterIntervalSeconds = incoming.UnifiUpdaterIntervalSeconds;
            current.UnifiUpdateConnectionFieldsWhenOnline = incoming.UnifiUpdateConnectionFieldsWhenOnline;
            current.UnifiSyncIpAddress = incoming.UnifiSyncIpAddress;
            current.UnifiSyncOnlineStatus = incoming.UnifiSyncOnlineStatus;
            current.UnifiSyncName = incoming.UnifiSyncName;
            current.UnifiSyncHostname = incoming.UnifiSyncHostname;
            current.UnifiSyncManufacturer = incoming.UnifiSyncManufacturer;
            current.UnifiSyncModel = incoming.UnifiSyncModel;

            current.UnifiBaseUrl = incoming.UnifiBaseUrl;
            current.UnifiSiteName = incoming.UnifiSiteName;
            current.UnifiUsername = incoming.UnifiUsername;
            current.UnifiPasswordProtected = incoming.UnifiPasswordProtected;
            current.UnifiAuthMode = NormalizeUnifiAuthMode(incoming.UnifiAuthMode);
            current.UnifiApiKeyProtected = incoming.UnifiApiKeyProtected;

            current.ShowLastSeenTooltips = incoming.ShowLastSeenTooltips;
            current.UiAutoRefreshSeconds = incoming.UiAutoRefreshSeconds;
            current.UiShowWanStatus = incoming.UiShowWanStatus;

            current.DarkMode = incoming.DarkMode;
            current.ThemeName = incoming.ThemeName;
            current.SiteTitle = incoming.SiteTitle;
            current.DateFormat = incoming.DateFormat;
            // Always keep tooltips enabled.
            current.ShowLastSeenTooltips = true;

            await db.SaveChangesAsync(ct);

            var fresh = await db.AppSettings.AsNoTracking().SingleAsync(x => x.Id == SingletonId, ct);
            SettingsChanged?.Invoke(fresh);
        }
        finally
        {
            _gate.Release();
        }
    }

    public string? Protect(string? plaintext) =>
        string.IsNullOrWhiteSpace(plaintext) ? null : _protector.Protect(plaintext);

    public string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return null;
        try { return _protector.Unprotect(protectedValue); }
        catch { return null; }
    }

    private static string NormalizeUnifiAuthMode(string? mode)
        => string.Equals(mode?.Trim(), "ApiKey", StringComparison.OrdinalIgnoreCase) ? "ApiKey" : "Session";

    private static async Task<AppSetting> EnsureSingletonRowAsync(AppDbContext db, CancellationToken ct)
    {
        var created = new AppSetting
        {
            Id = SingletonId,
            DarkMode = true,
            ThemeName = "High Contrast",
            DateFormat = "MM-dd-yyyy HH:mm",
            UiAutoRefreshSeconds = 0,
            ShowLastSeenTooltips = true,
            SiteTitle = "Netipam",
            UiShowWanStatus = true,
            UnifiUpdaterEnabled = false,
            UnifiUpdaterIntervalSeconds = 60,
            UnifiUpdateConnectionFieldsWhenOnline = true,
            UnifiSyncIpAddress = true,
            UnifiSyncOnlineStatus = true,
            UnifiSyncName = false,
            UnifiSyncHostname = false,
            UnifiSyncManufacturer = false,
            UnifiSyncModel = false,
            UnifiAuthMode = "Session",
        };

        db.AppSettings.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}
