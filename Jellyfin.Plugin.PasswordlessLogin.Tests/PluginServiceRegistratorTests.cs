using System.Linq;
using Jellyfin.Plugin.PasswordlessLogin.Filters;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.PasswordlessLogin.Tests;

public class PluginServiceRegistratorTests
{
    [Fact]
    public void RegisterServices_AddsGlobalResolvableFilter()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton(Mock.Of<IUserManager>());
        services.AddSingleton(Mock.Of<INetworkManager>());

        new PluginServiceRegistrator().RegisterServices(services, Mock.Of<IServerApplicationHost>());

        using var provider = services.BuildServiceProvider();
        var filters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.Filters;
        var serviceFilter = Assert.IsType<ServiceFilterAttribute>(Assert.Single(filters));
        Assert.Equal(typeof(PublicUsersPasswordlessFilter), serviceFilter.ServiceType);
        Assert.IsType<PublicUsersPasswordlessFilter>(serviceFilter.CreateInstance(provider));
    }
}
