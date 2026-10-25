using TabStudio.LanguageServers.DebugAdapters;

namespace TabStudio.App.ViewModels;

/// <summary>The breakpoints of programs debug adapters run: hit counts, logpoints, and what the adapter verified.</summary>
public sealed partial class BreakpointsViewModel
{
    /// <summary>Changes a breakpoint's hit count and log message, which debug adapters support.</summary>
    /// <param name="breakpoint">The breakpoint.</param>
    /// <param name="hitCount">After how many hits it stops; <see langword="null"/> for every hit.</param>
    /// <param name="logMessage">The message to log instead of stopping; <see langword="null"/> to stop.</param>
    public void SetOptions(BreakpointViewModel breakpoint, string? hitCount, string? logMessage)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        breakpoint.SetOptions(hitCount, logMessage);
        OnChanged(breakpoint.FilePath);
    }

    /// <summary>The breakpoints of every file, as a debug adapter takes them.</summary>
    public Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> ForAdapter() =>
        _breakpoints
            .GroupBy(b => b.FilePath, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DapSourceBreakpoint>)g.Select(ToAdapter).ToList(), StringComparer.Ordinal);

    /// <summary>A file's breakpoints, as a debug adapter takes them.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public List<DapSourceBreakpoint> ForAdapter(string filePath) => InFile(filePath).Select(ToAdapter).ToList();

    /// <summary>Shows what a debug adapter did with a file's breakpoints: verified or not, and moved to the line it chose.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="results">The adapter's breakpoints, in the order of <see cref="ForAdapter(string)"/>.</param>
    public void ApplyAdapterResults(string filePath, IReadOnlyList<DapBreakpoint> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var breakpoints = InFile(filePath);
        for (var i = 0; i < breakpoints.Count && i < results.Count; i++)
        {
            var breakpoint = breakpoints[i];
            var result = results[i];
            breakpoint.BindToAdapter(result.IsVerified, result.Message);
            if (result.IsVerified && result.Line is { } line && line != breakpoint.Line && Find(filePath, line) is null)
            {
                breakpoint.MoveTo(line, DocumentFor(filePath)?.Document);
            }
        }

        OnChanged(filePath);
    }

    /// <summary>Forgets what the debug adapter said, when its session ends.</summary>
    public void UnbindAdapter()
    {
        foreach (var breakpoint in _breakpoints)
        {
            breakpoint.BindToAdapter(null, null);
        }

        foreach (var file in _breakpoints.Select(b => b.FilePath).Distinct(StringComparer.Ordinal).ToList())
        {
            OnChanged(file);
        }
    }

    private static DapSourceBreakpoint ToAdapter(BreakpointViewModel breakpoint) =>
        new(breakpoint.Line, breakpoint.Condition, breakpoint.HitCount, breakpoint.LogMessage);
}
