using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EzNihongo.Platform.Api.Identity;

public sealed class PlatformIdentityDbContextFactory : IDesignTimeDbContextFactory<PlatformIdentityDbContext>
{
    public PlatformIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Postgres__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set Postgres__ConnectionString before creating or applying Identity migrations.");
        }

        var options = new DbContextOptionsBuilder<PlatformIdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PlatformIdentityDbContext(options);
    }
}
