using TabStudio.Debugging;
using TabStudio.LanguageServers.DebugAdapters;

namespace TabStudio.App.ViewModels;

/// <summary>The breakpoints of programs debug adapters run: hit counts, logpoints, and what the adapter verified.</summary>
public sealed partial class BreakpointsViewModel
{
    /// <summary>Changes a breakpoint's hit count and its log message, which debug adapters support.</summary>
    /// <param name="breakpoint">The breakpoint.</param>
    /// <param name="hitCount">On which hits it stops, as <see cref="BreakpointHitCondition"/> reads it; <see langword="null"/> for every hit.</param>
    /// <param name="logMessage">The message to log instead of stopping; <see langword="null"/> to stop.</param>
    public void SetOptions(BreakpointViewModel breakpoint, string? hitCount, string? logMessage)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        breakpoint.SetOptions(hitCount, logMessage);
        if (_session is not null && breakpoint.Binding is { } binding)
        {
            // A new hit count replaces the C# debugger's breakpoint on the line, and counts again from zero.
            breakpoint.Bind(_session.SetBreakpoint(binding.FilePath, binding.Line, breakpoint.Condition, BreakpointHitCondition.Parse(breakpoint.HitCount)));
        }

        OnChanged(breakpoint.FilePath);
    }

    /// <summary>
    /// Whether the IDE counts a breakpoint's hits for a debug adapter, rather than the adapter: every breakpoint with a
    /// hit count, except a logpoint, which never stops for the IDE to count it.
    /// </summary>
    /// <param name="breakpoint">The breakpoint.</param>
    public static bool CountsHitsForAdapter(BreakpointViewModel breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        return breakpoint.LogMessage is null && BreakpointHitCondition.Parse(breakpoint.HitCount) is not null;
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

    // A hit count the IDE counts is not the adapter's: each adapter reads a bare number its own way.
    private static DapSourceBreakpoint ToAdapter(BreakpointViewModel breakpoint) =>
        new(breakpoint.Line, breakpoint.Condition, CountsHitsForAdapter(breakpoint) ? null : breakpoint.HitCount, breakpoint.LogMessage);
}
