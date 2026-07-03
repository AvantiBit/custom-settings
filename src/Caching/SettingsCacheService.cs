using Avantibit.Optimizely.CustomSettings.Attributes;
using Avantibit.Optimizely.CustomSettings.Configuration;
using Avantibit.Optimizely.CustomSettings.Discovery;
using Avantibit.Optimizely.CustomSettings.Infrastructure;
using Avantibit.Optimizely.CustomSettings.Persistence.Abstractions;
using Avantibit.Optimizely.CustomSettings.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Avantibit.Optimizely.CustomSettings.Caching;

/// <summary>
/// Pre-populated settings cache that loads all settings at startup and supports atomic full-reload.
/// </summary>
public class SettingsCacheService : ISettingsCacheService
{
    private volatile Dictionary<SettingsCacheKey, object> _cache = new();
    private volatile string? _masterLanguage;
    private static readonly ConcurrentDictionary<Type, List<PropertyInfo>> _fallbackPropsCache = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISettingsDiscoveryService _discoveryService;
    private readonly ILogger<SettingsCacheService> _logger;
    private int _hitCount;
    private int _missCount;

    public SettingsCacheService(
        IServiceScopeFactory scopeFactory,
        ISettingsDiscoveryService discoveryService,
        ILogger<SettingsCacheService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public T Get<T>(Guid? siteId, string? languageCode) where T : class, new()
    {
        var key = SettingsCacheKey.Create<T>(siteId, languageCode);
        var snapshot = _cache;

        if (snapshot.TryGetValue(key, out var value))
        {
            Interlocked.Increment(ref _hitCount);
            return CloneSettings((T)value);
        }

        // On a cache miss for a non-master language, synthesize an instance whose
        // [FallbackToMasterLanguage] properties are filled from the master entry.
        var masterLang = _masterLanguage;
        if (!string.IsNullOrEmpty(masterLang) &&
            !string.Equals(languageCode, masterLang, StringComparison.OrdinalIgnoreCase))
        {
            var masterKey = SettingsCacheKey.Create<T>(siteId, masterLang);
            if (snapshot.TryGetValue(masterKey, out var masterValue))
            {
                var props = GetOrComputeFallbackProperties<T>();
                if (props.Count > 0)
                {
                    var synthesized = new T();
                    foreach (var prop in props)
                    {
                        try
                        {
                            var masterVal = prop.GetValue(masterValue);
                            if (!SettingsValueHelper.IsNullOrDefault(masterVal))
                                prop.SetValue(synthesized, masterVal);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to apply fallback for property {PropertyName} on {SettingsType}",
                                prop.Name, typeof(T).Name);
                        }
                    }
                    Interlocked.Increment(ref _missCount);
                    return synthesized;
                }
            }
        }

        Interlocked.Increment(ref _missCount);
        return new T();
    }

    public async Task LoadAllAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
        var languageResolver = scope.ServiceProvider.GetRequiredService<ILanguageContextResolver>();

        var entities = await repository.GetAllAsync(cancellationToken);
        var masterLanguage = languageResolver.GetMasterLanguage();
        _masterLanguage = masterLanguage;
        var knownTypes = BuildTypeMap();
        var newCache = new Dictionary<SettingsCacheKey, object>();

        // First pass: deserialize all entities into the new cache
        foreach (var entity in entities)
        {
            if (!knownTypes.TryGetValue(entity.SettingsType, out var settingsType))
            {
                _logger.LogDebug("Skipping unknown settings type {SettingsType} during cache load", entity.SettingsType);
                continue;
            }

            try
            {
                var deserialized = JsonSerializer.Deserialize(entity.JsonData, settingsType, CustomSettingsJsonOptions.Default);
                if (deserialized != null)
                {
                    var key = new SettingsCacheKey(entity.SettingsType, entity.SiteId, entity.LanguageCode);
                    newCache[key] = deserialized;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                    entity.SettingsType, entity.SiteId, entity.LanguageCode);
            }
        }

        // Second pass: apply [FallbackToMasterLanguage] for non-master language entries
        ApplyFallbacks(newCache, masterLanguage, knownTypes);

        // Atomic swap
        _cache = newCache;

        _logger.LogInformation("Settings cache loaded with {Count} entries", newCache.Count);
    }

    public CacheStatistics GetStatistics()
    {
        var hits = _hitCount;
        var misses = _missCount;
        var total = hits + misses;
        var hitRatio = total > 0 ? (double)hits / total : 0;

        return new CacheStatistics(
            TotalEntries: _cache.Count,
            HitCount: hits,
            MissCount: misses,
            HitRatio: hitRatio);
    }

    private Dictionary<string, Type> BuildTypeMap()
    {
        var map = new Dictionary<string, Type>();
        foreach (var group in _discoveryService.GetAllGroups())
        {
            var fullName = group.SettingsType.FullName;
            if (fullName != null)
            {
                map[fullName] = group.SettingsType;
            }
        }
        return map;
    }

    private void ApplyFallbacks(Dictionary<SettingsCacheKey, object> cache, string masterLanguage, Dictionary<string, Type> knownTypes)
    {
        var fallbackProps = GetFallbackProperties(knownTypes);

        // Group cache entries by SettingsType once (avoids repeated linear scans)
        var entriesByType = cache
            .GroupBy(kv => kv.Key.SettingsType)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (type, props) in fallbackProps)
        {
            if (props.Count == 0) continue;

            var typeName = type.FullName!;
            if (!entriesByType.TryGetValue(typeName, out var entriesForType))
                continue;

            foreach (var (key, value) in entriesForType)
            {
                if (string.Equals(key.LanguageCode, masterLanguage, StringComparison.OrdinalIgnoreCase))
                    continue;

                var masterKey = new SettingsCacheKey(typeName, key.SiteId, masterLanguage);
                if (!cache.TryGetValue(masterKey, out var masterValue))
                    continue;

                foreach (var prop in props)
                {
                    try
                    {
                        var currentVal = prop.GetValue(value);
                        if (SettingsValueHelper.IsNullOrDefault(currentVal))
                        {
                            var masterVal = prop.GetValue(masterValue);
                            if (!SettingsValueHelper.IsNullOrDefault(masterVal))
                            {
                                prop.SetValue(value, masterVal);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to apply fallback for property {PropertyName} on {SettingsType}",
                            prop.Name, type.Name);
                    }
                }
            }
        }
    }

    private static Dictionary<Type, List<PropertyInfo>> GetFallbackProperties(Dictionary<string, Type> knownTypes)
    {
        var result = new Dictionary<Type, List<PropertyInfo>>();
        foreach (var (_, type) in knownTypes)
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<FallbackToMasterLanguageAttribute>() != null)
                .ToList();
            result[type] = props;
        }
        return result;
    }

    private static List<PropertyInfo> GetOrComputeFallbackProperties<T>() where T : class
    {
        return _fallbackPropsCache.GetOrAdd(typeof(T), static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<FallbackToMasterLanguageAttribute>() != null)
             .ToList());
    }

    private static T CloneSettings<T>(T value) where T : class, new()
    {
        var cloned = JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, CustomSettingsJsonOptions.Default), CustomSettingsJsonOptions.Default);
        if (cloned is null)
        {
            throw new InvalidOperationException($"Failed to clone cached settings instance of type {typeof(T).FullName}.");
        }

        return cloned;
    }
}
