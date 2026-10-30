using System.Xml.Linq;
using TabStudio.App.Services;
using TabStudio.App.Services.Agent;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

/// <summary>
/// Tokens stay on the device and with its user: agent.json, with the Claude API key and MCP tokens, was readable by
/// every user of a desktop, and Android's Auto Backup copied the State folder, GitHub token included, to the cloud.
/// </summary>
public sealed class SecretStorageTests
{
    [Fact]
    public void The_agents_settings_are_readable_by_their_owner_only()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        new AgentSettingsStore(paths).Save(AgentSettings.Default with { ApiKey = "sk-ant-secret" });

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(paths.State, "agent.json")));
    }

    [Fact]
    public void Android_backups_leave_out_the_state_folder()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TabStudio.slnx")))
        {
            root = root.Parent;
        }

        var android = Path.Combine(root!.FullName, "src", "TabStudio.Android");
        XNamespace ns = "http://schemas.android.com/apk/res/android";
        var application = XDocument.Load(Path.Combine(android, "Properties", "AndroidManifest.xml")).Descendants("application").Single();
        Assert.Equal("@xml/backup_rules", (string?)application.Attribute(ns + "fullBackupContent"));
        Assert.Equal("@xml/data_extraction_rules", (string?)application.Attribute(ns + "dataExtractionRules"));

        foreach (var rules in new[] { "backup_rules.xml", "data_extraction_rules.xml" })
        {
            var excluded = XDocument.Load(Path.Combine(android, "Resources", "xml", rules)).Descendants("exclude")
                .Select(e => ((string?)e.Attribute("domain"), (string?)e.Attribute("path")))
                .ToList();
            Assert.Contains(("file", "TabStudio/State/"), excluded);
        }
    }
}
