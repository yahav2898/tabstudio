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
        TitleButton.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        CloseButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        PreviousButton.Click += (_, _) => _peek?.Previous();
        NextButton.Click += (_, _) => _peek?.Next();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
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

        // Once laid out, scroll the definition into view with some context around it.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Editor.ScrollTo(line, 1), Avalonia.Threading.DispatcherPriority.Background);
        Editor.TextArea.TextView.Redraw();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
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
