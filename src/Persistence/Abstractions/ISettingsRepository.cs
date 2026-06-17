namespace Avantibit.Optimizely.CustomSettings.Persistence.Abstractions
{
    /// <summary>
    /// Repository interface for persisting custom settings to storage.
    /// </summary>
    public interface ISettingsRepository
    {
        /// <summary>
        /// Gets settings by type, site, and language.
        /// </summary>
        Task<SettingsEntity?> GetAsync(string settingsType, Guid? siteId, string? languageCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves or updates settings.
        /// </summary>
        Task SaveAsync(SettingsEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes settings.
        /// </summary>
        Task<bool> DeleteAsync(string settingsType, Guid? siteId, string? languageCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all settings entities from the database.
        /// </summary>
        Task<List<SettingsEntity>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current settings version counter.
        /// </summary>
        Task<long> GetVersionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically increments the settings version counter.
        /// </summary>
        Task IncrementVersionAsync(CancellationToken cancellationToken = default);
    }
}