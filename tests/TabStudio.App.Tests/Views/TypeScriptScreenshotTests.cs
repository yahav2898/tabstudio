using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// A TypeScript file in the IDE with the real typescript-language-server, which the CI job installs: the server's
/// semantic colors over TextMate's, inlay hints for parameter names and return types, and folding.
/// </summary>
public sealed class TypeScriptScreenshotTests : IDisposable
{
    private const string Program = """
        interface Person {
            name: string;
            age: number;
        }

        function greet(person: Person, punctuation: string) {
            return `Hello, ${person.name}${punctuation}`;
        }

        const ada: Person = { name: "Ada", age: 36 };
        console.log(greet(ada, "!"));
        """;

    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public TypeScriptScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Resolve("app")));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [RealServerAvaloniaFact("typescript-language-server")]
    public async Task Semantic_colors_inlay_hints_and_folds_from_the_server()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var folder = _temp.Resolve("web");
        var path = _temp.Write("web/main.ts", Program + "\n");
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        var document = await workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The file did not open.");
        var view = ActiveEditor(window);

        await UiTestHelpers.WaitUntilAsync(
            () => document.LanguageServer?.ServerState is LanguageServerState.Running && document.Classifications.Count > 0
                && view.InlayHints?.Hints.Count > 0 && view.Folding is not null,
            TimeSpan.FromSeconds(90),
            because: "semantic colors, inlay hints and folds");
        view.Editor.CaretOffset = OffsetOf(view.Editor, "greet(ada") + 2;
        await UiTestHelpers.WaitUntilAsync(() => view.ReferenceHighlights?.Highlights.Count == 2, TimeSpan.FromSeconds(20), because: "the highlights of greet");

        UiTestHelpers.SaveScreenshot(window, "lsp-14-typescript-hints");

        Assert.Contains(view.InlayHints!.Hints, h => h.Text.Contains("punctuation:", StringComparison.Ordinal));
        Assert.Contains(view.InlayHints.Hints, h => h.Text.Contains(": string", StringComparison.Ordinal));
        Assert.Contains(document.Classifications, c => c.Classification == "class name");
        Assert.Contains(view.Folding!.AllFoldings, f => document.Document.GetLineByOffset(f.StartOffset).LineNumber == 6);
        Assert.Equal("TypeScript ready", workspace.LanguageServerText);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }
}
