using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Netipam.Components;
using Netipam.Data;
using Netipam.Proxmox;
using Netipam.Services;
using Netipam.Unifi;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Services
// --------------------
builder.Services.AddSingleton<UnifiUpdaterControl>();
builder.Services.AddSingleton<ProxmoxUpdaterControl>();
builder.Services.AddSingleton<AppSettingsService>();
builder.Services.AddSingleton<PingStatusService>();
builder.Services.AddHostedService<UnifiOnlineStatusUpdater>();
builder.Services.AddHostedService<ProxmoxHostMappingUpdater>();
builder.Services.AddScoped<UiSessionStateService>();
builder.Services.AddScoped<DatabaseResetService>();
builder.Services.AddScoped<BackupService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
// Persist DataProtection keys so auth cookies survive restarts (Docker: mount /data/keys)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/data/keys"))
    .SetApplicationName("Netipam");
builder.Services.AddScoped<ExcelExportService>();

// --------------------
// EF Core (SQLite) - FIX RELATIVE PATH
// --------------------
var configured = builder.Configuration.GetConnectionString("Default") ?? "Data Source=Netipam.db";
string sqliteConnectionString;

const string prefix = "Data Source=";
if (configured.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
{
    var dataSource = configured.Substring(configured.IndexOf('=') + 1).Trim().Trim('"');
    sqliteConnectionString = Path.IsPathRooted(dataSource)
        ? configured
        : $"Data Source={Path.Combine(builder.Environment.ContentRootPath, dataSource)}";
}
else
{
    sqliteConnectionString = configured;
}

// Identity needs AddDbContext (scoped DbContext).
// Your services prefer IDbContextFactory (singleton factory).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(sqliteConnectionString),
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Singleton);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));

// --------------------
// Identity / Auth (Blazor-only UI)
// --------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;

        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Force any auth “challenge” redirect to our Blazor login page
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
});

// Blazor auth state integration
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    // Require auth everywhere by default
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("CanEdit", p => p.RequireRole("Admin", "Editor"));
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

// --------------------
// UniFi options + client
// --------------------
builder.Services.Configure<UnifiOptions>(builder.Configuration.GetSection("Unifi"));
builder.Services.Configure<ProxmoxOptions>(builder.Configuration.GetSection("Proxmox"));
builder.Services.AddHttpClient<UnifiApiClient>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    });
builder.Services.AddHttpClient<ProxmoxApiClient>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    });

var app = builder.Build();

// Migrate + seed Identity (roles + admin user)
if (builder.Configuration.GetValue<bool>("Netipam:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsDevelopment())
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM __EFMigrationsLock;");
        }
        catch
        {
            // Ignore if lock table doesn't exist yet.
        }
    }
    await db.Database.MigrateAsync();
}

await IdentitySeed.EnsureSeededAsync(app.Services, app.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

// If no users exist yet, force setup flow
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/setup", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/auth/setup", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/favicon", StringComparison.OrdinalIgnoreCase) ||
        Path.HasExtension(path))
    {
        await next();
        return;
    }

    using var scope = app.Services.CreateScope();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (!await userMgr.Users.AnyAsync())
    {
        context.Response.Redirect("/setup");
        return;
    }

    await next();
});

// Auth middleware (REQUIRED)
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// --------------------
// Export endpoints
// --------------------
app.MapGet("/export/clients.xlsx", async (ExcelExportService exporter, CancellationToken ct) =>
{
    var bytes = await exporter.ExportClientsAsync(ct);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Netipam-Clients.xlsx");
});

app.MapGet("/export/devices.xlsx", async (ExcelExportService exporter, CancellationToken ct) =>
{
    var bytes = await exporter.ExportDevicesAsync(ct);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Netipam-Devices.xlsx");
});

app.MapGet("/export/subnets.xlsx", async (ExcelExportService exporter, CancellationToken ct) =>
{
    var bytes = await exporter.ExportSubnetsAsync(ct);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Netipam-Subnets.xlsx");
});

app.MapGet("/export/subnets/{subnetId:int}/ips.xlsx", async (int subnetId, ExcelExportService exporter, CancellationToken ct) =>
{
    var bytes = await exporter.ExportSubnetIpsForSubnetAsync(subnetId, ct);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Netipam-Subnet-{subnetId}-IPs.xlsx");
});

// Health endpoint for orchestrators
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        // Minimal DB check
        await db.Database.ExecuteSqlRawAsync("SELECT 1;", ct);
        return Results.Ok(new { status = "ok" });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
}).AllowAnonymous();

app.MapGet("/export/all.xlsx", async (ExcelExportService exporter, bool? perSubnetTabs, CancellationToken ct) =>
{
    var bytes = await exporter.ExportAllAsync(includePerSubnetTabs: perSubnetTabs == true, ct);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        perSubnetTabs == true ? "Netipam-All-WithSubnets.xlsx" : "Netipam-All.xlsx");
});

app.MapGet("/export/topology.xlsx", async (ExcelExportService exporter, CancellationToken ct) =>
{
    var bytes = await exporter.ExportTopologyAsync(ct);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Netipam-Topology.xlsx");
});

app.MapGet("/export/updater-logs.xlsx", async (ExcelExportService exporter, CancellationToken ct) =>
{
    var bytes = await exporter.ExportUpdaterLogsAsync(ct);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Netipam-Updater-Logs.xlsx");
});

app.MapGet("/export/ip-history.xlsx", async (ExcelExportService exporter, CancellationToken ct) =>
{
    var bytes = await exporter.ExportIpHistoryAsync(ct);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Netipam-Ip-History.xlsx");
});

// --------------------
// Backup endpoints (JSON)
// --------------------
app.MapGet("/backup/export", async (BackupService backup, CancellationToken ct) =>
{
    var bytes = await backup.ExportAsync(ct);
    var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
    return Results.File(bytes, "application/json", $"Netipam-Backup-{stamp}.json");
}).RequireAuthorization("AdminOnly");

app.MapPost("/backup/import", async (HttpRequest request, BackupService backup, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Invalid content type.");

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
        return Results.BadRequest("Backup file is missing.");

    if (file.Length > 1024L * 1024L * 200L)
        return Results.BadRequest("Backup file is too large.");

    await using var stream = file.OpenReadStream();
    var deleted = await backup.ImportAsync(stream, ct);
    return Results.Ok(new { deleted });
})
.RequireAuthorization("AdminOnly")
.DisableAntiforgery();

app.MapPost("/auth/login", async (
    HttpContext http,
    SignInManager<ApplicationUser> signInManager,
    [FromForm] string username,
    [FromForm] string password,
    [FromForm] bool rememberMe,
    [FromForm] string? returnUrl) =>
{
    returnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;

    // Only allow local redirects (prevents open redirect)
    if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        returnUrl = "/";

    var result = await signInManager.PasswordSignInAsync(username, password, rememberMe, lockoutOnFailure: false);

    if (result.Succeeded)
        return Results.LocalRedirect(returnUrl);

    // bounce back with an error flag
    var ru = Uri.EscapeDataString(returnUrl);
    return Results.LocalRedirect($"/login?error=1&returnUrl={ru}");
})
.AllowAnonymous()
.DisableAntiforgery(); // keep simple for now; you can enable antiforgery later

app.MapPost("/auth/setup", async (
    UserManager<ApplicationUser> userMgr,
    RoleManager<IdentityRole> roleMgr,
    [FromForm] string username,
    [FromForm] string password,
    [FromForm] string confirmPassword) =>
{
    if (await userMgr.Users.AnyAsync())
        return Results.Redirect("/login");

    username = (username ?? "").Trim();
    if (string.IsNullOrWhiteSpace(username))
        return Results.LocalRedirect("/setup?error=" + Uri.EscapeDataString("Username is required."));

    if (string.IsNullOrWhiteSpace(password))
        return Results.LocalRedirect("/setup?error=" + Uri.EscapeDataString("Password is required."));

    if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        return Results.LocalRedirect("/setup?error=" + Uri.EscapeDataString("Passwords do not match."));

    if (!await roleMgr.RoleExistsAsync("Admin"))
        await roleMgr.CreateAsync(new IdentityRole("Admin"));

    var user = new ApplicationUser
    {
        UserName = username,
        Email = null,
        EmailConfirmed = true
    };

    var create = await userMgr.CreateAsync(user, password);
    if (!create.Succeeded)
    {
        var msg = string.Join(" | ", create.Errors.Select(e => e.Description));
        return Results.LocalRedirect("/setup?error=" + Uri.EscapeDataString(msg));
    }

    await userMgr.AddToRoleAsync(user, "Admin");
    return Results.Redirect("/login");
})
.AllowAnonymous()
.DisableAntiforgery();

app.MapGet("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
