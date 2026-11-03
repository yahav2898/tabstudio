using System.Text.RegularExpressions;

namespace TabStudio.App.Tests.Platform;

/// <summary>
/// What the IDE's Android activity handles itself rather than being recreated for. A recreated activity loses the IDE's
/// window; DeX's display size (density) and Android's Bold text (font weight) did that. The APK's own manifest is
/// checked too, by build/ci/android-verify-apk.sh in the Android job.
/// </summary>
public sealed class AndroidActivityTests
{
    [Theory]
    [InlineData("Density")]
    [InlineData("FontWeightAdjustment")]
    [InlineData("FontScale")]
    [InlineData("ScreenSize")]
    [InlineData("UiMode")]
    public void The_IDE_activity_is_not_recreated_when_the_display_size_or_text_weight_changes(string change)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "TabStudio.Android", "MainActivity.cs"));
        var attribute = Regex.Match(source, @"\[Activity\((?<body>.*?)\)\]\s*(#pragma[^\n]*\n\s*)?public sealed partial class MainActivity", RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        Assert.True(attribute.Success, "MainActivity's [Activity] attribute was not found.");
        Assert.Contains("ConfigChanges." + change, attribute.Groups["body"].Value, StringComparison.Ordinal);
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
