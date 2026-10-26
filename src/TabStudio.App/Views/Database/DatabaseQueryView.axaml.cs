using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Database;
using TabStudio.Database;

namespace TabStudio.App.Views.Database;

public sealed partial class DatabaseQueryView : UserControl
{
    private readonly DatabaseQueryViewModel _model;
    private CompletionWindow? _completion;
    private int _sortIndex = -1;
    private bool _sortDescending;
    public DatabaseQueryView(DocumentViewModel document)
    {
        InitializeComponent();
        _model = document.DatabaseQuery!; DataContext = _model; SqlEditor.Document = document.Document;
        SqlEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("SQL");
        SqlEditor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        SqlEditor.TextArea.TextEntered += OnTextEntered;
        _model.RunRequested += OnRunRequested; _model.ResultChanged += OnResultChanged;
        ResultsGrid.Sorting += OnSorting;
        DetachedFromVisualTree += (_, _) => { _completion?.Close(); };
        // These are owned by the document, just like the view; no global handlers keep either alive.
        RebuildColumns();
    }
    private async void OnRunRequested(object? sender, EventArgs e) => await RunAsync();
    private Task RunAsync() => _model.ExecuteAsync(SqlEditor.SelectionLength > 0 ? SqlEditor.SelectedText : SqlEditor.Text);
    private async void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Enter)
        { e.Handled = true; _completion?.Close(); await RunAsync(); }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Space)
        { e.Handled = true; ShowCompletion(); }
    }
    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_completion is null && e.Text is { Length: > 0 } text && (text == "." || char.IsLetter(text[^1]))) ShowCompletion();
    }
    private void ShowCompletion()
    {
        if (_completion is not null) return;
        var items = SqlCompletions.Get(_model.Schema, SqlEditor.Text, SqlEditor.CaretOffset, _model.Connection.Provider);
        if (items.Count == 0) return;
        var window = _completion = new CompletionWindow(SqlEditor.TextArea);
        window.StartOffset = SqlEditor.CaretOffset - SqlCompletions.PrefixLength(SqlEditor.Text, SqlEditor.CaretOffset);
        foreach (var item in items) window.CompletionList.CompletionData.Add(new Completion(item));
        window.Closed += (_, _) => _completion = null; window.Show();
    }
    private void OnResultChanged(object? sender, EventArgs e) => RebuildColumns();
    private void RebuildColumns()
    {
        ResultsGrid.Columns.Clear();
        _sortIndex = -1;
        _sortDescending = false;
        if (_model.SelectedResult is not { } result) return;
        for (var i = 0; i < result.Data.Columns.Count; i++)
            ResultsGrid.Columns.Add(new DataGridTextColumn { Header = result.Data.Columns[i].Name, Binding = new Binding($"[{i}]"), SortMemberPath = i.ToString(System.Globalization.CultureInfo.InvariantCulture), MinWidth = 100, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
    }
    private void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        var index = int.Parse(e.Column.SortMemberPath!, System.Globalization.CultureInfo.InvariantCulture);
        var descending = _sortIndex == index && !_sortDescending;
        _sortIndex = index;
        _sortDescending = descending;
        if (_model.SelectedResult is { } result)
            for (var i = 0; i < ResultsGrid.Columns.Count; i++)
                ResultsGrid.Columns[i].Header = result.Data.Columns[i].Name + (i == index ? descending ? " ↓" : " ↑" : string.Empty);
        var sorted = _model.Rows.OrderBy(r => r.Values[index], Comparer<object?>.Create(Compare)).ToArray();
        if (descending) Array.Reverse(sorted);
        _model.Rows.Clear(); foreach (var row in sorted) _model.Rows.Add(row);
    }
    private static int Compare(object? left, object? right)
    {
        if (left is null) return right is null ? 0 : -1;
        if (right is null) return 1;
        return left.GetType() == right.GetType() && left is IComparable c ? c.CompareTo(right) : string.Compare(SqlExecutor.Format(left), SqlExecutor.Format(right), StringComparison.Ordinal);
    }
    private async void OnCopyCsv(object? sender, RoutedEventArgs e) => await CopyAsync(json: false);
    private async void OnCopyJson(object? sender, RoutedEventArgs e) => await CopyAsync(json: true);
    private async Task CopyAsync(bool json)
    {
        if (_model.Export() is { } result && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(json ? SqlExecutor.Json(result) : SqlExecutor.Csv(result));
    }
    private sealed class Completion(SqlCompletion item) : ICompletionData
    {
        public IImage? Image => null;
        public string Text => item.Text;
        public object Content => item.Text;
        public object Description => item.Kind;
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) => textArea.Document.Replace(completionSegment, item.InsertionText ?? item.Text);
    }
}
