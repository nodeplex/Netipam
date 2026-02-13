using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Netipam.Data;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Read appsettings.json the same way the app would
        var basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var configured = config.GetConnectionString("Default") ?? "Data Source=Netipam.db";

        // Match your "force absolute path under ContentRoot" behavior
        var sqlite = MakeSqliteAbsolute(configured, basePath);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqlite)
            .Options;

        return new AppDbContext(options);
    }

    private static string MakeSqliteAbsolute(string configured, string contentRoot)
    {
        const string prefix = "Data Source=";

        if (!configured.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return configured;

        var dataSource = configured.Substring(configured.IndexOf('=') + 1).Trim().Trim('"');

        if (!Path.IsPathRooted(dataSource))
        {
            var dbPath = Path.Combine(contentRoot, dataSource);
            return $"Data Source={dbPath}";
        }

        return configured;
    }
}
