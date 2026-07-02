using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Avantibit.Optimizely.CustomSettings.Discovery;

/// <summary>
/// Hosted service that triggers settings group discovery at application startup.
/// </summary>
public class SettingsDiscoveryHostedService : IHostedService
{
    private readonly ISettingsDiscoveryService _settingsDiscoveryService;
    private readonly ILogger<SettingsDiscoveryHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsDiscoveryHostedService"/> class.
    /// </summary>
    /// <param name="settingsDiscoveryService">The settings discovery service.</param>
    /// <param name="logger">The logger instance.</param>
    public SettingsDiscoveryHostedService(
        ISettingsDiscoveryService settingsDiscoveryService,
        ILogger<SettingsDiscoveryHostedService> logger)
    {
        _settingsDiscoveryService = settingsDiscoveryService ?? throw new ArgumentNullException(nameof(settingsDiscoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the hosted service and triggers settings group discovery.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting settings group discovery...");

        try
        {
            var groups = _settingsDiscoveryService.GetAllGroups();

            _logger.LogInformation(
                "Settings group discovery completed. Discovered {Count} group(s): {Groups}",
                groups.Count,
                string.Join(", ", groups.Select(g => g.Name)));

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover settings groups during application startup. Application will continue but custom settings may not be available.");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stops the hosted service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Settings discovery hosted service is stopping");
        return Task.CompletedTask;
    }
}