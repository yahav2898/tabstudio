namespace TabStudio.App.Tests.Sweep;

/// <summary>
/// What the sweep of every command may find and still pass: problems that belong to another area of the IDE and are
/// tracked there, each with its reason. Everything else the sweep finds fails CI.
/// </summary>
internal static class SweepAllowances
{
    /// <summary>
    /// Whether a sweep that finds problems fails. Off while the first sweep collects what there is to fix; CI then
    /// enforces it, so what was fixed stays fixed.
    /// </summary>
    public static bool Enforced => false;

    /// <summary>Problems that are known and left to their own area, by kind and a part of the text, with why.</summary>
    private static readonly (string Kind, string Text, string Reason)[] Allowed =
    [
    ];

    /// <summary>Whether a problem is on the list.</summary>
    public static bool IsAllowed(SweepProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return !Enforced || Allowed.Any(a => a.Kind == problem.Kind && (problem.What.Contains(a.Text, StringComparison.Ordinal) || problem.Where.Contains(a.Text, StringComparison.Ordinal)));
    }
}
