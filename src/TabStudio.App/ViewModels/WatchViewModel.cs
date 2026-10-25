using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Debugging;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The Watch panel: expressions the user typed, such as <c>order.Items[0].Price</c>, evaluated every time the program
/// pauses. The expressions are kept with the session.
/// </summary>
/// <remarks>
/// TabStudio's debugger evaluates paths, not full C#: a variable in scope (or a member of <c>this</c>) followed by
/// member names and indexes. Method calls and operators are not supported, and the panel says so.
/// </remarks>
public sealed partial class WatchViewModel : ObservableObject
{
    private readonly List<string> _expressions = [];
    private DebugPausedEventArgs? _pause;

    /// <summary>The watches and their values.</summary>
    public VariableTreeViewModel Tree { get; } = new();

    /// <summary>The expressions, in order.</summary>
    public IReadOnlyList<string> Expressions => _expressions;

    /// <summary>The text in the "Add item to watch" box.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewExpressionCommand))]
    public partial string NewExpression { get; set; } = string.Empty;

    /// <summary>Raised when the expressions change, so the session can be saved.</summary>
    public event EventHandler? ExpressionsChanged;

    /// <summary>Creates the panel.</summary>
    public WatchViewModel()
    {
        Tree.RemoveRequested += (_, row) => _ = RemoveAsync(row.Name);
    }

    /// <summary>Replaces the expressions with saved ones.</summary>
    /// <param name="expressions">The expressions, or <see langword="null"/> for none.</param>
    public void Load(IEnumerable<string>? expressions)
    {
        _expressions.Clear();
        _expressions.AddRange((expressions ?? []).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct(StringComparer.Ordinal));
        _ = RefreshAsync();
    }

    /// <summary>Adds an expression and evaluates it if the program is paused.</summary>
    /// <param name="expression">The expression.</param>
    public async Task AddAsync(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var trimmed = expression.Trim();
        if (trimmed.Length == 0 || _expressions.Contains(trimmed, StringComparer.Ordinal))
        {
            return;
        }

        _expressions.Add(trimmed);
        ExpressionsChanged?.Invoke(this, EventArgs.Empty);
        await RefreshAsync();
    }

    /// <summary>Removes an expression.</summary>
    /// <param name="expression">The expression.</param>
    public async Task RemoveAsync(string expression)
    {
        if (_expressions.Remove(expression))
        {
            ExpressionsChanged?.Invoke(this, EventArgs.Empty);
            await RefreshAsync();
        }
    }

    /// <summary>Evaluates every expression in a pause, or shows them without values while the program is not paused.</summary>
    /// <param name="pause">The pause, or <see langword="null"/> when the program runs or has ended.</param>
    public Task EvaluateAsync(DebugPausedEventArgs? pause)
    {
        _pause = pause;
        return RefreshAsync();
    }

    /// <summary>Marks the values as out of date while the program runs.</summary>
    public void MarkStale()
    {
        _pause = null;
        Tree.IsStale = true;
    }

    [RelayCommand(CanExecute = nameof(CanAddNewExpression))]
    private async Task AddNewExpressionAsync()
    {
        var expression = NewExpression;
        NewExpression = string.Empty;
        await AddAsync(expression);
    }

    private bool CanAddNewExpression() => !string.IsNullOrWhiteSpace(NewExpression);

    private async Task RefreshAsync()
    {
        var pause = _pause;
        var expressions = _expressions.ToList();
        if (pause is null)
        {
            // Not paused: the expressions are listed, and get values at the next pause.
            await Tree.ShowAsync(expressions.Select(e => new VariableSource(e, e, null, string.Empty, IsRemovable: true)).ToList());
            return;
        }

        var sources = await Task.Run(() => expressions.Select(e => Evaluate(pause, e)).ToList());
        if (ReferenceEquals(pause, _pause))
        {
            await Tree.ShowAsync(sources);
        }
    }

    private static VariableSource Evaluate(DebugPausedEventArgs pause, string expression)
    {
        try
        {
            return pause.FindValue(expression) is { } value
                ? new VariableSource(expression, expression, value, IsRemovable: true)
                : new VariableSource(expression, expression, null, $"'{expression}' is not a variable, member or item in scope. Watch takes paths such as order.Items[0].Price; method calls and operators are not supported.", IsRemovable: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new VariableSource(expression, expression, null, ex.Message, IsRemovable: true);
        }
    }
}
