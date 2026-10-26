using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Analysis;

/// <summary>One GCC -fstack-usage row, including whether GCC could bound dynamic allocation.</summary>
/// <param name="File">Compiler-reported source path.</param>
/// <param name="Line">One-based declaration line.</param>
/// <param name="Column">One-based declaration column.</param>
/// <param name="Function">Compiler-reported function identity.</param>
/// <param name="Bytes">Local frame size, excluding callees.</param>
/// <param name="Bounded">False for unbounded dynamic allocation.</param>
public sealed record StackUsageEntry(string File, int Line, int Column, string Function, ulong Bytes, bool Bounded);

/// <summary>A call edge. A missing callee explicitly represents an unresolved indirect call.</summary>
/// <param name="Caller">Function identity matching its stack-usage entry.</param>
/// <param name="Callee">Callee identity, or null when its target is unknown.</param>
public sealed record StackCall(string Caller, string? Callee);

/// <summary>A function's computed bound, or the reason it cannot be bounded safely.</summary>
/// <param name="Function">Function identity.</param>
/// <param name="WorstCaseBytes">Known bound, excluding asynchronous interrupts, or null when unknown.</param>
/// <param name="KnownBytes">Known local frame plus known callee requirements; this is not a bound when WorstCaseBytes is null.</param>
/// <param name="Reasons">Missing analysis evidence, recursion or dynamic/indirect calls.</param>
public sealed record FunctionStackBound(string Function, ulong? WorstCaseBytes, ulong KnownBytes, IReadOnlyList<string> Reasons);

/// <summary>A user-configured RTOS task's stack allocation in bytes, never words.</summary>
/// <param name="Name">Task display name.</param>
/// <param name="EntryFunction">Task entry identity in the call graph.</param>
/// <param name="AllocatedBytes">Actual task stack size in bytes.</param>
/// <param name="ExtraBytes">Configured RTOS/context/exception allowance to add to the call-chain requirement.</param>
public sealed record TaskStackBudget(string Name, string EntryFunction, ulong AllocatedBytes, ulong ExtraBytes = 0);

/// <summary>A task's stack budget check; unknown analysis never becomes a passing result.</summary>
/// <param name="Task">Task name.</param>
/// <param name="RequiredBytes">Bound plus explicit allowance, or null if unknown.</param>
/// <param name="AllocatedBytes">Configured allocation.</param>
/// <param name="ExceedsBudget">Whether known required or known minimum bytes exceed allocation.</param>
/// <param name="Message">Human-readable result, with analysis limitations.</param>
public sealed record TaskStackFinding(string Task, ulong? RequiredBytes, ulong AllocatedBytes, bool ExceedsBudget, string Message);

/// <summary>Conservative stack analysis for GCC evidence and an explicitly complete call graph.</summary>
public static partial class StackAnalyzer
{
    /// <summary>Reads GCC's tab-delimited .su rows. Invalid lines fail with their input line number.</summary>
    public static IReadOnlyList<StackUsageEntry> ParseGccStackUsage(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<StackUsageEntry>();
        var lineNumber = 0;
        foreach (var line in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var row = UsageLine().Match(line);
            if (!row.Success || !int.TryParse(row.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sourceLine)
                || !int.TryParse(row.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var column)
                || !ulong.TryParse(row.Groups[5].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
            {
                throw new InvalidDataException($"Invalid GCC stack-usage row at line {lineNumber}.");
            }
            var flags = row.Groups[6].Value.Split(',', StringSplitOptions.TrimEntries);
            if (flags.Any(f => f is not ("static" or "dynamic" or "bounded")))
                throw new InvalidDataException($"Unknown GCC stack-usage qualifier at line {lineNumber}.");
            var bounded = flags.Contains("static", StringComparer.Ordinal) || flags.Contains("bounded", StringComparer.Ordinal);
            result.Add(new(row.Groups[1].Value, sourceLine, column, row.Groups[4].Value, bytes, bounded));
        }
        return result;
    }

    /// <summary>Calculates bounds on a directed graph. Incomplete graphs, duplicate identities, recursion,
    /// missing library .su records and indirect calls produce unknown bounds, not optimistic zeroes.
    /// Interrupt nesting, assembly, startup, RTOS switch frames and ABI effects need separate allowances.</summary>
    public static IReadOnlyList<FunctionStackBound> Analyze(IReadOnlyList<StackUsageEntry> usage,
        IReadOnlyList<StackCall> calls, bool callGraphComplete = false)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(calls);
        var functions = usage.GroupBy(u => u.Function, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var graph = calls.GroupBy(c => c.Caller, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Callee).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var cache = new Dictionary<string, FunctionStackBound>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        FunctionStackBound Walk(string function)
        {
            if (cache.TryGetValue(function, out var cached)) return cached;
            if (!visiting.Add(function)) return new(function, null, 0, ["Recursive call cycle has no configured recursion bound."]);
            var reasons = new HashSet<string>(StringComparer.Ordinal);
            ulong own = 0;
            if (!functions.TryGetValue(function, out var entries))
            {
                reasons.Add($"Missing stack-usage evidence for {function}.");
            }
            else
            {
                own = entries.Max(e => e.Bytes);
                if (entries.Length != 1) reasons.Add($"Ambiguous function identity {function}; qualify same-name static functions.");
                if (entries.Any(e => !e.Bounded)) reasons.Add($"Unbounded dynamic stack allocation in {function}.");
            }
            if (!callGraphComplete) reasons.Add("The call graph has not been declared complete.");
            ulong deepest = 0;
            if (graph.TryGetValue(function, out var targets))
            {
                foreach (var target in targets)
                {
                    if (target is null) { reasons.Add($"Unresolved indirect call in {function}."); continue; }
                    var child = Walk(target);
                    deepest = Math.Max(deepest, child.KnownBytes);
                    foreach (var reason in child.Reasons) reasons.Add(reason);
                }
            }
            visiting.Remove(function);
            var known = checked(own + deepest);
            var result = new FunctionStackBound(function, reasons.Count == 0 ? known : null, known, reasons.Order(StringComparer.Ordinal).ToArray());
            cache[function] = result;
            return result;
        }
        foreach (var function in functions.Keys.Concat(graph.Keys).Concat(calls.Select(c => c.Callee).OfType<string>()).Distinct(StringComparer.Ordinal))
            Walk(function);
        return cache.Values.OrderBy(f => f.Function, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Checks RTOS task allocations, adding explicitly configured context and exception overhead.</summary>
    public static IReadOnlyList<TaskStackFinding> CheckTaskBudgets(IReadOnlyList<FunctionStackBound> functions,
        IReadOnlyList<TaskStackBudget> tasks)
    {
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(tasks);
        var lookup = functions.ToDictionary(f => f.Function, StringComparer.Ordinal);
        return tasks.Select(task =>
        {
            if (!lookup.TryGetValue(task.EntryFunction, out var function))
                return new TaskStackFinding(task.Name, null, task.AllocatedBytes, false, "Task entry has no stack analysis; budget is unverified.");
            var known = checked(function.KnownBytes + task.ExtraBytes);
            var required = function.WorstCaseBytes.HasValue ? checked(function.WorstCaseBytes.Value + task.ExtraBytes) : (ulong?)null;
            var exceeds = known > task.AllocatedBytes;
            var message = exceeds ? $"Known stack requirement {known} B exceeds {task.AllocatedBytes} B."
                : required.HasValue ? $"Estimated bound {required.Value} B of {task.AllocatedBytes} B, including the configured allowance."
                : "Worst-case stack is unknown: " + string.Join(" ", function.Reasons);
            return new TaskStackFinding(task.Name, required, task.AllocatedBytes, exceeds, message);
        }).ToArray();
    }

    [GeneratedRegex(@"^(.+):(\d+):(\d+):(.+)\t(\d+)\t(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex UsageLine();
}
