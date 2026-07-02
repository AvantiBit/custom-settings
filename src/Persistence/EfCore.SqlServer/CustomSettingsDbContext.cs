using Avantibit.Optimizely.CustomSettings.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Avantibit.Optimizely.CustomSettings.Persistence.EfCore.SqlServer
{
    /// <summary>
    /// EF Core DbContext for custom settings storage.
    /// </summary>
    public class CustomSettingsDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomSettingsDbContext"/> class.
        /// </summary>
        /// <param name="options">The options for this context.</param>
        public CustomSettingsDbContext(DbContextOptions<CustomSettingsDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the DbSet for custom settings entities.
        /// </summary>
        public DbSet<SettingsEntity> Settings { get; set; } = null!;

        /// <summary>
        /// Gets or sets the DbSet for settings version tracking.
        /// </summary>
        public DbSet<SettingsVersionEntity> SettingsVersion { get; set; } = null!;

        /// <summary>
        /// Configures the model for the custom settings database.
        /// </summary>
        /// <param name="modelBuilder">The model builder to configure.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SettingsEntity>(entity =>
            {
                entity.ToTable("CustomSettings");

                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.SettingsType, e.SiteId, e.LanguageCode })
                    .IsUnique()
                    .HasDatabaseName("Settings_Composite")
                    .HasFilter("[SiteId] IS NOT NULL AND [LanguageCode] IS NOT NULL");

                // Enforce uniqueness for rows where SiteId is null and LanguageCode is not null.
                entity.HasIndex(e => new { e.SettingsType, e.LanguageCode })
                    .IsUnique()
                    .HasDatabaseName("Settings_Composite_NullSite")
                    .HasFilter("[SiteId] IS NULL AND [LanguageCode] IS NOT NULL");

                // Enforce uniqueness for rows where SiteId is not null and LanguageCode is null.
                entity.HasIndex(e => new { e.SettingsType, e.SiteId })
                    .IsUnique()
                    .HasDatabaseName("Settings_Composite_NullLang")
                    .HasFilter("[SiteId] IS NOT NULL AND [LanguageCode] IS NULL");

                // Enforce uniqueness for global/language-neutral rows where both are null.
                entity.HasIndex(e => e.SettingsType)
                    .IsUnique()
                    .HasDatabaseName("Settings_Composite_NullBoth")
                    .HasFilter("[SiteId] IS NULL AND [LanguageCode] IS NULL");

                entity.Property(e => e.SettingsType)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.LanguageCode)
                    .HasMaxLength(10);

                entity.Property(e => e.JsonData)
                    .IsRequired();

                entity.Property(e => e.Version)
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<SettingsVersionEntity>(entity =>
            {
                entity.ToTable("CustomSettingsVersion");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Version)
                    .IsConcurrencyToken();

                entity.HasData(new SettingsVersionEntity { Id = 1, Version = 0 });
            });
        }
    }
}
