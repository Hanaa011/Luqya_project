using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Forge.EntityFrameworkCore;

/* This class is needed for EF Core console commands
 * (like Add-Migration and Update-Database commands) */
public class ForgeDbContextFactory : IDesignTimeDbContextFactory<ForgeDbContext>
{
    public ForgeDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        
        ForgeEfCoreEntityExtensionMappings.Configure();

        var builder = new DbContextOptionsBuilder<ForgeDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));
        
        return new ForgeDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Forge.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}
