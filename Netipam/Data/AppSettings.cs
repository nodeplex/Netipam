namespace Netipam.Data;

public sealed class AppSetting
{
    public int Id { get; set; }  // always 1 (singleton row)

    // --------------------
    // UniFi updater
    // --------------------
    public bool UnifiUpdaterEnabled { get; set; } = true;
    public int UnifiUpdaterIntervalSeconds { get; set; } = 120;
    public bool UnifiUpdateConnectionFieldsWhenOnline { get; set; } = true;
    public bool UnifiSyncIpAddress { get; set; } = true;
    public bool UnifiSyncOnlineStatus { get; set; } = true;
    public bool UnifiSyncName { get; set; } = false;
    public bool UnifiSyncHostname { get; set; } = false;
    public bool UnifiSyncManufacturer { get; set; } = false;
    public bool UnifiSyncModel { get; set; } = false;

    // UniFi connection details (DB-backed settings)
    public string? UnifiBaseUrl { get; set; }         // e.g. https://unifi.local:8443
    public string? UnifiSiteName { get; set; }        // e.g. default
    public string? UnifiUsername { get; set; }
    public string? UnifiPasswordProtected { get; set; } // encrypted at rest
    public string UnifiAuthMode { get; set; } = "Session"; // Session | ApiKey
    public string? UnifiApiKeyProtected { get; set; } // encrypted at rest

    // --------------------
    // Proxmox host mapping
    // --------------------
    public bool ProxmoxEnabled { get; set; } = false;
    public string? ProxmoxBaseUrl { get; set; } // e.g. https://proxmox.local:8006
    public string? ProxmoxApiTokenId { get; set; } // e.g. user@pve!token
    public string? ProxmoxApiTokenSecretProtected { get; set; } // encrypted at rest
    public string? ProxmoxHostDeviceMac { get; set; } // host device MAC in Netipam
    public int ProxmoxIntervalSeconds { get; set; } = 300;
    public bool ProxmoxUpdateExistingHostAssignments { get; set; } = true;

    // --------------------
    // UI / general
    // --------------------
    public bool ShowLastSeenTooltips { get; set; } = true;
    public int UiAutoRefreshSeconds { get; set; } = 0; // 0 = off
    public bool DarkMode { get; set; } = true;
    public string ThemeName { get; set; } = "Graphite";
    public bool UiShowWanStatus { get; set; } = true;

    // Cosmetic "site name" shown on the app bar + title
    public string SiteTitle { get; set; } = "Netipam";

    // Date format used by dashboards/pages (you’ll apply this where you format dates)
    public string DateFormat { get; set; } = "MM-dd-yyyy HH:mm";
}
