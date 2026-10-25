using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using TabStudio.App.Editing;

namespace TabStudio.App.Views;

/// <summary>
/// The editor of a file a language server owns: TextMate colors with the server's semantic colors over them,
/// squiggles, inlay hints, folding and formatting as the user types. IntelliSense is shared with C#.
/// </summary>
public sealed partial class CodeEditorView
{
    private static readonly IBrush FoldMarkerBrush = new ImmutableSolidColorBrush(Color.Parse("#9AA1AE"));
    private static readonly IBrush FoldMarkerBackground = new ImmutableSolidColorBrush(Color.Parse("#1B1D23"));

    private InlayHintGenerator? _inlayHints;
    private FoldingManager? _folding;

    /// <summary>The inlay hints shown, for tests; <see langword="null"/> without a language server.</summary>
    internal InlayHintGenerator? InlayHints => _inlayHints;

    /// <summary>The folds, once the language server sent folding ranges; for tests.</summary>
    internal FoldingManager? Folding => _folding;

    /// <summary>Whether the completion list's popup is shown, not only open in its view model; for tests.</summary>
    internal bool IsCompletionShown => _completionPopup?.IsOpen == true;

    /// <summary>Colors and squiggles from the language server, over the TextMate colors. Called by the constructor.</summary>
    private void InstallLanguageServerRendering(out ClassificationColorizer colorizer, out DiagnosticRenderer diagnostics)
    {
        colorizer = new ClassificationColorizer();
        diagnostics = new DiagnosticRenderer();

        // After TextMate's transformer, so a name the server knows more about gets its color.
        _editor.TextArea.TextView.LineTransformers.Add(colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Add(diagnostics);
    }

    /// <summary>Inlay hints, folding and formatting on typing. Called once, by the constructor.</summary>
    private void InitializeLanguageServer()
    {
        if (!_document.HasLanguageServer)
        {
            return;
        }

        ApplyClassifications();
        ApplyDiagnostics();
        _inlayHints = new InlayHintGenerator();
        _editor.TextArea.TextView.ElementGenerators.Add(_inlayHints);
        _document.DecorationsChanged += OnServerDecorationsChanged;
        _document.Document.Changed += OnDocumentChangedForServerDecorations;
        _editor.TextArea.TextEntered += OnTextEnteredForServer;
        ApplyServerDecorations();
    }

    /// <summary>Removes what <see cref="InitializeLanguageServer"/> added. Called by <see cref="Dispose"/>.</summary>
    private void DisposeLanguageServer()
    {
        if (_inlayHints is null)
        {
            return;
        }

        _document.DecorationsChanged -= OnServerDecorationsChanged;
        _document.Document.Changed -= OnDocumentChangedForServerDecorations;
        _editor.TextArea.TextEntered -= OnTextEnteredForServer;
        _editor.TextArea.TextView.ElementGenerators.Remove(_inlayHints);
        if (_folding is not null)
        {
            FoldingManager.Uninstall(_folding);
            _folding = null;
        }
    }

    /// <summary>The context menu's commands only language servers answer. Called while the menu is built.</summary>
    private void AddLanguageServerMenuItems(Avalonia.Controls.ContextMenu menu)
    {
        if (!_document.HasLanguageServer)
        {
            return;
        }

        var declaration = new Avalonia.Controls.MenuItem { Header = "Go To Declaration" };
        declaration.Click += (_, _) => _ = Workspace?.GoToDeclarationCommand.ExecuteAsync(null);
        var typeDefinition = new Avalonia.Controls.MenuItem { Header = "Go To Type Definition" };
        typeDefinition.Click += (_, _) => _ = Workspace?.GoToTypeDefinitionCommand.ExecuteAsync(null);
        menu.Items.Add(declaration);
        menu.Items.Add(typeDefinition);
    }

    /// <summary>
    /// The breakpoint menu's hit count and logpoint, for files a debug adapter runs; they go before Delete Breakpoint.
    /// </summary>
    private void AddAdapterBreakpointItems(Avalonia.Controls.MenuFlyout menu, string path, int line, ViewModels.BreakpointViewModel? breakpoint)
    {
        if (_document.Debugger is not ViewModels.IEditorBreakpointOptions options || !options.HasBreakpointOptions(path))
        {
            return;
        }

        if (breakpoint is null)
        {
            menu.Items.Add(CreateMenuItem("Insert Logpoint...", () => _ = options.EditLogMessageAsync(path, line)));
            return;
        }

        var index = Math.Max(0, menu.Items.Count - 1);
        menu.Items.Insert(index, CreateMenuItem("Hit Count...", () => _ = options.EditHitCountAsync(path, line)));
        menu.Items.Insert(index + 1, CreateMenuItem(breakpoint.LogMessage is null ? "Log a Message..." : "Edit Log Message...", () => _ = options.EditLogMessageAsync(path, line)));
    }

    private void OnServerDecorationsChanged(object? sender, EventArgs e) => ApplyServerDecorations();

    private void OnDocumentChangedForServerDecorations(object? sender, DocumentChangeEventArgs e) => _inlayHints?.OnDocumentChanged(e);

    private void OnTextEnteredForServer(object? sender, TextInputEventArgs e)
    {
        if (e.Text is { Length: 1 } text && _completion?.IsCommitting != true)
        {
            _ = _document.FormatOnTypeAsync(_editor.CaretOffset, text[0]);
        }
    }

    private void ApplyServerDecorations()
    {
        _inlayHints?.SetHints(_document.InlayHints);
        if (_document.FoldingRanges.Count > 0 && _folding is null)
        {
            _editor.SetValue(FoldingMargin.FoldingMarkerBrushProperty, FoldMarkerBrush);
            _editor.SetValue(FoldingMargin.FoldingMarkerBackgroundBrushProperty, FoldMarkerBackground);
            _editor.SetValue(FoldingMargin.SelectedFoldingMarkerBrushProperty, Brushes.White);
            _editor.SetValue(FoldingMargin.SelectedFoldingMarkerBackgroundBrushProperty, FoldMarkerBackground);
            _folding = FoldingManager.Install(_editor.TextArea);
        }

        if (_folding is not null)
        {
            var document = _editor.Document;
            var folds = _document.FoldingRanges
                .Where(r => r.StartLine >= 0 && r.EndLine > r.StartLine && r.EndLine < document.LineCount)
                .Select(r => new NewFolding(
                    document.GetLineByNumber(r.StartLine + 1).EndOffset,
                    document.GetLineByNumber(r.EndLine + 1).EndOffset)
                {
                    Name = r.Kind switch
                    {
                        "comment" => "/* ... */",
                        "imports" => "import ...",
                        _ => "...",
                    },
                })
                .Where(f => f.EndOffset > f.StartOffset)
                .OrderBy(f => f.StartOffset)
                .ToList();
            _folding.UpdateFoldings(folds, -1);
        }

        _editor.TextArea.TextView.Redraw();
    }
}
