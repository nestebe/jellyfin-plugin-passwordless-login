using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PasswordlessLogin.Configuration;
using Jellyfin.Plugin.PasswordlessLogin.Filters;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

#pragma warning disable CS0618 // HasPassword is obsolete upstream but still read by the web client.

namespace Jellyfin.Plugin.PasswordlessLogin.Tests;

public class PublicUsersPasswordlessFilterTests
{
    private const string LdapProviderId = "Jellyfin.Plugin.LDAP_Auth.LdapAuthenticationProviderPlugin";

    private static readonly IPAddress _lanAddress = IPAddress.Parse("192.168.1.20");
    private static readonly IPAddress _serverAddress = IPAddress.Parse("192.168.1.10");
    private static readonly IPAddress _wanAddress = IPAddress.Parse("203.0.113.7");

    private readonly Mock<IUserManager> _userManager = new();
    private readonly Mock<INetworkManager> _networkManager = new();
    private PluginConfiguration? _configuration = new() { EnablePasswordlessLogin = true };

    public PublicUsersPasswordlessFilterTests()
    {
        _networkManager.Setup(n => n.IsInLocalNetwork(_lanAddress)).Returns(true);
        _networkManager.Setup(n => n.IsInLocalNetwork(_wanAddress)).Returns(false);
    }

    [Fact]
    public async Task LanRequest_PasswordlessDefaultUser_HasPasswordFalse()
    {
        var dto = AddUser("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: null);

        await RunAsync(_lanAddress, dto);

        Assert.False(dto.HasPassword);
    }

    [Fact]
    public async Task SameMachineRequest_PasswordlessDefaultUser_HasPasswordFalse()
    {
        var dto = AddUser("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: null);

        // Remote == local address: HttpContext.IsLocal() is true even if the network manager says otherwise.
        await RunAsync(_serverAddress, dto);

        Assert.False(dto.HasPassword);
    }

    [Fact]
    public async Task EmptyPassword_TreatedAsPasswordless()
    {
        var dto = AddUser("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: string.Empty);

        await RunAsync(_lanAddress, dto);

        Assert.False(dto.HasPassword);
    }

    [Fact]
    public async Task RemoteRequest_PasswordlessDefaultUser_HasPasswordStaysTrue()
    {
        var dto = AddUser("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: null);

        await RunAsync(_wanAddress, dto);

        Assert.True(dto.HasPassword);
    }

    [Fact]
    public async Task UserWithPassword_HasPasswordStaysTrue()
    {
        var dto = AddUser("admin", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: "$PBKDF2-SHA512$iterations=210000$AA$BB");

        await RunAsync(_lanAddress, dto);

        Assert.True(dto.HasPassword);
    }

    [Fact]
    public async Task LdapLikeProvider_HasPasswordStaysTrue()
    {
        // External providers keep no local password, yet they do require one.
        var dto = AddUser("ldapuser", LdapProviderId, password: null);

        await RunAsync(_lanAddress, dto);

        Assert.True(dto.HasPassword);
    }

    [Fact]
    public async Task OptionDisabled_HasPasswordStaysTrue()
    {
        _configuration = new PluginConfiguration();
        var dto = AddUser("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: null);

        await RunAsync(_lanAddress, dto);

        Assert.True(dto.HasPassword);
    }

    [Fact]
    public async Task NoConfiguration_HasPasswordStaysTrue()
    {
        _configuration = null;
        var dto = AddUser("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: null);

        await RunAsync(_lanAddress, dto);

        Assert.True(dto.HasPassword);
    }

    [Fact]
    public async Task UnknownUser_HasPasswordStaysTrue()
    {
        var dto = new UserDto { Id = Guid.NewGuid(), Name = "ghost" };

        await RunAsync(_lanAddress, dto);

        Assert.True(dto.HasPassword);
    }

    [Fact]
    public async Task OtherAction_IsNotModified()
    {
        var dto = AddUser("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: null);

        await RunAsync(_lanAddress, dto, actionName: "GetUsers");

        Assert.True(dto.HasPassword);
    }

    [Fact]
    public async Task MixedUsers_OnlyPasswordlessDefaultUsersChanged()
    {
        var guest = AddUser("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: null);
        var admin = AddUser("admin", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, password: "hash");
        var ldap = AddUser("ldapuser", LdapProviderId, password: null);

        await RunAsync(_lanAddress, guest, admin, ldap);

        Assert.False(guest.HasPassword);
        Assert.True(admin.HasPassword);
        Assert.True(ldap.HasPassword);
    }

    [Fact]
    public async Task LazyResult_IsMaterializedSoChangesReachTheSerializer()
    {
        var user = new User("guest", PublicUsersPasswordlessFilter.DefaultAuthenticationProviderId, "reset");
        _userManager.Setup(u => u.GetUserById(user.Id)).Returns(user);

        // Mimics UserController.Get(): a Select() that builds new DTOs on every enumeration.
        var lazy = Enumerable.Range(0, 1).Select(_ => new UserDto { Id = user.Id, Name = user.Username });
        var result = new OkObjectResult(lazy);

        await CreateFilter().OnResultExecutionAsync(CreateContext(_lanAddress, result, "GetPublicUsers"), Next);

        var users = Assert.IsAssignableFrom<IReadOnlyList<UserDto>>(result.Value);
        Assert.False(Assert.Single(users).HasPassword);
    }

    [Fact]
    public async Task NonObjectResult_PassesThrough()
    {
        var called = false;
        var context = CreateContext(_lanAddress, new UnauthorizedResult(), "GetPublicUsers");

        await CreateFilter().OnResultExecutionAsync(context, () =>
        {
            called = true;
            return Next();
        });

        Assert.True(called);
        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    private static Task<ResultExecutedContext> Next() => Task.FromResult<ResultExecutedContext>(null!);

    private UserDto AddUser(string name, string providerId, string? password)
    {
        var user = new User(name, providerId, "Jellyfin.Server.Implementations.Users.DefaultPasswordResetProvider")
        {
            Password = password
        };
        _userManager.Setup(u => u.GetUserById(user.Id)).Returns(user);

        return new UserDto { Id = user.Id, Name = name };
    }

    private PublicUsersPasswordlessFilter CreateFilter()
        => new(_userManager.Object, _networkManager.Object, () => _configuration);

    private async Task RunAsync(IPAddress remoteAddress, params UserDto[] users)
        => await RunAsync(remoteAddress, users, "GetPublicUsers").ConfigureAwait(true);

    private async Task RunAsync(IPAddress remoteAddress, UserDto dto, string actionName)
        => await RunAsync(remoteAddress, [dto], actionName).ConfigureAwait(true);

    private async Task RunAsync(IPAddress remoteAddress, UserDto[] users, string actionName)
    {
        var context = CreateContext(remoteAddress, new OkObjectResult(users.AsEnumerable()), actionName);
        await CreateFilter().OnResultExecutionAsync(context, Next).ConfigureAwait(true);
    }

    private static ResultExecutingContext CreateContext(IPAddress remoteAddress, IActionResult result, string actionName)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.LocalIpAddress = _serverAddress;
        httpContext.Connection.RemoteIpAddress = remoteAddress;

        var actionDescriptor = new ControllerActionDescriptor
        {
            ControllerName = "User",
            ActionName = actionName
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        return new ResultExecutingContext(actionContext, new List<IFilterMetadata>(), result, controller: new object());
    }
}
