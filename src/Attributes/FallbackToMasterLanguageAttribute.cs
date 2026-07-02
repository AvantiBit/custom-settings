namespace Avantibit.Optimizely.CustomSettings.Attributes;

/// <summary>
/// Marks a property to fallback to the master/default language value when the current language value is null or not set.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class FallbackToMasterLanguageAttribute : Attribute
{

}