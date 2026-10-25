using Avalonia.Controls;
using Avalonia.Input;
using TabStudio.App.ViewModels;
using TabStudio.App.Views.Extensions;

namespace TabStudio.App.Views;

/// <summary>
/// Extensions in the shell: the Extensions tab shows the Extensions window, and Ctrl+K, Ctrl+X inserts a snippet in
/// the editor, as in Visual Studio.
/// </summary>
public sealed partial class WorkspaceView
{
    /// <summary>Teaches the document tabs the Extensions window and registers its chord. Called by the constructor.</summary>
    private void InitializeExtensions()
    {
        var files = Editors.ViewFactory;
        Editors.ViewFactory = document => document is DocumentViewModel { ExtensionsPage: { } page }
            ? new ExtensionsView { DataContext = page }
            : files?.Invoke(document) ?? new Panel();
        AddEditorChord(Key.K, Key.X, () => _viewModel?.InsertSnippetCommand);
    }
}
