using Microsoft.AspNetCore.Mvc.Razor;

namespace Avantibit.Optimizely.CustomSettings.Infrastructure;

/// <summary>
/// Expands view locations to include custom settings views from the /Optimizely/Views directory.
/// </summary>
public sealed class CustomSettingsViewLocationExpander : IViewLocationExpander
{
    /// <summary>
    /// Populates route values for the view location expansion.
    /// </summary>
    /// <param name="context">The view location expander context.</param>
    public void PopulateValues(ViewLocationExpanderContext context){ }

    /// <summary>
    /// Expands view locations to include custom paths for Optimizely custom settings views.
    /// </summary>
    /// <param name="context">The view location expander context.</param>
    /// <param name="viewLocations">The existing view locations.</param>
    /// <returns>The expanded view locations including custom paths.</returns>
    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        var customLocations = new[]
        {
            "/Optimizely/Views/{1}/{0}.cshtml",
            "/Optimizely/Views/Shared/{0}.cshtml"
        };

        return customLocations.Concat(viewLocations);
    }
}