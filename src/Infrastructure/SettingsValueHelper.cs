namespace Avantibit.Optimizely.CustomSettings.Infrastructure;

/// <summary>
/// Shared utility methods for settings value inspection.
/// </summary>
internal static class SettingsValueHelper
{
    /// <summary>
    /// Determines whether a value is null or represents the default value for its type.
    /// Strings are considered default if null or whitespace.
    /// Value types are compared against <see cref="Activator.CreateInstance"/>.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>True if the value is null or default; otherwise, false.</returns>
    internal static bool IsNullOrDefault(object? value)
    {
        if (value is null)
            return true;

        var type = value.GetType();

        if (type.IsValueType)
            return value.Equals(Activator.CreateInstance(type));

        if (value is string s)
            return string.IsNullOrWhiteSpace(s);

        return false;
    }
}
