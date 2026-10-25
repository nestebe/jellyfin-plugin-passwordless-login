using Jellyfin.Plugin.PasswordlessLogin.Filters;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jellyfin.Plugin.PasswordlessLogin;

/// <summary>
/// Registers the plugin's services. Called by the server before the host is built.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<PublicUsersPasswordlessFilter>();
        serviceCollection.AddSingleton<IConfigureOptions<MvcOptions>, ConfigureMvcOptions>();
    }

    /// <summary>
    /// Adds <see cref="PublicUsersPasswordlessFilter"/> as a global MVC filter.
    /// </summary>
    internal sealed class ConfigureMvcOptions : IConfigureOptions<MvcOptions>
    {
        /// <inheritdoc />
        public void Configure(MvcOptions options)
        {
            options.Filters.AddService<PublicUsersPasswordlessFilter>();
        }
    }
}
