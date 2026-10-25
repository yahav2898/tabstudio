using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using TabStudio.App.Editing;
using TabStudio.App.Editing.Extensions;

namespace TabStudio.App.Views;

/// <summary>
/// Installed VS Code extensions in the editor: the chosen color theme's editor colors (background, text, current line,
/// selection, caret, line numbers and, for C#, the colors of Roslyn's classifications); highlighting that follows the
/// file's type when an extension that claims the file is installed or removed; and the language rules and snippets in
/// <c>CodeEditorView.LanguageRules.cs</c> and <c>CodeEditorView.Snippets.cs</c>. The IDE around the editor keeps
/// TabStudio's own dark style.
/// </summary>
public sealed partial class CodeEditorView
{
    private TextMate.Installation? _extensionTextMate;
    private EditorColorTheme? _appliedTheme;
    private bool _hasAppliedTheme;

    /// <summary>The editor's TextMate highlighting, whether it was installed with the editor or when an extension claimed the file.</summary>
    private TextMate.Installation? TextMateInstallation => _textMate ?? _extensionTextMate;

    /// <summary>
    /// The color a character of the document is drawn in now, or <see langword="null"/> when it is not on screen; for
    /// tests and the self test, which check that an installed grammar and theme color the text.
    /// </summary>
    /// <param name="offset">The character's offset.</param>
    internal Color? ForegroundAt(int offset)
    {
        var document = _editor.Document;
        var textView = _editor.TextArea.TextView;
        if (offset < 0 || offset >= document.TextLength || !textView.IsMeasureValid)
        {
            return null;
        }

        var line = document.GetLineByOffset(offset);
        var visualLine = textView.GetOrConstructVisualLine(line);
        var column = visualLine.GetVisualColumn(offset - line.Offset);
        foreach (var element in visualLine.Elements)
        {
            if (column >= element.VisualColumn && column < element.VisualColumn + element.VisualLength)
            {
                return (element.TextRunProperties.ForegroundBrush as ISolidColorBrush)?.Color;
            }
        }

        return null;
    }

    /// <summary>The editor's background now, for tests.</summary>
    internal IBrush? EditorBackground => _editor.Background;

    /// <summary>The color of text no grammar rule colors, for tests and the self test.</summary>
    internal Color? TextForeground => (_editor.Foreground as ISolidColorBrush)?.Color;

    private void InitializeExtensions()
    {
        EditorExtensions.Changed += OnEditorExtensionsChanged;
        _document.FileTypeChanged += OnFileTypeChanged;
        ApplyColorTheme();

        // Snippets first: they rebuild the completion list, whose typing handlers must run before the language rules'.
        InitializeSnippets();
        InitializeLanguageRules();

        // A script without an extension may be claimed by its first line, which only the open document has.
        _document.RefreshFileType();
    }

    private void DisposeExtensions()
    {
        EditorExtensions.Changed -= OnEditorExtensionsChanged;
        _document.FileTypeChanged -= OnFileTypeChanged;
        DisposeLanguageRules();
        DisposeSnippets();
        _extensionTextMate?.Dispose();
    }

    private void OnEditorExtensionsChanged(object? sender, EventArgs e)
    {
        ApplyColorTheme();
        UpdateLanguageRules();
    }

    private void OnFileTypeChanged(object? sender, EventArgs e)
    {
        UpdateHighlighting();
        UpdateLanguageRules();
        EndSnippetSession();
    }

    /// <summary>Colors the file with its type's grammar, after the type changed.</summary>
    private void UpdateHighlighting()
    {
        if (_document.HasLanguageService)
        {
            return;
        }

        var extension = _document.FileType.HighlightingExtension;
        if (TextMateInstallation is { } installation)
        {
            TextMateHighlighting.TrySetGrammar(installation, extension is null ? null : TextMateHighlighting.ScopeFor(extension));
        }
        else if (extension is not null)
        {
            _extensionTextMate = TextMateHighlighting.TryInstall(_editor, extension);
        }

        _editor.TextArea.TextView.Redraw();
    }

    /// <summary>Gives the editor the chosen color theme's colors, or TabStudio's own when there is none.</summary>
    private void ApplyColorTheme()
    {
        var theme = EditorExtensions.Theme;
        if (_hasAppliedTheme && ReferenceEquals(theme, _appliedTheme))
        {
            return;
        }

        _hasAppliedTheme = true;
        _appliedTheme = theme;
        var textArea = _editor.TextArea;
        if (theme is null)
        {
            _editor.ClearValue(TemplatedControl.BackgroundProperty);
            _editor.ClearValue(TemplatedControl.ForegroundProperty);
            _editor.ClearValue(TextEditor.LineNumbersForegroundProperty);
            textArea.ClearValue(TextArea.SelectionBrushProperty);
            textArea.TextView.CurrentLineBackground = CurrentLineBrush;
            textArea.Caret.CaretBrush = CaretBrush;
            ClearFoldingColors();
        }
        else
        {
            _editor.Background = theme.Background;
            _editor.Foreground = theme.Foreground;
            _editor.LineNumbersForeground = theme.LineNumbers;
            textArea.SelectionBrush = theme.Selection;
            textArea.TextView.CurrentLineBackground = theme.CurrentLine;
            textArea.Caret.CaretBrush = theme.Caret;
            SetFoldingColors(theme);
        }

        if (_colorizer is not null)
        {
            _colorizer.Palette = theme is null ? null : theme.ClassificationBrush;
            ApplyClassifications();
        }

        _brackets?.SetTheme(theme);
        _snippetRenderer?.SetTheme(theme);
        textArea.TextView.Redraw();
    }

    private void SetFoldingColors(EditorColorTheme theme)
    {
        _editor.SetValue(FoldingMargin.FoldingMarkerBrushProperty, theme.LineNumbers);
        _editor.SetValue(FoldingMargin.FoldingMarkerBackgroundBrushProperty, theme.Background);
        _editor.SetValue(FoldingMargin.SelectedFoldingMarkerBrushProperty, theme.Foreground);
        _editor.SetValue(FoldingMargin.SelectedFoldingMarkerBackgroundBrushProperty, theme.Background);
    }

    private void ClearFoldingColors()
    {
        _editor.SetValue(FoldingMargin.FoldingMarkerBrushProperty, FoldMarkerBrush);
        _editor.SetValue(FoldingMargin.FoldingMarkerBackgroundBrushProperty, FoldMarkerBackground);
        _editor.SetValue(FoldingMargin.SelectedFoldingMarkerBrushProperty, Brushes.White);
        _editor.SetValue(FoldingMargin.SelectedFoldingMarkerBackgroundBrushProperty, FoldMarkerBackground);
    }
}
