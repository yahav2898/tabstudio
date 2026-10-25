using TabStudio.Compilation.Uno;
using TabStudio.Core.Diagnostics;
using TabStudio.Packages.Uno;
using TabStudio.Testing;
using TabStudio.Uno.Templates;

namespace TabStudio.Uno.Tests;

/// <summary>
/// The Windows App SDK's WinUI 3 Blank App against nuget.org: Uno Platform's packages stand in for the Windows App
/// SDK's, Uno's XAML generator compiles it, and what Uno does not implement or only Windows has is reported clearly.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class WinUIAppTests(NuGetFixture nuget) : IDisposable
{
    private const string Name = "Notes";

    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Uno_Platforms_packages_replace_the_Windows_App_SDKs()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.WinUIAppId, Name);

        var replaced = Assert.Single(app.Restore.Diagnostics, d => d.Id == UnoPackageDiagnosticIds.WindowsAppSdkReplaced);
        Assert.Equal(DiagnosticSeverityLevel.Info, replaced.Severity);
        Assert.Contains("Microsoft.WindowsAppSDK 2.5.1", replaced.Message, StringComparison.Ordinal);
        Assert.Contains("Uno Platform 6.7", replaced.Message, StringComparison.Ordinal);
        Assert.Contains(app.Restore.CompileAssemblies, path => path.EndsWith("Uno.UI.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(app.Restore.CompileAssemblies, path => path.Contains("microsoft.windowsappsdk", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(app.Restore.Analyzers, path => path.Contains("microsoft.windowsappsdk", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(app.Restore.Analyzers, path => path.EndsWith("Uno.UI.SourceGenerators.dll", StringComparison.Ordinal));
    }

    [Fact]
    public async Task It_builds_and_its_XAML_says_where_Uno_does_not_implement_WinUI()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.WinUIAppId, Name);
        var window = app.PathOf("MainWindow.xaml");
        var lines = app.Read("MainWindow.xaml").ReplaceLineEndings("\n").Split('\n');

        var build = await app.BuildAsync();
        var live = await app.Host.GetDocumentDiagnosticsAsync(window);

        Assert.Equal(0, build.ErrorCount);
        foreach (var (element, type) in new[] { ("<MicaBackdrop", "MicaBackdrop"), ("<TitleBar ", "TitleBar") })
        {
            var line = Array.FindIndex(lines, l => l.Contains(element, StringComparison.Ordinal)) + 1;
            var warning = Assert.Single(build.Diagnostics, d => d.Id == UnoCompatibility.NotImplementedId && d.Message.Contains(type, StringComparison.Ordinal) && d.Location?.FilePath == window);
            Assert.Equal(DiagnosticSeverityLevel.Warning, warning.Severity);
            Assert.Equal(line, warning.Location!.Line);
            Assert.Equal(type, File.ReadAllText(window).ReplaceLineEndings("\n").Substring(warning.Location.Start, warning.Location.Length));
            Assert.Contains("Windows Bridge", warning.Message, StringComparison.Ordinal);
            Assert.Contains(live, d => d.Id == UnoCompatibility.NotImplementedId && d.Location?.Line == line);
        }
    }

    [Fact]
    public async Task A_Windows_only_API_is_reported_as_Windows_only_instead_of_missing()
    {
        using var app = await UnoAppWorkspace.CreateAsync(nuget, _temp, UnoTemplates.WinUIAppId, Name);
        var code = app.Read("MainWindow.xaml.cs").Replace(
            "RootFrame.Navigate(typeof(MainPage));",
            "RootFrame.Navigate(typeof(MainPage));\n        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);",
            StringComparison.Ordinal);
        app.Save("MainWindow.xaml.cs", code);

        var live = await app.Host.GetDocumentDiagnosticsAsync(app.PathOf("MainWindow.xaml.cs"));

        var error = Assert.Single(live, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal(UnoCompatibility.WindowsOnlyId, error.Id);
        Assert.Contains("WinRT.Interop", error.Message, StringComparison.Ordinal);
        Assert.Contains("Windows Bridge", error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
