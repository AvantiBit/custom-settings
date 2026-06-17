using Avantibit.Optimizely.CustomSettings.Caching;
using Avantibit.Optimizely.CustomSettings.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avantibit.Optimizely.CustomSettings.Synchronization;

/// <summary>
/// Hosted service that keeps the settings cache synchronized across servers by polling a version counter in the database.
/// Blocks startup until the cache is warm (all settings loaded).
/// </summary>
internal sealed class SettingsCachePollingService : IHostedService
{
    private readonly ISettingsCacheService _cacheService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SettingsCacheOptions _options;
    private readonly ILogger<SettingsCachePollingService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pollingTask;
    private long _lastKnownVersion;

    public SettingsCachePollingService(
        ISettingsCacheService cacheService,
        IServiceScopeFactory scopeFactory,
        IOptions<SettingsCacheOptions> options,
        ILogger<SettingsCachePollingService> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Warming settings cache before accepting requests...");

        await _cacheService.LoadAllAsync(cancellationToken);
        _lastKnownVersion = await GetVersionFromDbAsync(cancellationToken);

        _logger.LogInformation("Settings cache warmed. Starting polling (interval: {Interval}s, jitter: 0-{Jitter}s)",
            _options.PollingIntervalSeconds, _options.MaxJitterSeconds);

        _pollingTask = PollLoopAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();

        if (_pollingTask != null)
        {
            await Task.WhenAny(_pollingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        var random = new Random();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var jitterMs = random.Next(0, _options.MaxJitterSeconds * 1000);
                var delayMs = (_options.PollingIntervalSeconds * 1000) + jitterMs;
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var currentVersion = await GetVersionFromDbAsync(cancellationToken);

                if (currentVersion != _lastKnownVersion)
                {
                    _logger.LogInformation("Settings version changed ({OldVersion} → {NewVersion}). Reloading cache...",
                        _lastKnownVersion, currentVersion);

                    await _cacheService.LoadAllAsync(cancellationToken);
                    _lastKnownVersion = currentVersion;

                    _logger.LogInformation("Settings cache reloaded successfully");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to poll settings version. Keeping current cache and retrying next tick.");
            }
        }
    }

    private async Task<long> GetVersionFromDbAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
        return await repository.GetVersionAsync(cancellationToken);
    }
}
