using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.Compilation.Features;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// IntelliSense commands that work across files: Go to Definition (F12), Go to Implementation (Ctrl+F12), Find All
/// References (Shift+F12), Rename (F2), and applying the edits of renames and code actions to every affected file.
/// Commands that only need the editor, such as completion and Quick Info, are sent to the active editor.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private ReferencesViewModel? _references;

    /// <summary>The Find All References panel in the bottom dock.</summary>
    public ReferencesViewModel References => _references ??= new ReferencesViewModel(NavigateToAsync);

    /// <summary>
    /// Applies the edits of a rename or code action: open documents change in their editor, where Undo reverts them;
    /// closed files change on disk. New and removed files reload the workspace.
    /// </summary>
    /// <param name="edits">The edits for each affected file.</param>
    /// <returns>The number of files that changed.</returns>
    public async Task<int> ApplyDocumentEditsAsync(IReadOnlyList<DocumentEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var changed = 0;
        var reload = false;
        foreach (var edit in edits)
        {
            try
            {
                switch (edit.Kind)
                {
                    case DocumentEditKind.Modified:
                        await ApplyToFileAsync(edit);
                        break;
                    case DocumentEditKind.Added:
                        Directory.CreateDirectory(Path.GetDirectoryName(edit.FilePath)!);
                        await File.WriteAllTextAsync(edit.FilePath, edit.Edits.Count > 0 ? edit.Edits[0].NewText : string.Empty);
                        reload = true;
                        break;
                    case DocumentEditKind.Removed:
                        await RemoveFileAsync(edit.FilePath);
                        reload = true;
                        break;
                }

                changed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusText = $"Could not change {Path.GetFileName(edit.FilePath)}: {ex.Message}";
            }
        }

        if (reload)
        {
            await ReloadWorkspaceAsync();
        }
        else
        {
            RefreshWpfXamlAfterEdits(edits);
        }

        return changed;
    }

    /// <summary>Shows a message in the status bar, such as why an action could not be applied.</summary>
    /// <param name="message">The message.</param>
    public void ShowStatus(string message) => StatusText = message;

    /// <summary>Go to Definition (F12): opens the declaration of the symbol at the caret.</summary>
    [RelayCommand]
    private async Task GoToDefinitionAsync()
    {
        if (CaretContext() is not { } context)
        {
            return;
        }

        var (document, features, offset) = context;

        DefinitionResult? definition;
        try
        {
            definition = await features.FindDefinitionAsync(document.FilePath, offset, BackgroundToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (definition is null)
        {
            StatusText = "Cannot navigate to the symbol under the caret.";
        }
        else if (definition.IsInMetadata)
        {
            StatusText = definition.AssemblyName is { } assembly
                ? $"{definition.SymbolDisplay} is defined in {assembly}"
                : $"{definition.SymbolDisplay} is defined in a compiled assembly";
        }
        else if (definition.Locations.Count == 1)
        {
            var location = definition.Locations[0];
            await NavigateToAsync(location.FilePath, location.Line, location.Column);
        }
        else
        {
            ShowLocations($"'{definition.SymbolDisplay}' definitions", definition.Locations);
        }
    }

    /// <summary>Go to Implementation (Ctrl+F12): opens the implementation, or lists them when there are several.</summary>
    [RelayCommand]
    private async Task GoToImplementationAsync()
    {
        if (CaretContext() is not { } context)
        {
            return;
        }

        var (document, features, offset) = context;

        IReadOnlyList<DefinitionResult> implementations;
        try
        {
            implementations = await features.FindImplementationsAsync(document.FilePath, offset, BackgroundToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var locations = implementations.SelectMany(i => i.Locations).ToList();
        if (locations.Count == 0)
        {
            StatusText = "The symbol has no implementations.";
        }
        else if (locations.Count == 1)
        {
            await NavigateToAsync(locations[0].FilePath, locations[0].Line, locations[0].Column);
        }
        else
        {
            ShowLocations($"'{WordAt(document, offset)}' implementations", locations);
        }
    }

    /// <summary>Find All References (Shift+F12): lists every reference in the References panel.</summary>
    [RelayCommand]
    private async Task FindAllReferencesAsync()
    {
        if (CaretContext() is not { } context || Workspace is not { } workspace)
        {
            return;
        }

        var (document, features, offset) = context;

        var title = $"'{WordAt(document, offset)}' references";
        References.BeginSearch(title);
        SelectPanel(BottomPanel.References);
        IReadOnlyList<ReferenceItem> items;
        try
        {
            items = await features.FindReferencesAsync(document.FilePath, offset, BackgroundToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        References.Show(title, items, workspace.RootDirectory);
    }

    /// <summary>
    /// Rename (F2, Ctrl+R, Ctrl+R): asks for the new name with the current one selected, then renames the symbol and
    /// every reference in the solution. Conflicts, such as a name that is already taken, are shown first.
    /// </summary>
    [RelayCommand]
    private async Task RenameSymbolAsync()
    {
        if (CaretContext() is not { } context)
        {
            return;
        }

        var (document, features, offset) = context;

        SyncOpenDocuments();
        RenameTarget target;
        try
        {
            target = await features.CanRenameAsync(document.FilePath, offset, BackgroundToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!target.CanRename)
        {
            StatusText = target.Reason ?? "This element cannot be renamed.";
            return;
        }

        var newName = await Prompt.AskAsync("Rename", $"Rename '{target.CurrentName}' and every reference to it.", "Rename", target.CurrentName);
        if (newName is null || newName == target.CurrentName)
        {
            document.RequestAction(EditorAction.Focus);
            return;
        }

        SyncOpenDocuments();
        var versions = Documents.Select(d => (Document: d, Edits: d.EditCount)).ToList();
        RenameResult result;
        try
        {
            result = await features.GetRenameEditsAsync(document.FilePath, offset, newName, null, BackgroundToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!result.Success)
        {
            StatusText = result.Error ?? $"'{target.CurrentName}' cannot be renamed to '{newName}'.";
            return;
        }

        if (versions.Any(v => v.Document.EditCount != v.Edits))
        {
            StatusText = "The code changed while renaming. Try again.";
            return;
        }

        if (result.Conflicts.Count > 0)
        {
            var first = result.Conflicts[0];
            var more = result.Conflicts.Count > 1 ? $" and {result.Conflicts.Count - 1} more" : string.Empty;
            var proceed = await Prompt.ConfirmAsync(
                "Rename conflicts",
                $"Renaming to '{newName}' causes {first.Id}: {first.Message}{more}. Rename anyway?",
                "Rename anyway");
            if (!proceed)
            {
                document.RequestAction(EditorAction.Focus);
                return;
            }
        }

        if (!await PreviewServerRenameAsync(document, target.CurrentName, newName, result.Edits))
        {
            document.RequestAction(EditorAction.Focus);
            return;
        }

        var files = await ApplyDocumentEditsAsync(result.Edits);
        StatusText = files == 1
            ? $"Renamed '{target.CurrentName}' to '{newName}'"
            : $"Renamed '{target.CurrentName}' to '{newName}' in {files} files";
        document.RequestAction(EditorAction.Focus);
    }

    /// <summary>Quick Actions and Refactorings (Ctrl+.): opens the light bulb menu in the active editor.</summary>
    [RelayCommand]
    private void QuickActions() => ActiveDocument?.RequestAction(EditorAction.QuickActions);

    /// <summary>List Members (Ctrl+J): opens the completion list in the active editor.</summary>
    [RelayCommand]
    private void ListMembers() => ActiveDocument?.RequestAction(EditorAction.ListMembers);

    /// <summary>Complete Word (Ctrl+Space): completes the word, or lists the choices when there are several.</summary>
    [RelayCommand]
    private void CompleteWord() => ActiveDocument?.RequestAction(EditorAction.CompleteWord);

    /// <summary>Parameter Info (Ctrl+Shift+Space): shows the signature of the call at the caret.</summary>
    [RelayCommand]
    private void ParameterInfo() => ActiveDocument?.RequestAction(EditorAction.ParameterInfo);

    /// <summary>Quick Info (Ctrl+K, Ctrl+I): shows the tooltip of the symbol at the caret.</summary>
    [RelayCommand]
    private void QuickInfo() => ActiveDocument?.RequestAction(EditorAction.QuickInfo);

    /// <summary>
    /// Prepares IntelliSense in the background once a workspace is open, so the first completion list and the first
    /// light bulb do not wait for Roslyn to index.
    /// </summary>
    private void StartIntelliSenseWarmUp()
    {
        if (_language is not { } language || FindWarmUpFile(language) is not { } file)
        {
            return;
        }

        var token = BackgroundToken;
        _ = Task.Run(async () =>
        {
            try
            {
                await language.Features.WarmUpAsync(file, token);
            }
            catch (OperationCanceledException)
            {
                // The workspace closed.
            }
        }, token);
    }

    /// <summary>A C# file to warm up with: an open one, or the first source file of a C# project.</summary>
    private string? FindWarmUpFile(ILanguageService language)
    {
        if (Documents.FirstOrDefault(d => d.HasLanguageService && d.FileType.IsCSharp) is { } open)
        {
            return open.FilePath;
        }

        if (Workspace is not { } workspace)
        {
            return null;
        }

        var folders = workspace.Projects.Select(p => p.DirectoryPath).ToList();
        return workspace.Projects
            .Where(p => p.Language == ProjectLanguage.CSharp)
            .SelectMany(p => ProjectSourceFiles.Enumerate(p, folders))
            .FirstOrDefault(language.ContainsDocument);
    }

    private CancellationToken BackgroundToken => _background?.Token ?? new CancellationToken(canceled: true);

    /// <summary>The active C# document, its IntelliSense and the caret, with every open document's text sent first.</summary>
    private (DocumentViewModel Document, ILanguageFeatures Features, int Offset)? CaretContext()
    {
        if (ActiveDocument is not { Features: { } features } document)
        {
            StatusText = ActiveDocument is null ? "Open a code file first." : NoIntelliSenseReason(ActiveDocument);
            return null;
        }

        SyncOpenDocuments();
        return (document, features, Math.Clamp(document.CaretOffset, 0, document.Document.TextLength));
    }

    private void SyncOpenDocuments()
    {
        foreach (var document in Documents)
        {
            document.SyncText();
        }
    }

    private async Task ApplyToFileAsync(DocumentEdit edit)
    {
        if (Documents.FirstOrDefault(d => string.Equals(d.FilePath, edit.FilePath, StringComparison.Ordinal)) is { } open)
        {
            open.ApplyEdits(edit.Edits);
            return;
        }

        var file = await TextFile.ReadAsync(edit.FilePath);
        var text = TextEdits.Apply(file.Text, edit.Edits);
        await TextFile.WriteAsync(edit.FilePath, text, file.Encoding);
        _language?.UpdateText(edit.FilePath, text);
    }

    private async Task RemoveFileAsync(string path)
    {
        await CloseDocumentsUnderAsync(path, save: false);
        if (!File.Exists(path))
        {
            return;
        }

        var trash = Path.Combine(_paths.Trash, DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(trash);
        File.Move(path, Path.Combine(trash, Path.GetFileName(path)), overwrite: true);
    }

    /// <summary>Lists source locations in the References panel, with the text of each line.</summary>
    private void ShowLocations(string title, IReadOnlyList<SourceLocation> locations)
    {
        var items = locations
            .Select(l => new ReferenceItem(l.FilePath, l.Start, l.Length, l.Line, l.Column, LineText(l.FilePath, l.Line), IsDefinition: true))
            .ToList();
        References.Show(title, items, Workspace?.RootDirectory ?? string.Empty);
        SelectPanel(BottomPanel.References);
    }

    private string LineText(string path, int line)
    {
        if (Documents.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.Ordinal)) is { } open)
        {
            return line >= 1 && line <= open.Document.LineCount ? open.Document.GetText(open.Document.GetLineByNumber(line)) : string.Empty;
        }

        try
        {
            return File.ReadLines(path).Skip(line - 1).FirstOrDefault() ?? string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>The identifier at or just before an offset, for panel titles.</summary>
    private static string WordAt(DocumentViewModel document, int offset)
    {
        var text = document.Document;
        var start = Math.Clamp(offset, 0, text.TextLength);
        var end = start;
        while (start > 0 && CompletionMatcher.IsIdentifierCharacter(text.GetCharAt(start - 1)))
        {
            start--;
        }

        while (end < text.TextLength && CompletionMatcher.IsIdentifierCharacter(text.GetCharAt(end)))
        {
            end++;
        }

        return end > start ? text.GetText(start, end - start) : "symbol";
    }
}
