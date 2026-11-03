using System.Xml.Linq;

namespace TabStudio.App.Tests.Platform;

/// <summary>
/// What the IDE's Android activity handles itself rather than being recreated for. A recreated activity loses the IDE's
/// window; DeX's display size (density) and Android's Bold text (font weight) did that. The APK's own manifest is
/// checked too, by build/ci/android-verify-apk.sh in the Android job.
/// </summary>
public sealed class AndroidActivityTests
{
    [Theory]
    [InlineData("density")]
    [InlineData("fontWeightAdjustment")]
    [InlineData("fontScale")]
    [InlineData("screenSize")]
    [InlineData("uiMode")]
    public void The_IDE_activity_is_not_recreated_when_the_display_size_or_text_weight_changes(string change)
    {
        var manifest = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "TabStudio.Android", "Properties", "AndroidManifest.xml"));
        XNamespace android = "http://schemas.android.com/apk/res/android";

        var activity = manifest.Descendants("activity").SingleOrDefault(a => (string?)a.Attribute(android + "name") == "com.tabstudio.ide.MainActivity");

        Assert.NotNull(activity);
        Assert.Contains(change, ((string?)activity.Attribute(android + "configChanges") ?? string.Empty).Split('|'));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TabStudio.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository was not found.");
    }
}
