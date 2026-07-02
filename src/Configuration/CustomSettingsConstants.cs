namespace Avantibit.Optimizely.CustomSettings.Configuration;

/// <summary>
/// Constants used across the CustomSettings package.
/// </summary>
public static class CustomSettingsConstants
{
    /// <summary>
    /// The name of the default authorization policy applied to settings groups
    /// that do not declare an explicit <c>AuthorizationPolicy</c>.
    /// This policy requires the user to be a CMS editor or administrator.
    /// </summary>
    public const string DefaultPolicyName = "CustomSettings.DefaultAccess";

    /// <summary>
    /// Optimizely CMS role that grants administrator-level access (legacy/UI role).
    /// </summary>
    internal const string RoleWebAdmins = "WebAdmins";

    /// <summary>
    /// Optimizely CMS role that grants editor-level access (legacy/UI role).
    /// </summary>
    internal const string RoleWebEditors = "WebEditors";

    /// <summary>
    /// Optimizely CMS 13 role that grants administrator-level access.
    /// </summary>
    internal const string RoleCmsAdmins = "CmsAdmins";

    /// <summary>
    /// Optimizely CMS 13 role that grants editor-level access.
    /// </summary>
    internal const string RoleCmsEditors = "CmsEditors";
}
