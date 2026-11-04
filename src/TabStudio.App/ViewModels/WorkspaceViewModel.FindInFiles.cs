using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Find in Files (Ctrl+Shift+F) and Replace in Files (Ctrl+Shift+H): text found in every file of the workspace, with
/// Match case, Match whole word and regular expressions, listed in the Find Results panel, apart from Find All
/// References' list.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private CancellationTokenSource? _findInFiles;
    private FindResultsViewModel? _findResults;

    /// <summary>The Find Results panel in the bottom dock: the Find in Files form and its matches.</summary>
    public FindResultsViewModel FindResults => _findResults ??= new FindResultsViewModel(NavigateToAsync, RunFindInFilesAsync, RunReplaceInFilesAsync);

    /// <summary>Whether Find Results is the visible bottom panel.</summary>
    public bool IsFindResultsSelected => SelectedPanel == BottomPanel.FindResults;

    /// <summary>
    /// Find in Files (Ctrl+Shift+F): opens the Find Results panel with the word at the caret to find, and puts the
    /// keyboard in its find box. Enter or Find All lists every match.
    /// </summary>
    [RelayCommand]
    private void FindInFiles() => OpenFindInFiles(replace: false);

    /// <summary>Replace in Files (Ctrl+Shift+H): as Find in Files, with the replace box and Replace All.</summary>
    [RelayCommand]
    private void ReplaceInFiles() => OpenFindInFiles(replace: true);

    private void OpenFindInFiles(bool replace)
    {
        if (Workspace is null)
        {
            StatusText = "Open a solution or folder first.";
            return;
        }

        var find = FindResults;
        find.IsReplaceMode = replace;
        if (ActiveDocument is { } active && TextToFind(active) is { Length: > 0 } text)
        {
            find.FindText = text;
        }

        SelectPanel(BottomPanel.FindResults);
        find.RequestFocus();
    }

    /// <summary>What Find in Files starts with: the word at the caret, or nothing when the caret is not on one.</summary>
    private static string TextToFind(DocumentViewModel document)
    {
        var text = document.Document;
        var start = Math.Clamp(document.CaretOffset, 0, text.TextLength);
        var end = start;
        while (start > 0 && CompletionMatcher.IsIdentifierCharacter(text.GetCharAt(start - 1)))
        {
            start--;
        }

        while (end < text.TextLength && CompletionMatcher.IsIdentifierCharacter(text.GetCharAt(end)))
        {
            end++;
        }

        return end > start ? text.GetText(start, end - start) : string.Empty;
    }

    /// <summary>Runs a Find in Files search for plain text, letter case ignored, and shows its matches.</summary>
    internal Task SearchInFilesAsync(string root, string text) => SearchInFilesAsync(root, new FindInFilesQuery(text));

    /// <summary>Runs a Find in Files search and shows its matches in Find Results.</summary>
    internal async Task SearchInFilesAsync(string root, FindInFilesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var results = FindResults.Results;
        var title = $"'{query.Text}' in files";
        SelectPanel(BottomPanel.FindResults);
        if (!FileTextMatcher.TryCreate(query, out var matcher, out var error))
        {
            CancelFindInFiles();
            results.ShowMessage(title, error);
            StatusText = error;
            return;
        }

        var token = StartFindInFiles().Token;
        results.BeginSearch(title);
        var open = OpenTextDocuments();
        IReadOnlyList<ReferenceItem> matches;
        try
        {
            matches = await Task.Run(() => FileTextSearch.Search(root, matcher, open, token), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (RegexMatchTimeoutException)
        {
            if (!token.IsCancellationRequested)
            {
                results.ShowMessage(title, TimeoutMessage);
            }

            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        results.Show(title, matches, root, ("match", "matches"), $"'{query.Text}' was not found in the solution's folder.");
        if (matches.Count >= FileTextSearch.MaximumResults)
        {
            StatusText = $"Find in Files stopped at {FileTextSearch.MaximumResults} matches. Search for something longer to see them all.";
        }
    }

    /// <summary>
    /// Replace All in Files: replaces every match in the solution's folder and lists what was replaced. An open
    /// document changes in its editor, as one step Undo reverts, keeping its unsaved changes; every other file changes
    /// on disk in its own encoding and line endings.
    /// </summary>
    /// <returns>What was replaced, or <see langword="null"/> when nothing ran.</returns>
    internal async Task<ReplaceInFilesResult?> ReplaceInFilesAsync(string root, FindInFilesQuery query, string replacement)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(replacement);
        var results = FindResults.Results;
        var title = $"'{query.Text}' replaced with '{replacement}'";
        SelectPanel(BottomPanel.FindResults);
        if (!FileTextMatcher.TryCreate(query, out var matcher, out var error))
        {
            results.ShowMessage(title, error);
            StatusText = error;
            return null;
        }

        // A search still running would list its matches over what was replaced. A replace is not cancelled by a
        // search started after it: it runs to the end, so what it wrote to disk is always reported.
        CancelFindInFiles();
        var token = BackgroundToken;
        results.BeginSearch(title);
        var open = OpenTextDocuments();
        var leftAlone = Documents
            .Where(d => !IsTextDocument(d) && !open.ContainsKey(d.FilePath))
            .Select(d => d.FilePath)
            .ToHashSet(StringComparer.Ordinal);
        ReplaceInFilesResult result;
        try
        {
            result = await Task.Run(() => FileTextSearch.ReplaceAsync(root, matcher, replacement, open, leftAlone, token), token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            results.ShowMessage(title, TimeoutMessage);
            return null;
        }

        foreach (var edit in result.OpenDocumentEdits)
        {
            ApplyReplacements(edit, open[edit.FilePath], matcher, replacement);
        }

        if (_language is { } language)
        {
            foreach (var (path, text) in result.WrittenFiles.Where(f => language.ContainsDocument(f.Path)))
            {
                language.UpdateText(path, text);
            }
        }

        var summary = result.Replacements == 0
            ? $"'{query.Text}' was not found in the solution's folder."
            : $"{Count(result.Replacements, "replacement", "replacements")} in {Count(result.Files, "file", "files")}";
        results.Show(title, result.Matches, root, ("replacement", "replacements"), summary);
        StatusText = result.Failures.Count == 0
            ? summary
            : $"{summary}; {Count(result.Failures.Count, "file", "files")} could not be written: {result.Failures[0]}";
        return result;
    }

    /// <summary>What a regular expression that runs too long says.</summary>
    private const string TimeoutMessage = "The regular expression took too long on a file. Make it more specific.";

    private Task RunFindInFilesAsync(FindInFilesQuery query)
    {
        if (Workspace is not { } workspace)
        {
            StatusText = "Open a solution or folder first.";
            return Task.CompletedTask;
        }

        return SearchInFilesAsync(workspace.RootDirectory, query);
    }

    private Task RunReplaceInFilesAsync(FindInFilesQuery query, string replacement)
    {
        if (Workspace is not { } workspace)
        {
            StatusText = "Open a solution or folder first.";
            return Task.CompletedTask;
        }

        return ReplaceInFilesAsync(workspace.RootDirectory, query, replacement);
    }

    /// <summary>Stops the search still running, and starts the token of a new one.</summary>
    private CancellationTokenSource StartFindInFiles()
    {
        CancelFindInFiles();
        return _findInFiles = CancellationTokenSource.CreateLinkedTokenSource(BackgroundToken);
    }

    private void CancelFindInFiles()
    {
        _findInFiles?.Cancel();
        _findInFiles?.Dispose();
        _findInFiles = null;
    }

    /// <summary>The text of every open text document, by full path, as it is in its editor.</summary>
    private Dictionary<string, string> OpenTextDocuments() =>
        Documents.Where(IsTextDocument)
            .GroupBy(d => d.FilePath, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Document.Text, StringComparer.Ordinal);

    /// <summary>Whether a document's editor is its file's text, rather than a notebook, a diff, a pull request or a page.</summary>
    private static bool IsTextDocument(DocumentViewModel document) =>
        document is { IsNotebook: false, IsCiLog: false, IsPullRequest: false, IsReview: false, IsComparison: false, IsExtensionsPage: false, IsDeviceMatrix: false };

    /// <summary>
    /// Applies Replace All's edits to an open document as one undoable step. When the text changed while the files
    /// were searched, the replacements are worked out again on the text as it is now.
    /// </summary>
    private void ApplyReplacements(DocumentEdit edit, string searchedText, FileTextMatcher matcher, string replacement)
    {
        if (Documents.FirstOrDefault(d => IsTextDocument(d) && string.Equals(d.FilePath, edit.FilePath, StringComparison.Ordinal)) is not { } document)
        {
            return;
        }

        var text = document.Document.Text;
        try
        {
            var edits = string.Equals(text, searchedText, StringComparison.Ordinal) ? edit.Edits : matcher.Replacements(text, replacement);
            TextEdits.Apply(document.Document, edits);
        }
        catch (RegexMatchTimeoutException)
        {
            StatusText = $"{document.FileName} was not changed: {TimeoutMessage}";
        }
    }
}
