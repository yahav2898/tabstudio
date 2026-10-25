using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.VisualTree;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Extensions.Manifests;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's extensions part: a real grammar extension found on Open VSX, installed with its hash and signature
/// checked, and a file of its language colored by it in the editor.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The extension the self test installs: VS Code's Perl grammar, small and without code.</summary>
    public static readonly ExtensionIdentifier GrammarExtension = new("vscode", "perl");

    /// <summary>The Perl file the self test opens, with a comment and a string to color.</summary>
    public const string PerlProgram = "# Greets the tablet.\nmy $name = \"Tablet\";\nprint \"Hello, $name!\\n\";\n";

    /// <summary>How long the editor may take to color the file.</summary>
    private static readonly TimeSpan ColoringTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Searches Open VSX for Perl, installs <see cref="GrammarExtension"/> from the Extensions window, opens a Perl
    /// file and checks that the editor colors its comment and its string with the extension's grammar.
    /// </summary>
    /// <param name="workspace">The open workspace, whose folder the Perl file is written to.</param>
    /// <param name="findEditor">Finds the editor that shows a document.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the file was colored, with a one-line summary.</returns>
    internal static async Task<SelfTestResult> InstallGrammarExtensionAsync(WorkspaceViewModel workspace, Func<DocumentViewModel, CodeEditorView?> findEditor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(findEditor);
        var clock = Stopwatch.StartNew();
        if (workspace.ExtensionsService is not { } service || workspace.OpenExtensions() is not { } page || workspace.Workspace is not { } folder)
        {
            return new SelfTestResult(false, "Extensions: the Extensions window is not available.", clock.Elapsed);
        }

        page.SearchText = "perl";
        await page.SearchNowCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
        if (page.BrowseResults.FirstOrDefault(i => i.Id == GrammarExtension) is not { } item)
        {
            return new SelfTestResult(false, $"Extensions: {service.Client.Registry.Host} did not find {GrammarExtension}: {page.BrowseMessage ?? page.ResultsHeading}", clock.Elapsed);
        }

        await page.InstallAsync(item).WaitAsync(cancellationToken);
        if (service.Store.Find(GrammarExtension) is not { } installed)
        {
            return new SelfTestResult(false, $"Extensions: {GrammarExtension} was not installed: {page.Notice}", clock.Elapsed);
        }

        var installedIn = clock.Elapsed;
        var path = Path.Combine(folder.RootDirectory, "selftest.pl");
        await File.WriteAllTextAsync(path, PerlProgram, cancellationToken);
        if (await workspace.OpenDocumentAsync(path) is not { } document)
        {
            return new SelfTestResult(false, "Extensions: selftest.pl did not open.", clock.Elapsed);
        }

        var comment = PerlProgram.IndexOf("Greets", StringComparison.Ordinal);
        var text = PerlProgram.IndexOf("Tablet\"", StringComparison.Ordinal);
        var deadline = clock.Elapsed + ColoringTimeout;
        (Color? Comment, Color? Text, Color? Plain) colors = default;
        while (clock.Elapsed < deadline)
        {
            if (findEditor(document) is { } editor)
            {
                colors = (editor.ForegroundAt(comment), editor.ForegroundAt(text), editor.TextForeground);
                if (colors.Comment is { } c && colors.Text is { } s && c != colors.Plain && s != colors.Plain && c != s)
                {
                    var summary = string.Create(
                        CultureInfo.InvariantCulture,
                        $"Extensions: installed {GrammarExtension} {installed.Version} from {service.Client.Registry.Host} ({installed.Source.Verification}) in {installedIn.TotalSeconds:0.0} s; {document.FileType.DisplayName} colored, comment {c}, string {s}");
                    return new SelfTestResult(true, summary, clock.Elapsed);
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return new SelfTestResult(false, $"Extensions: selftest.pl ({document.FileType.DisplayName}) was not colored: comment {colors.Comment}, string {colors.Text}, text {colors.Plain}", clock.Elapsed);
    }

    /// <summary>The editor that shows a document in the app's window.</summary>
    private static CodeEditorView? FindEditor(DocumentViewModel document)
    {
        Visual? root = Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            ISingleViewApplicationLifetime single => single.MainView,
            _ => null,
        };
        return root?.GetVisualDescendants().OfType<CodeEditorView>().FirstOrDefault(v => ReferenceEquals(v.DataContext, document) && v.IsEffectivelyVisible);
    }
}
