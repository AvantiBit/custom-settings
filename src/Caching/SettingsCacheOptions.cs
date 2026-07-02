namespace Avantibit.Optimizely.CustomSettings.Caching
{
    public class SettingsCacheOptions
    {
        public int PollingIntervalSeconds { get; set; } = 10;
        public int MaxJitterSeconds { get; set; } = 2;
    }
}
