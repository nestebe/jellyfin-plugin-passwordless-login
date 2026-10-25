using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PasswordlessLogin.Configuration;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.PasswordlessLogin.Filters;

/// <summary>
/// Result filter that reports <see cref="UserDto.HasPassword"/> as <c>false</c> on <c>GET /Users/Public</c>
/// for passwordless users, so the web client logs them in with one click again.
/// </summary>
public class PublicUsersPasswordlessFilter : IAsyncResultFilter
{
    /// <summary>
    /// The <see cref="User.AuthenticationProviderId"/> of users authenticated by the built-in provider.
    /// </summary>
    internal const string DefaultAuthenticationProviderId = "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider";

    private const string UserControllerName = "User";
    private const string GetPublicUsersActionName = "GetPublicUsers";

    private readonly IUserManager _userManager;
    private readonly INetworkManager _networkManager;
    private readonly Func<PluginConfiguration?> _configurationProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicUsersPasswordlessFilter"/> class.
    /// </summary>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="networkManager">Instance of the <see cref="INetworkManager"/> interface.</param>
    public PublicUsersPasswordlessFilter(IUserManager userManager, INetworkManager networkManager)
        : this(userManager, networkManager, () => Plugin.Instance?.Configuration)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicUsersPasswordlessFilter"/> class.
    /// </summary>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="networkManager">Instance of the <see cref="INetworkManager"/> interface.</param>
    /// <param name="configurationProvider">Returns the current plugin configuration.</param>
    internal PublicUsersPasswordlessFilter(
        IUserManager userManager,
        INetworkManager networkManager,
        Func<PluginConfiguration?> configurationProvider)
    {
        _userManager = userManager;
        _networkManager = networkManager;
        _configurationProvider = configurationProvider;
    }

    /// <inheritdoc />
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (_configurationProvider()?.EnablePasswordlessLogin == true
            && IsGetPublicUsers(context.ActionDescriptor)
            && context.Result is ObjectResult { Value: IEnumerable<UserDto> users } objectResult
            && IsLocalRequest(context.HttpContext))
        {
            // The controller returns a lazy LINQ query: materialize it so the serializer sees our changes.
            var materialized = users.ToList();
            foreach (var dto in materialized)
            {
                if (IsPasswordless(_userManager.GetUserById(dto.Id)))
                {
#pragma warning disable CS0618 // Type or member is obsolete: the web client still reads it.
                    dto.HasPassword = false;
#pragma warning restore CS0618
                }
            }

            objectResult.Value = materialized;
        }

        return next();
    }

    private static bool IsGetPublicUsers(Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor actionDescriptor)
        => actionDescriptor is ControllerActionDescriptor descriptor
            && string.Equals(descriptor.ControllerName, UserControllerName, StringComparison.Ordinal)
            && string.Equals(descriptor.ActionName, GetPublicUsersActionName, StringComparison.Ordinal);

    private static bool IsPasswordless(User? user)
        => user is not null
            && string.Equals(user.AuthenticationProviderId, DefaultAuthenticationProviderId, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(user.Password);

    private bool IsLocalRequest(HttpContext httpContext)
        => httpContext.IsLocal() || _networkManager.IsInLocalNetwork(httpContext.GetNormalizedRemoteIP());
}
