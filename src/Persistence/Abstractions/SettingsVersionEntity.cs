using System.ComponentModel.DataAnnotations;

namespace Avantibit.Optimizely.CustomSettings.Persistence.Abstractions
{
    public class SettingsVersionEntity
    {
        [Key]
        public int Id { get; set; }

        public long Version { get; set; }
    }
}
