using System.ComponentModel.DataAnnotations;
using Avantibit.Optimizely.CustomSettings.Attributes;

namespace AvantiBit.Optimizely.CustomSettings.Sample.Cms13.Features;

[SettingsGroup(
Name = "Custom Settings for integration",
Description = "Custom Settings for your API",
SortOrder = 100,
AuthorizationPolicy = "AdminsOnly")]
public class IntegrationCustomSettings
{

    [Display(Name = "Items Per Page")]
    [Range(1, 100)]
    public int ItemsPerPage { get; set; } = 10;

    [Display(Name = "Enable Analytics")]
    public bool EnableAnalytics { get; set; }

    // DateTime
    [Display(Name = "Launch Date")]
    public DateTime? LaunchDate { get; set; }

    // Guid
    [Display(Name = "Tracking ID")]
    public Guid TrackingId { get; set; } = Guid.NewGuid();

    // URL — using Uri type
    [Display(Name = "External API URL")]
    public Uri? ExternalApiUrl { get; set; }

    // URL — using string with [Url] attribute
    [Url]
    [Display(Name = "Website URL")]
    public string? WebsiteUrl { get; set; }

    // Multi-value string list
    [Display(Name = "Allowed Domains")]
    public IList<string> AllowedDomains { get; set; } = new List<string>();
}
