using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Netipam.Data;

public static class IdentitySeed
{
    public static async Task EnsureSeededAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentitySeedMarker>>();

        // Roles you will use for permissions
        var roles = new[] { "Admin", "Editor", "Viewer" };
        foreach (var r in roles)
        {
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));
        }

        // If any users already exist, do not reseed.
        if (await userMgr.Users.AnyAsync())
            return;

        logger.LogWarning("No users exist yet. Navigate to /setup to create the first admin user.");
    }

    private sealed class IdentitySeedMarker { }
}
