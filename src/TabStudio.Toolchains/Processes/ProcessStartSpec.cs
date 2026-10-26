namespace TabStudio.Toolchains.Processes;

/// <summary>The size of a pseudo terminal, in character cells.</summary>
/// <param name="Rows">The number of rows, at least 1.</param>
/// <param name="Columns">The number of columns, at least 1.</param>
public readonly record struct TerminalSize(int Rows, int Columns)
{
    /// <summary>The size terminals start at when nothing else is known: 24 rows of 80 columns.</summary>
    public static TerminalSize Default { get; } = new(24, 80);

    /// <summary>The same size with both dimensions clamped to what a terminal can have.</summary>
    public TerminalSize Clamped => new(Math.Clamp(Rows, 1, 1000), Math.Clamp(Columns, 1, 1000));
}

/// <summary>
/// What to run in a toolchain runtime: a program, its arguments, the folder it runs in, its environment and
/// whether it gets a pseudo terminal.
/// </summary>
/// <remarks>
/// Paths are the ones the IDE knows: <see cref="WorkingDirectory"/> is a folder on the device, such as a workspace
/// folder, and the runtime makes it visible to the program at the same path. <see cref="Program"/> is a command
/// name looked up on the program's <c>PATH</c>, or an absolute path as the program sees it.
/// </remarks>
public sealed record ProcessStartSpec
{
    /// <summary>The program: a command name such as <c>python3</c>, or an absolute path.</summary>
    public required string Program { get; init; }

    /// <summary>The arguments, passed to the program as they are, without a shell.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>The folder the program starts in, or <see langword="null"/> for the runtime's home folder.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Environment variables to set on top of the runtime's own; a <see langword="null"/> value removes a variable.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Runs the program in a pseudo terminal of this size, so it can use colors, move the cursor and read keys as
    /// they are typed; <see langword="null"/> connects its standard streams to pipes instead.
    /// </summary>
    public TerminalSize? Terminal { get; init; }

    /// <summary>The program and its arguments as one line, quoted where needed, for logs and the Output panel.</summary>
    public string CommandLine => string.Join(' ', new[] { Program }.Concat(Arguments).Select(Quote));

    private static string Quote(string argument) =>
        argument.Length > 0 && argument.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or '/' or '=' or ':' or '@' or '+' or ',')
            ? argument
            : "'" + argument.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
