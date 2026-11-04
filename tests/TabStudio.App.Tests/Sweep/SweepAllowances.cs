namespace TabStudio.App.Tests.Sweep;

/// <summary>
/// What the sweep of every command may find and still pass: what is right as it is, or belongs to another area of the
/// IDE and is tracked there, each with its reason. Everything else the sweep finds fails CI.
/// </summary>
internal static class SweepAllowances
{
    /// <summary>Whether a sweep that finds problems fails. CI enforces it, so what was fixed stays fixed.</summary>
    public static bool Enforced => true;

    /// <summary>Problems that are known and right, or left to their own area, by kind and a part of the text or place, with why.</summary>
    private static readonly (string Kind, string Text, string Reason)[] Allowed =
    [
        // Embedded commands on a C# console solution in the headless desktop host: each says why it cannot run here.
        ("error-log", "uses an Android USB-OTG connection", "Identify target needs a probe over USB-OTG; on a tablet it asks for one, and the desktop host has none."),
        ("error-log", "storage upload uses Android USB-OTG", "BOOTSEL upload needs a board over USB-OTG; the desktop host has none."),
        ("error-log", "Pause the matching on-chip GDB session", "Fault capture needs a halted on-chip debug session, and says so."),
        ("error-log", "Install MicroPython / CircuitPython", "The REPL needs mpremote from the Toolchain Store, and says so."),
        ("error-log", "Open the Python file to synchronize first", "Sync needs a Python file open, and says so."),
        ("error-log", "Open a Keil .uvprojx", "Import needs a vendor project open, and says so."),

        // Commands whose effect does not show on the screen, or that repeat the status they already show.
        ("no-effect", "Edit > Copy", "Copy changes only the clipboard."),
        ("no-effect", "Parameter Info", "The caret is not in a call's arguments, where Visual Studio shows nothing either."),
        ("no-effect", "Run on Save", "The Live Unit Testing trigger changes a setting the closed menu shows."),
        ("no-effect", "\"Save All\"", "Nothing is unsaved after the sweep puts the sample back."),
        ("no-effect", "> \"Output\"", "The Output tab is already the one shown."),
        ("no-effect", "Run Android App", "It repeats the status Build Android APK just showed: choose an Android startup project first."),
        ("no-effect", "Debug Tests in Context", "It repeats the status Run Tests in Context just showed: there are no tests at the caret of the sample."),
        ("no-effect", "Edit register field", "It repeats the status the register commands just showed: load the chip's SVD first."),

        // Left to their areas, in the pull request's remaining problems.
        ("no-effect", "Flutter Hot", "Build, run, debug: Flutter Hot Reload and Hot Restart stay enabled with no Flutter app running."),
        ("no-effect", "UiTestRecorderView > \"Record\"", "Swarm: Record stays enabled with no app running to record."),
        ("no-effect", "DatabaseExplorerView > \"Update Database\"", "Database Explorer: EF Core's Update Database is enabled with no EF Core project."),
        ("no-effect", "DatabaseExplorerView > \"Add\"", "Database Explorer: Add Migration is enabled with no EF Core project."),
        ("no-effect", "ContainersView > \"Stop\"", "Containers: Stop is enabled with no container selected."),
        ("no-effect", "ContainersView > \"Restart\"", "Containers: Restart is enabled with no container selected."),
        ("no-effect", "ContainersView > \"Start\"", "Containers: Start is enabled with no container selected."),
        ("no-effect", "ContainersView > \"Cancel\"", "Containers: the run form's Cancel lies under the form's content, left to the containers area."),
        ("covered", "ContainersView > \"Cancel\"", "Containers: the run form's Cancel lies under the form's content, left to the containers area."),

        // A splitter's grab area reaches a few pixels past its line by design, so a finger can find it.
        ("overlap", "lies under GridSplitter", "A splitter takes the few pixels at the edge of what it resizes, by design."),
    ];

    /// <summary>Whether a problem is on the list.</summary>
    public static bool IsAllowed(SweepProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return !Enforced || Allowed.Any(a => a.Kind == problem.Kind && (problem.What.Contains(a.Text, StringComparison.Ordinal) || problem.Where.Contains(a.Text, StringComparison.Ordinal)));
    }
}
