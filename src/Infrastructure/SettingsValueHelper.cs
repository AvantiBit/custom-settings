namespace Avantibit.Optimizely.CustomSettings.Infrastructure;

/// <summary>
/// Provides helper methods for evaluating settings property values.
/// </summary>
internal static class SettingsValueHelper
{
    /// <summary>
    /// Determines whether the specified value is null or the default value for its type.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is null or equals the default for its type; otherwise, false.</returns>
    public static bool IsNullOrDefault(object? value)
    {
        if (value is null)
            return true;

        var type = value.GetType();
        if (!type.IsValueType)
            return false;

        var defaultValue = Activator.CreateInstance(type);
        return value.Equals(defaultValue);
    }
}
