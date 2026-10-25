using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PasswordlessLogin.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether passwordless users are advertised as such on the
    /// public user list for local network requests, enabling one-click login in the web client.
    /// Disabled by default.
    /// </summary>
    public bool EnablePasswordlessLogin { get; set; }
}
