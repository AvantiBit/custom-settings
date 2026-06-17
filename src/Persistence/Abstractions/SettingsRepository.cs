using Avantibit.Optimizely.CustomSettings.Persistence.EfCore.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Avantibit.Optimizely.CustomSettings.Persistence.Abstractions
{
    /// <summary>
    /// Implementation of settings repository for persisting custom settings using Entity Framework Core.
    /// </summary>
    public class SettingsRepository : ISettingsRepository
    {
        private const int VersionIncrementRetryLimit = 3;
        private readonly CustomSettingsDbContext _context;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<SettingsRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        public SettingsRepository(
            CustomSettingsDbContext context,
            ILogger<SettingsRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Gets settings by type, site, and language.
        /// </summary>
        /// <param name="settingsType">The fully qualified type name of the settings.</param>
        /// <param name="siteId">The site ID.</param>
        /// <param name="languageCode">The language code.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The settings entity if found; otherwise, null.</returns>
        public async Task<SettingsEntity?> GetAsync(
            string settingsType,
            Guid? siteId,
            string? languageCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(settingsType))
            {
                throw new ArgumentException("Settings type cannot be null or empty.", nameof(settingsType));
            }

            try
            {
                return await _context.Settings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.SettingsType == settingsType &&
                             s.SiteId == siteId &&
                             s.LanguageCode == languageCode,
                        cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Database failure retrieving settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode}). Returning null to allow cached/default fallback.",
                    settingsType,
                    siteId,
                    languageCode);
                return null;
            }
        }

        public async Task SaveAsync(SettingsEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            try
            {
                if (_context.Database.IsRelational())
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                    await SaveSettingsEntityAsync(entity, cancellationToken);
                    await IncrementVersionAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await SaveSettingsEntityAsync(entity, cancellationToken);
                    await IncrementVersionAsync(cancellationToken);
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex,
                    "Database failure saving settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                    entity.SettingsType,
                    entity.SiteId,
                    entity.LanguageCode);
                throw new InvalidOperationException(
                    $"Failed to save settings {entity.SettingsType}. Please try again or contact support if the problem persists.",
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error saving settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                    entity.SettingsType,
                    entity.SiteId,
                    entity.LanguageCode);
                throw;
            }
        }

        /// <summary>
        /// Deletes settings.
        /// </summary>
        /// <param name="settingsType">The fully qualified type name of the settings.</param>
        /// <param name="siteId">The site ID.</param>
        /// <param name="languageCode">The language code.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if settings were deleted; otherwise, false.</returns>
        public async Task<bool> DeleteAsync(
            string settingsType,
            Guid? siteId,
            string? languageCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(settingsType))
            {
                throw new ArgumentException("Settings type cannot be null or empty.", nameof(settingsType));
            }

            try
            {
                if (_context.Database.IsRelational())
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                    var deleted = await DeleteSettingsEntityAsync(settingsType, siteId, languageCode, cancellationToken);
                    if (deleted)
                    {
                        await IncrementVersionAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return deleted;
                }

                var inMemoryDeleted = await DeleteSettingsEntityAsync(settingsType, siteId, languageCode, cancellationToken);
                if (inMemoryDeleted)
                {
                    await IncrementVersionAsync(cancellationToken);
                }

                return inMemoryDeleted;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex,
                    "Database failure deleting settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                    settingsType,
                    siteId,
                    languageCode);
                throw new InvalidOperationException(
                    $"Failed to delete settings {settingsType}. Please try again or contact support if the problem persists.",
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error deleting settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                    settingsType,
                    siteId,
                    languageCode);
                throw;
            }
        }

        public async Task<List<SettingsEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Settings
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<long> GetVersionAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SettingsVersion
                .Where(v => v.Id == 1)
                .Select(v => v.Version)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task IncrementVersionAsync(CancellationToken cancellationToken = default)
        {
            for (var attempt = 1; attempt <= VersionIncrementRetryLimit; attempt++)
            {
                var versionEntity = await _context.SettingsVersion.FindAsync(new object[] { 1 }, cancellationToken);
                if (versionEntity is null)
                {
                    throw new InvalidOperationException("Settings version row with Id=1 was not found.");
                }

                versionEntity.Version++;

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateConcurrencyException ex) when (attempt < VersionIncrementRetryLimit)
                {
                    _logger.LogWarning(
                        ex,
                        "Concurrent settings version update detected. Retrying attempt {Attempt} of {RetryLimit}.",
                        attempt + 1,
                        VersionIncrementRetryLimit);

                    foreach (var entry in ex.Entries)
                    {
                        entry.State = EntityState.Detached;
                    }
                }
            }

            throw new InvalidOperationException(
                $"Failed to increment settings version after {VersionIncrementRetryLimit} attempts due to repeated concurrency conflicts.");
        }

        private async Task SaveSettingsEntityAsync(SettingsEntity entity, CancellationToken cancellationToken)
        {
            var existing = await _context.Settings
                .FirstOrDefaultAsync(
                    s => s.SettingsType == entity.SettingsType &&
                         s.SiteId == entity.SiteId &&
                         s.LanguageCode == entity.LanguageCode,
                    cancellationToken);

            if (existing is not null)
            {
                existing.JsonData = entity.JsonData;
                existing.ModifiedUtc = DateTime.UtcNow;
                existing.Version++;
                _context.Entry(existing).State = EntityState.Modified;
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            entity.CreatedUtc = DateTime.UtcNow;
            entity.ModifiedUtc = DateTime.UtcNow;
            entity.Version = 1;
            await _context.Settings.AddAsync(entity, cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent request inserted the same key between our read and insert.
                // Detach the failed entity and fall back to updating the row that now exists.
                _context.Entry(entity).State = EntityState.Detached;

                existing = await _context.Settings.FirstOrDefaultAsync(
                    s => s.SettingsType == entity.SettingsType &&
                         s.SiteId == entity.SiteId &&
                         s.LanguageCode == entity.LanguageCode,
                    cancellationToken);

                if (existing is null)
                {
                    throw;
                }

                existing.JsonData = entity.JsonData;
                existing.ModifiedUtc = DateTime.UtcNow;
                existing.Version++;
                _context.Entry(existing).State = EntityState.Modified;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<bool> DeleteSettingsEntityAsync(
            string settingsType,
            Guid? siteId,
            string? languageCode,
            CancellationToken cancellationToken)
        {
            var entity = await _context.Settings
                .FirstOrDefaultAsync(
                    s => s.SettingsType == settingsType &&
                         s.SiteId == siteId &&
                         s.LanguageCode == languageCode,
                    cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.Settings.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
