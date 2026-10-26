using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace $rootnamespace$.Porting;

/// <summary>The kind of icon a message box shows.</summary>
internal enum MessageIcon
{
    None,
    Information,
    Question,
    Warning,
    Error,
}

/// <summary>A button of a message box: its text, the answer it gives, and whether Enter or Escape presses it.</summary>
internal sealed record MessageButton(string Text, int Result, bool IsDefault = false, bool IsCancel = false);

/// <summary>The message box's window: an icon, the message and a row of buttons.</summary>
internal sealed class MessageDialog : WindowView
{
    private int _result;

    private MessageDialog(string text, string? caption, IReadOnlyList<MessageButton> buttons, MessageIcon icon, int closedResult)
    {
        _result = closedResult;
        Title = string.IsNullOrEmpty(caption) ? " " : caption;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        foreach (var button in buttons)
        {
            var control = new Button { Content = button.Text, MinWidth = 80, IsDefault = button.IsDefault, IsCancel = button.IsCancel, HorizontalContentAlignment = HorizontalAlignment.Center };
            var result = button.Result;
            control.Click += (_, _) =>
            {
                _result = result;
                Close();
            };
            row.Children.Add(control);
        }

        var message = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var body = new DockPanel { Margin = new Thickness(16, 8, 16, 16) };
        DockPanel.SetDock(row, Dock.Bottom);
        body.Children.Add(row);
        if (Glyph(icon) is { } glyph)
        {
            DockPanel.SetDock(glyph, Dock.Left);
            body.Children.Add(glyph);
        }

        body.Children.Add(message);
        Content = body;
    }

    /// <summary>Shows a message and waits for the answer.</summary>
    /// <param name="owner">The window it belongs to, or <see langword="null"/> for the main window.</param>
    /// <param name="text">The message.</param>
    /// <param name="caption">The title.</param>
    /// <param name="buttons">The buttons, left to right.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="closedResult">The answer when the box is closed without a button.</param>
    public static async Task<int> ShowAsync(WindowView? owner, string text, string? caption, IReadOnlyList<MessageButton> buttons, MessageIcon icon, int closedResult)
    {
        var dialog = new MessageDialog(text, caption, buttons, icon, closedResult) { Owner = owner ?? MainWindow };
        await dialog.ShowModalAsync();
        return dialog._result;
    }

    private static Border? Glyph(MessageIcon icon)
    {
        var (symbol, color) = icon switch
        {
            MessageIcon.Information => ("i", Color.FromRgb(0x00, 0x78, 0xD4)),
            MessageIcon.Question => ("?", Color.FromRgb(0x00, 0x78, 0xD4)),
            MessageIcon.Warning => ("!", Color.FromRgb(0xC1, 0x7A, 0x00)),
            MessageIcon.Error => ("×", Color.FromRgb(0xC4, 0x2B, 0x1C)),
            _ => (string.Empty, Colors.Transparent),
        };

        return symbol.Length == 0 ? null : new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(color),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = symbol,
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }
}
