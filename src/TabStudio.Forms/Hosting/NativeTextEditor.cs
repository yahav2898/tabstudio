using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;
using AvaloniaControl = Avalonia.Controls.Control;
using AvaloniaTextBox = Avalonia.Controls.TextBox;
using DrawingColor = System.Drawing.Color;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// The native text box shown over the Windows Forms text field that has focus, so typing, the device's keyboard,
/// selection handles and the IME work as in any Avalonia text box. It has no border or padding of its own and takes
/// the field's font and colors, so it looks like the field it covers.
/// </summary>
/// <remarks>
/// The model keeps the text: what the user types is sent to it with the text revision the box had, and when the app
/// sets the text or the selection, the frame's revision changes and the box takes the app's text.
/// </remarks>
internal sealed class NativeTextEditor : AvaloniaTextBox
{
    private static readonly IBrush SelectionHighlight = new ImmutableSolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
    private static ControlTheme? _theme;

    private bool _applying;

    /// <summary>Creates the editor.</summary>
    public NativeTextEditor()
    {
        Theme = EditorTheme;
        Padding = default;
        BorderThickness = default;
        MinHeight = 0;
        MinWidth = 0;
        CornerRadius = default;
        SelectionBrush = SelectionHighlight;
        SelectionForegroundBrush = Brushes.White;
        IsVisible = false;
    }

    /// <summary>Raised when the user changed the text: the window and the text, with the revision it was typed over.</summary>
    public event EventHandler<EditorEdit>? Edited;

    /// <summary>Raised when the user moved the caret or the selection.</summary>
    public event EventHandler<EditorEdit>? SelectionMoved;

    /// <summary>The window whose field the editor covers, or 0.</summary>
    public int WindowId { get; private set; }

    /// <summary>The field the editor shows, or <see langword="null"/>.</summary>
    public EditorFrame? Field { get; private set; }

    /// <summary>Where the editor is, on the desktop.</summary>
    public System.Drawing.Rectangle DesktopBounds { get; private set; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(NativeTextEditor);

    /// <summary>A template with nothing but the scrolling text: no border, padding, watermark or buttons.</summary>
    private static ControlTheme EditorTheme => _theme ??= new ControlTheme(typeof(NativeTextEditor))
    {
        Setters =
        {
            new Setter(TemplateProperty, new FuncControlTemplate<AvaloniaTextBox>(BuildTemplate)),
            new Setter(FocusAdornerProperty, null),
        },
    };

    /// <summary>Shows the editor over a field, taking the app's text when its revision changed.</summary>
    /// <param name="windowId">The field's window.</param>
    /// <param name="field">The field.</param>
    /// <param name="desktopBounds">Where the field's text goes, on the desktop.</param>
    public void Show(int windowId, EditorFrame field, System.Drawing.Rectangle desktopBounds)
    {
        var previous = WindowId == windowId ? Field : null;
        var sameField = previous is not null && previous.ControlId == field.ControlId;
        Field = field;
        WindowId = windowId;
        DesktopBounds = desktopBounds;
        _applying = true;
        try
        {
            ApplyLook(field);
            if (!sameField || previous!.TextRevision != field.TextRevision)
            {
                // ClearUndo keeps the box's own undo from going back to another field's text.
                Text = field.Text;
                ClearSelection();
                CaretIndex = 0;
                if (!sameField)
                {
                    IsUndoEnabled = false;
                    IsUndoEnabled = true;
                }
            }

            if (!sameField || previous!.SelectionRevision != field.SelectionRevision || previous.TextRevision != field.TextRevision)
            {
                var start = Math.Clamp(field.SelectionStart, 0, field.Text.Length);
                var end = Math.Clamp(field.SelectionStart + field.SelectionLength, start, field.Text.Length);
                SelectionStart = start;
                SelectionEnd = end;
                CaretIndex = end;
            }
        }
        finally
        {
            _applying = false;
        }

        IsVisible = true;
    }

    /// <summary>Hides the editor: no field has focus.</summary>
    public void Hide()
    {
        IsVisible = false;
        Field = null;
        WindowId = 0;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_applying || Field is not { } field)
        {
            return;
        }

        if (change.Property == TextProperty)
        {
            var text = Text ?? string.Empty;
            var (start, length) = Selection();
            Field = field with { Text = text, SelectionStart = start, SelectionLength = length };
            Edited?.Invoke(this, new EditorEdit(WindowId, field.ControlId, text, start, length, field.TextRevision));
        }
        else if (change.Property == SelectionStartProperty || change.Property == SelectionEndProperty || change.Property == CaretIndexProperty)
        {
            var (start, length) = Selection();
            if (start != field.SelectionStart || length != field.SelectionLength)
            {
                Field = field with { SelectionStart = start, SelectionLength = length };
                SelectionMoved?.Invoke(this, new EditorEdit(WindowId, field.ControlId, Text ?? string.Empty, start, length, field.TextRevision));
            }
        }
    }

    private static AvaloniaControl BuildTemplate(AvaloniaTextBox box, INameScope scope)
    {
        var presenter = new TextPresenter { Name = "PART_TextPresenter" };
        presenter.Bind(TextPresenter.TextProperty, new TemplateBinding(TextProperty) { Mode = BindingMode.TwoWay });
        presenter.Bind(TextPresenter.CaretIndexProperty, new TemplateBinding(CaretIndexProperty));
        presenter.Bind(TextPresenter.SelectionStartProperty, new TemplateBinding(SelectionStartProperty));
        presenter.Bind(TextPresenter.SelectionEndProperty, new TemplateBinding(SelectionEndProperty));
        presenter.Bind(TextPresenter.TextAlignmentProperty, new TemplateBinding(TextAlignmentProperty));
        presenter.Bind(TextPresenter.TextWrappingProperty, new TemplateBinding(TextWrappingProperty));
        presenter.Bind(TextPresenter.LineHeightProperty, new TemplateBinding(LineHeightProperty));
        presenter.Bind(TextPresenter.PasswordCharProperty, new TemplateBinding(PasswordCharProperty));
        presenter.Bind(TextPresenter.RevealPasswordProperty, new TemplateBinding(RevealPasswordProperty));
        presenter.Bind(TextPresenter.SelectionBrushProperty, new TemplateBinding(SelectionBrushProperty));
        presenter.Bind(TextPresenter.SelectionForegroundBrushProperty, new TemplateBinding(SelectionForegroundBrushProperty));
        presenter.Bind(TextPresenter.CaretBrushProperty, new TemplateBinding(CaretBrushProperty));
        presenter.Bind(TextPresenter.CaretBlinkIntervalProperty, new TemplateBinding(CaretBlinkIntervalProperty));
        presenter.Bind(HorizontalAlignmentProperty, new TemplateBinding(HorizontalContentAlignmentProperty));
        presenter.Bind(VerticalAlignmentProperty, new TemplateBinding(VerticalContentAlignmentProperty));
        scope.Register(presenter.Name, presenter);

        var scroller = new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter, AllowAutoHide = false };
        scroller.Bind(ScrollViewer.HorizontalScrollBarVisibilityProperty, new TemplateBinding(ScrollViewer.HorizontalScrollBarVisibilityProperty));
        scroller.Bind(ScrollViewer.VerticalScrollBarVisibilityProperty, new TemplateBinding(ScrollViewer.VerticalScrollBarVisibilityProperty));
        scope.Register(scroller.Name, scroller);

        var background = new Border { Child = scroller };
        background.Bind(Border.BackgroundProperty, new TemplateBinding(BackgroundProperty));
        background.Bind(Decorator.PaddingProperty, new TemplateBinding(PaddingProperty));
        return background;
    }

    private static IBrush Brush(DrawingColor color) => new ImmutableSolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

    private void ApplyLook(EditorFrame field)
    {
        var font = field.Font;
        FontFamily = new FontFamily(font.Family);
        FontSize = font.Size;
        FontWeight = font.Bold ? FontWeight.Bold : FontWeight.Normal;
        FontStyle = font.Italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal;
        LineHeight = TextMetrics.LineHeight(font);
        Foreground = Brush(field.ForeColor);
        CaretBrush = Brush(field.ForeColor);
        Background = Brush(field.BackColor.A == 0 ? DrawingColor.White : field.BackColor);
        TextAlignment = field.Alignment switch
        {
            HorizontalAlignment.Center => TextAlignment.Center,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
        AcceptsReturn = field.Multiline && field.AcceptsReturn;
        AcceptsTab = field.AcceptsTab;
        TextWrapping = field.Multiline && field.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        VerticalContentAlignment = field.Multiline ? Avalonia.Layout.VerticalAlignment.Top : Avalonia.Layout.VerticalAlignment.Center;
        IsReadOnly = field.ReadOnly;
        PasswordChar = field.PasswordChar;
        MaxLength = Math.Max(0, field.MaxLength);
        var bars = field.ScrollBars;
        ScrollViewer.SetVerticalScrollBarVisibility(this, field.Multiline && bars is ScrollBars.Vertical or ScrollBars.Both ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden);
        ScrollViewer.SetHorizontalScrollBarVisibility(this, field.Multiline && bars is ScrollBars.Horizontal or ScrollBars.Both ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden);
    }

    private (int Start, int Length) Selection()
    {
        var start = Math.Min(SelectionStart, SelectionEnd);
        var end = Math.Max(SelectionStart, SelectionEnd);
        return start == end ? (CaretIndex, 0) : (start, end - start);
    }
}

/// <summary>Something the user did in the native text box.</summary>
/// <param name="WindowId">The window.</param>
/// <param name="ControlId">The field.</param>
/// <param name="Text">The text.</param>
/// <param name="SelectionStart">The selection's start.</param>
/// <param name="SelectionLength">The selection's length.</param>
/// <param name="Revision">The text revision the box had.</param>
internal sealed record EditorEdit(int WindowId, int ControlId, string Text, int SelectionStart, int SelectionLength, int Revision);
