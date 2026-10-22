using Microsoft.EntityFrameworkCore;

namespace EzNihongo.Platform.Api.Identity;

public sealed class IdentityDatabaseStartupCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<IdentityDatabaseStartupCheck> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();

        try
        {
            if (!await database.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException("The PostgreSQL identity database is not reachable.");
            }

            await database.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogCritical(exception,
                "Cannot connect to or initialize the PostgreSQL database; the API will not start.");
            throw new InvalidOperationException(
                "The PostgreSQL database is unavailable or its schema could not be initialized. Verify the Postgres:ConnectionString configuration and migrations.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
