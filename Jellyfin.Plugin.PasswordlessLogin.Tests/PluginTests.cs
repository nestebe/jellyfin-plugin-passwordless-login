using System;
using System.IO;
using Jellyfin.Plugin.PasswordlessLogin.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.PasswordlessLogin.Tests;

public sealed class PluginTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "passwordless-login-tests", Guid.NewGuid().ToString("N"));

    public PluginTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void OnUninstalling_DisablesOptionAndDeletesSettingsFile()
    {
        var plugin = CreatePlugin(new PluginConfiguration { EnablePasswordlessLogin = true });
        File.WriteAllText(plugin.ConfigurationFilePath, "<PluginConfiguration />");
        Assert.True(plugin.Configuration.EnablePasswordlessLogin);

        plugin.OnUninstalling();

        Assert.False(plugin.Configuration.EnablePasswordlessLogin);
        Assert.False(File.Exists(plugin.ConfigurationFilePath));
    }

    [Fact]
    public void OnUninstalling_WithoutSettingsFile_DoesNotThrow()
    {
        var plugin = CreatePlugin(new PluginConfiguration());

        plugin.OnUninstalling();

        Assert.False(plugin.Configuration.EnablePasswordlessLogin);
    }

    private Plugin CreatePlugin(PluginConfiguration storedConfiguration)
    {
        var paths = Mock.Of<IApplicationPaths>(p => p.PluginConfigurationsPath == _directory && p.PluginsPath == _directory);
        var xmlSerializer = new Mock<IXmlSerializer>();
        xmlSerializer
            .Setup(x => x.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
            .Returns(storedConfiguration);

        var plugin = new Plugin(paths, xmlSerializer.Object, NullLogger<Plugin>.Instance);
        plugin.SetAttributes(Path.Combine(_directory, "Jellyfin.Plugin.PasswordlessLogin.dll"), _directory, new Version(1, 0, 1, 0));
        return plugin;
    }
}
