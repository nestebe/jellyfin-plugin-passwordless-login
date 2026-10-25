# Jellyfin Passwordless Login plugin

Restores **one-click login** for accounts that have no password (a guest account on a shared TV, a kids
profile, ...) when the login screen is opened from your **local network**.

## Why

Before Jellyfin 12, `GET /Users/Public` reported `HasPassword = false` for accounts without a password, and
the web client logged those users in as soon as their card was clicked. Since
[jellyfin/jellyfin#14950](https://github.com/jellyfin/jellyfin/pull/14950) the server always reports
`HasPassword = true`, so the login screen asks for a password that does not exist. Upstream decided not to
bring this back ([jellyfin/jellyfin#18175](https://github.com/jellyfin/jellyfin/issues/18175)), so this plugin
does it as an opt-in.

## What it does

The plugin adds a global MVC result filter that only touches the response of `GET /Users/Public`. For each
user in that response it sets `HasPassword = false` when **all** of the following are true:

- the plugin option is enabled (it is **off** by default);
- the request comes from the local network (same machine, or an address in Jellyfin's
  *LAN networks* as decided by Jellyfin's own network manager);
- the user is authenticated by the built-in provider (`DefaultAuthenticationProvider`), so LDAP, SSO and other
  external providers are never affected;
- the user has no password stored.

Nothing else changes: authentication itself is still done by Jellyfin. A passwordless user could already log
in by submitting an empty password; this plugin only tells the web client that it may do so for them.
Changing the option takes effect immediately, without a restart.

## Security trade-off

Read this before enabling the option.

- **Anyone on your local network** (house guests, devices on your Wi-Fi, a compromised IoT device) can open a
  passwordless account with a single click. That was already possible by typing the user name with an empty
  password, but it is now trivial.
- The public login screen **reveals which visible accounts have no password**, which is exactly the
  information upstream decided to stop exposing.
- Requests from outside your LAN are never changed. If Jellyfin sits **behind a reverse proxy**, add the
  proxy to *Dashboard > Networking > Known proxies*: otherwise Jellyfin may see every request as coming from
  the proxy's address and treat remote visitors as local (if the proxy is on your LAN).
- Only use passwordless accounts for users with limited rights: never an administrator, no content deletion,
  parental controls as needed. Hide accounts you do not want listed (*Hide this user from login screens*).

If any of this is not acceptable for your setup, keep the option off, or give the account a password and use
*Quick Connect* or the client's "remember me" instead.

## Install

### From a build

1. Build the plugin (see below) or take `Jellyfin.Plugin.PasswordlessLogin.dll` from a release.
2. Stop Jellyfin.
3. Create a folder `PasswordlessLogin_1.0.0.0` in the server's `plugins` directory
   (for example `%LOCALAPPDATA%\jellyfin\plugins` on Windows, `/var/lib/jellyfin/plugins` on Debian/Ubuntu,
   `/config/plugins` in the official Docker image) and copy the DLL into it.
4. Start Jellyfin. The plugin shows up in *Dashboard > Plugins* as **Passwordless Login**.
5. Open its settings, read the warning, tick **Enable one-click login...** and save.

### Compatibility

| Plugin | Built against | Server |
|--------|---------------|--------|
| 1.0.0.0 | `Jellyfin.Controller` 12.3.0, `net10.0` (`targetAbi` 12.3.0.0) | 12.3 or newer (tested on 13.0.0 / master) |

Server master is 13.0.0, but that version of `Jellyfin.Controller` is not published on NuGet, so the plugin
builds against 12.3.0, the newest published package. The APIs it uses are the same on master. To build against
another version:

```bash
dotnet build -c Release -p:JellyfinVersion=12.3.0
```

## Build and test

Requires the .NET 10 SDK.

```bash
dotnet build -c Release
```

```bash
dotnet test
```

The DLL is written to `Jellyfin.Plugin.PasswordlessLogin/bin/Release/net10.0/`.

## Layout

Follows [jellyfin-plugin-template](https://github.com/jellyfin/jellyfin-plugin-template):

- `Jellyfin.Plugin.PasswordlessLogin/Plugin.cs`: plugin entry point and config page registration.
- `Jellyfin.Plugin.PasswordlessLogin/PluginServiceRegistrator.cs`: registers the filter as a global MVC filter
  (`IPluginServiceRegistrator`, run before the server's host is built).
- `Jellyfin.Plugin.PasswordlessLogin/Filters/PublicUsersPasswordlessFilter.cs`: the filter.
- `Jellyfin.Plugin.PasswordlessLogin/Configuration/`: plugin options and the dashboard page.
- `Jellyfin.Plugin.PasswordlessLogin.Tests/`: unit tests (local vs remote, password vs none, external provider,
  option off, lazy result materialization, DI registration).
- `build.yaml`: metadata for [jprm](https://github.com/oddstr13/jellyfin-plugin-repository-manager) if you
  want to publish it in a plugin repository.

## License

GPL-3.0, like Jellyfin and the plugin template.
