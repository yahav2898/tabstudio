using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>Find in Files (Ctrl+Shift+F): text found in every file of the workspace, listed in the References panel.</summary>
public sealed partial class WorkspaceViewModel
{
    private CancellationTokenSource? _findInFiles;

    /// <summary>
    /// Asks what to find, with the word at the caret filled in, then lists every line of the workspace's files that
    /// holds it, letter case ignored, in the References panel. Open documents are searched as they are in the
    /// editor. Tapping a match opens it.
    /// </summary>
    [RelayCommand]
    private async Task FindInFilesAsync()
    {
        if (Workspace is not { } workspace)
        {
            StatusText = "Open a solution or folder first.";
            return;
        }

        var word = ActiveDocument is { } active ? WordAt(active, active.CaretOffset) : string.Empty;
        var text = await Prompt.AskAsync("Find in Files", "Find text in every file of the solution's folder. Letter case is ignored.", "Find All", word);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        text = text.Split('\n')[0].TrimEnd('\r');
        await SearchInFilesAsync(workspace.RootDirectory, text);
    }

    /// <summary>Runs a Find in Files search and shows its matches.</summary>
    internal async Task SearchInFilesAsync(string root, string text)
    {
        _findInFiles?.Cancel();
        _findInFiles?.Dispose();
        var search = _findInFiles = CancellationTokenSource.CreateLinkedTokenSource(BackgroundToken);
        var title = $"'{text}' in files";
        References.BeginSearch(title);
        SelectPanel(BottomPanel.References);
        var open = Documents.GroupBy(d => d.FilePath, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Document.Text, StringComparer.Ordinal);
        IReadOnlyList<ReferenceItem> matches;
        try
        {
            matches = await Task.Run(() => FileTextSearch.Search(root, text, matchCase: false, open, search.Token), search.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (search.IsCancellationRequested)
        {
            return;
        }

        References.Show(title, matches, root, ("match", "matches"), $"'{text}' was not found in the solution's folder.");
        if (matches.Count >= FileTextSearch.MaximumResults)
        {
            StatusText = $"Find in Files stopped at {FileTextSearch.MaximumResults} matches. Search for something longer to see them all.";
        }
    }
}
