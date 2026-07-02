using Avantibit.Optimizely.CustomSettings.Persistence.EfCore.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Avantibit.Optimizely.CustomSettings.Infrastructure;

/// <summary>
/// Applies pending EF Core migrations for CustomSettingsDbContext at application startup.
/// </summary>
internal sealed class CustomSettingsMigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CustomSettingsMigrationHostedService> _logger;

    public CustomSettingsMigrationHostedService(
        IServiceProvider serviceProvider,
        ILogger<CustomSettingsMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CustomSettingsDbContext>();

        try
        {
            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);

            if (pending.Any())
            {
                _logger.LogInformation(
                    "Applying {Count} pending CustomSettings migration(s): {Migrations}",
                    pending.Count(),
                    string.Join(", ", pending));

                await context.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("CustomSettings migrations applied successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply CustomSettings database migrations.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}