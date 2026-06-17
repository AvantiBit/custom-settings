namespace Avantibit.Optimizely.CustomSettings.Caching;

/// <summary>
/// Service for managing a pre-populated in-memory settings cache synchronized across servers.
/// </summary>
public interface ISettingsCacheService
{
    /// <summary>
    /// Gets a defensive copy of settings from the in-memory cache. Returns a default instance if not found.
    /// </summary>
    T Get<T>(Guid? siteId, string? languageCode) where T : class, new();

    /// <summary>
    /// Loads all settings from the database into the cache, atomically swapping the entire cache contents.
    /// </summary>
    Task LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Cache statistics for monitoring and diagnostics.
/// </summary>
public record CacheStatistics(
    int TotalEntries,
    int HitCount,
    int MissCount,
    double HitRatio);
