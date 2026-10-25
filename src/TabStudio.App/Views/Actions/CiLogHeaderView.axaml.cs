using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TabStudio.App.Views.Actions;

/// <summary>
/// The header of a job's log tab: the job and its run, the error count, Next error (F8) and Previous error
/// (Shift+F8), timestamps, folding, Go to Line (Ctrl+G), downloading again, and opening the job on GitHub.
/// </summary>
public sealed partial class CiLogHeaderView : UserControl
{
    private int _lineCount;

    /// <summary>Creates the view.</summary>
    public CiLogHeaderView()
    {
        InitializeComponent();
        GoToLineBox.KeyDown += OnGoToLineKeyDown;
    }

    /// <summary>Raised to move to another error: -1 for the previous one, 1 for the next.</summary>
    public event EventHandler<int>? ErrorMoveRequested;

    /// <summary>Raised to fold every step and group (<see langword="true"/>) or unfold everything.</summary>
    public event EventHandler<bool>? FoldRequested;

    /// <summary>Raised to go to a 1-based line.</summary>
    public event EventHandler<int>? GoToLineRequested;

    /// <summary>Raised when the Go to Line button is tapped; the log answers with <see cref="OpenGoToLine"/>.</summary>
    public event EventHandler? GoToLineBarRequested;

    /// <summary>Raised when the Go to Line bar closes, so the editor takes the focus back.</summary>
    public event EventHandler? GoToLineClosed;

    /// <summary>Shows the Go to Line bar, as Ctrl+G does.</summary>
    /// <param name="currentLine">The caret's line, filled in.</param>
    /// <param name="lineCount">How many lines the log has.</param>
    public void OpenGoToLine(int currentLine, int lineCount)
    {
        _lineCount = lineCount;
        GoToLineHint.Text = $"1 to {lineCount.ToString("N0", CultureInfo.CurrentCulture)}";
        GoToLineBox.Text = currentLine.ToString(CultureInfo.CurrentCulture);
        GoToLineBar.IsVisible = true;
        Dispatcher.UIThread.Post(() =>
        {
            GoToLineBox.Focus();
            GoToLineBox.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void OnPreviousErrorClick(object? sender, RoutedEventArgs e) => ErrorMoveRequested?.Invoke(this, -1);

    private void OnNextErrorClick(object? sender, RoutedEventArgs e) => ErrorMoveRequested?.Invoke(this, 1);

    private void OnFoldAllClick(object? sender, RoutedEventArgs e) => FoldRequested?.Invoke(this, true);

    private void OnUnfoldAllClick(object? sender, RoutedEventArgs e) => FoldRequested?.Invoke(this, false);

    private void OnGoToLineClick(object? sender, RoutedEventArgs e) => GoToLineBarRequested?.Invoke(this, EventArgs.Empty);

    private void OnGoClick(object? sender, RoutedEventArgs e) => Go();

    private void OnGoToLineCancelClick(object? sender, RoutedEventArgs e) => CloseGoToLine();

    private void OnGoToLineKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Go();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseGoToLine();
            e.Handled = true;
        }
    }

    private void Go()
    {
        if (int.TryParse(GoToLineBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var line) && line >= 1 && line <= Math.Max(1, _lineCount))
        {
            CloseGoToLine();
            GoToLineRequested?.Invoke(this, line);
        }
        else
        {
            GoToLineHint.Text = $"Enter a line from 1 to {_lineCount.ToString("N0", CultureInfo.CurrentCulture)}";
        }
    }

    private void CloseGoToLine()
    {
        GoToLineBar.IsVisible = false;
        GoToLineClosed?.Invoke(this, EventArgs.Empty);
    }
}
