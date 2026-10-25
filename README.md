# Jellyfin Passwordless Login plugin

[![Latest release](https://img.shields.io/github/v/release/nestebe/jellyfin-plugin-passwordless-login)](https://github.com/nestebe/jellyfin-plugin-passwordless-login/releases/latest)
![Jellyfin 12.3+](https://img.shields.io/badge/Jellyfin-12.3%2B-00a4dc)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)

Restores **one-click login** for accounts that have no password (a guest account on a shared TV, a kids
profile, ...) when the login screen is opened from your **local network**. Off by default.

## Install

1. In Jellyfin, open **Dashboard > Plugins > Manage repositories**
   (*Tableau de bord > Extensions > Gérer les dépôts*) and add a repository with this URL:

   ```text
   https://raw.githubusercontent.com/nestebe/jellyfin-plugin-passwordless-login/master/manifest.json
   ```

   Any name works, for example `Passwordless Login`.
2. In the plugin catalogue, open **Passwordless Login** (category *Authentication*) and click **Install**.
3. Restart Jellyfin.
4. Open the plugin's settings, read the [security warning](#security-trade-off), tick
   **Enable one-click login for passwordless users on the local network** and save. No restart needed.
5. Make sure the guest account has no password and is not hidden from the login screen
   (user settings > *Hide this user from login screens* unticked).

Updates show up in the catalogue like any other plugin. Requires Jellyfin **12.3 or newer**.

<details>
<summary>Manual install (without the repository)</summary>

1. Download `passwordless-login_<version>.zip` from the
   [latest release](https://github.com/nestebe/jellyfin-plugin-passwordless-login/releases/latest).
2. Stop Jellyfin.
3. Extract the zip into a new folder `PasswordlessLogin` inside the server's `plugins` directory:
   - Windows: `%LOCALAPPDATA%\jellyfin\plugins`
   - Debian / Ubuntu: `/var/lib/jellyfin/plugins`
   - Official Docker image: `/config/plugins`
4. Start Jellyfin, then enable the option as in step 4 above.

</details>

## Uninstall

Remove it from **Dashboard > Plugins** and restart Jellyfin. On removal the plugin turns one-click login off
immediately (without waiting for the restart) and deletes its settings file, so a later reinstall starts
disabled again. It never changes users, passwords or the database: nothing else is left behind.

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

## Development

Requires the .NET 10 SDK.

```bash
dotnet test
```

```bash
dotnet build -c Release
```

The DLL is written to `Jellyfin.Plugin.PasswordlessLogin/bin/Release/net10.0/`.

The plugin builds against `Jellyfin.Controller` **12.3.0**, the newest version published on NuGet (server
master is 13.0.0 but is not published; the APIs used are the same). The version is set by `JellyfinVersion`
in `Directory.Build.props`.

### Layout

Follows [jellyfin-plugin-template](https://github.com/jellyfin/jellyfin-plugin-template):

- `Jellyfin.Plugin.PasswordlessLogin/Plugin.cs`: plugin entry point, config page, cleanup on uninstall.
- `Jellyfin.Plugin.PasswordlessLogin/PluginServiceRegistrator.cs`: registers the filter as a global MVC filter
  (`IPluginServiceRegistrator`, run before the server's host is built).
- `Jellyfin.Plugin.PasswordlessLogin/Filters/PublicUsersPasswordlessFilter.cs`: the filter.
- `Jellyfin.Plugin.PasswordlessLogin/Configuration/`: plugin options and the dashboard page.
- `Jellyfin.Plugin.PasswordlessLogin.Tests/`: unit tests.
- `build.yaml`: release metadata (version, targetAbi, changelog).
- `manifest.json`: the plugin repository manifest that Jellyfin servers read.
- `release.ps1`: release script.

### Releasing a new version

Requires PowerShell 7 and the GitHub CLI, logged in.

1. Bump `version` (and `targetAbi` if needed) and write the `changelog` in `build.yaml`.
2. Run the release script. It runs the tests, zips the DLL, creates the GitHub release `v<version>` with the zip
   attached and adds the version, with the zip's MD5 checksum, to `manifest.json`:

   ```bash
   pwsh ./release.ps1
   ```

   Add `-DryRun` to only build, test and zip.
3. Commit and push `manifest.json` and `build.yaml`: servers see the update on their next catalogue refresh.

## License

GPL-3.0, like Jellyfin and the plugin template.
