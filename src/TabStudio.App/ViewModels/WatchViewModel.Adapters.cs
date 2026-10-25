namespace TabStudio.App.ViewModels;

/// <summary>The Watch panel of a program a debug adapter paused: each expression is evaluated by the adapter.</summary>
public sealed partial class WatchViewModel
{
    private Func<string, Task<VariableSource>>? _evaluator;

    /// <summary>
    /// Evaluates the expressions with a debug adapter from now on, as the program pauses, or stops doing so.
    /// </summary>
    /// <param name="evaluator">Evaluates an expression in the paused frame; <see langword="null"/> while the program runs.</param>
    public Task EvaluateWithAsync(Func<string, Task<VariableSource>>? evaluator)
    {
        _evaluator = evaluator;
        return evaluator is null ? Task.CompletedTask : RefreshAsync();
    }

    private async Task RefreshWithEvaluatorAsync(Func<string, Task<VariableSource>> evaluator)
    {
        var expressions = _expressions.ToList();
        var sources = new List<VariableSource>(expressions.Count);
        foreach (var expression in expressions)
        {
            sources.Add((await evaluator(expression)) with { IsRemovable = true });
        }

        if (ReferenceEquals(evaluator, _evaluator))
        {
            await Tree.ShowAsync(sources);
        }
    }
}
