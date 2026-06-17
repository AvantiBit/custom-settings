using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Avantibit.Optimizely.CustomSettings.Persistence.Abstractions
{
    /// <summary>
    /// Entity for storing custom settings with composite key.
    /// </summary>
    public class SettingsEntity
    {
        /// <summary>
        /// Gets or sets the unique identifier for the settings entity.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified type name of the settings class.
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string SettingsType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the site identifier, or null for global settings.
        /// </summary>
        public Guid? SiteId { get; set; }

        /// <summary>
        /// Gets or sets the language code, or null for language-neutral settings.
        /// </summary>
        [MaxLength(10)]
        public string? LanguageCode { get; set; }

        /// <summary>
        /// Gets or sets the JSON-serialized settings data.
        /// </summary>
        [Required]
        public string JsonData { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the settings were created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the settings were last modified.
        /// </summary>
        public DateTime ModifiedUtc { get; set; }

        /// <summary>
        /// Gets or sets the version number for optimistic concurrency control.
        /// </summary>
        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}