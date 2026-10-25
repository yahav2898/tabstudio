using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Extensions;

/// <summary>The self test's extensions step, against the fake registry's recording of open-vsx.org.</summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class ExtensionSelfTestTests
{
    [AvaloniaFact]
    public async Task The_self_test_installs_the_Perl_grammar_and_sees_a_Perl_file_colored()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();

        var result = await SelfTest.InstallGrammarExtensionAsync(
            fixture.Workspace,
            document => window.GetVisualDescendants().OfType<CodeEditorView>().FirstOrDefault(v => ReferenceEquals(v.DataContext, document) && v.IsEffectivelyVisible),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Contains("(Signature)", result.Message, StringComparison.Ordinal);
        Assert.Contains("Perl colored", result.Message, StringComparison.Ordinal);
        UiTestHelpers.SaveScreenshot(window, "ext-11-perl-from-open-vsx");
        window.Close();
    }
}
