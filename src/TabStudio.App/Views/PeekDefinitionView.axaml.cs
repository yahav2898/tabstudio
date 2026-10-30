using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using TabStudio.App.Editing;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Views;

/// <summary>
/// The peek's editor: the definition's file, colored like the code editor, with the definition's line marked and
/// scrolled into view.
/// </summary>
public sealed partial class PeekDefinitionView : UserControl
{
    private readonly ClassificationColorizer _colorizer = new();
    private TextMate.Installation? _textMate;
    private PeekDefinitionViewModel? _peek;

    /// <summary>Creates the view.</summary>
    public PeekDefinitionView()
    {
        InitializeComponent();
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableHyperlinks = false;
        Editor.TextArea.TextView.CurrentLineBackground = ThemeBrushes.Get("Editor.PeekDefinitionLine");
        Editor.TextArea.TextView.CurrentLineBorder = null;
        Editor.TextArea.TextView.LineTransformers.Add(_colorizer);

        // A gap after the line numbers, as the main editor has: "8{" touched its code.
        Editor.TextArea.LeftMargins.Add(new Border { Width = GutterGap, Name = "PeekGutterGap" });
        TitleButton.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        CloseButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        PreviousButton.Click += (_, _) => _peek?.Previous();
        NextButton.Click += (_, _) => _peek?.Next();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Editor.LayoutUpdated += (_, _) => ScrollToPeekLine();
        Editor.AddHandler(PointerPressedEvent, (_, _) => _scrollLine = null, RoutingStrategies.Tunnel, handledEventsToo: true);
        Editor.AddHandler(PointerWheelChangedEvent, (_, _) => _scrollLine = null, RoutingStrategies.Tunnel, handledEventsToo: true);
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Raised when the user asks to open the definition as a tab.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Raised when the user closes the peek.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The text shown, for tests.</summary>
    public string Text => Editor.Text;

    /// <summary>The line marked as the definition's, for tests.</summary>
    public int MarkedLine => Editor.TextArea.Caret.Line;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_peek is not null)
        {
            _peek.PropertyChanged -= OnPeekPropertyChanged;
        }

        _peek = DataContext as PeekDefinitionViewModel;
        if (_peek is not null)
        {
            _peek.PropertyChanged += OnPeekPropertyChanged;
            Show(_peek.Current);
        }
    }

    private void OnPeekPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PeekDefinitionViewModel.Current) && _peek is not null)
        {
            Show(_peek.Current);
        }
    }

    private void Show(PeekedDefinition definition)
    {
        _textMate?.Dispose();
        _textMate = null;
        Editor.Document = new TextDocument(definition.Text);
        _colorizer.SetClassifications(definition.Classifications.Select(c => (c.Start, c.Length, c.Classification)));
        if (definition.Classifications.Count == 0 && FileTypes.For(definition.Location.FilePath).HighlightingExtension is { } extension)
        {
            _textMate = TextMateHighlighting.TryInstall(Editor, extension);
        }

        var line = Math.Clamp(definition.Location.Line, 1, Editor.Document.LineCount);
        Editor.TextArea.Caret.Line = line;
        Editor.TextArea.Caret.Column = Math.Max(1, definition.Location.Column);

        // Once laid out, the definition shows two lines from the top, at a whole line: centering it scrolled to any
        // pixel and cut the first and last lines in half. Each layout scrolls there again until the user scrolls,
        // since the editor's own bring-the-caret-into-view comes after the first layout and moved it.
        _scrollLine = line;
        ScrollToPeekLine();
        Editor.TextArea.TextView.Redraw();
    }

    private int? _scrollLine;

    private void ScrollToPeekLine()
    {
        if (_scrollLine is not { } line || Editor.ExtentHeight <= 0 || Editor.ViewportHeight <= 0)
        {
            return;
        }

        var target = Math.Min(TopOffsetOf(line), Math.Max(0, Editor.ExtentHeight - Editor.ViewportHeight));
        if (Math.Abs(Editor.VerticalOffset - target) >= 0.5)
        {
            Editor.ScrollToVerticalOffset(target);
        }
    }

    /// <summary>The space after the line numbers.</summary>
    internal const double GutterGap = 8;

    /// <summary>The vertical offset that puts a line two lines below the top of the view, on a line boundary.</summary>
    /// <param name="line">The one-based line.</param>
    internal double TopOffsetOf(int line) => Math.Max(0, line - 3) * Editor.TextArea.TextView.DefaultLineHeight;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _scrollLine = null;
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _textMate?.Dispose();
        _textMate = null;
    }
}
