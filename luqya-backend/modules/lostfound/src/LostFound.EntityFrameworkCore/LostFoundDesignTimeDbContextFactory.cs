using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LostFound.EntityFrameworkCore;

public class LostFoundDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<LostFoundDbContext>
{
    public LostFoundDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<LostFoundDbContext>()
            .UseSqlServer(
                configuration.GetConnectionString("Default")
            );

        return new LostFoundDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        var hostPath = Path.Combine(
            currentDirectory,
            "../../../../src/Forge.HttpApi.Host"
        );

        if (!Directory.Exists(hostPath))
        {
            hostPath = currentDirectory;
        }

        return new ConfigurationBuilder()
            .SetBasePath(hostPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
    }
}