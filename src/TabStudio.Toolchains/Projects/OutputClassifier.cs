using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Projects;

/// <summary>What a line a program or a build printed says, which decides its color in the Output panel.</summary>
public enum OutputLineKind
{
    /// <summary>Ordinary output, such as Cargo's <c>Compiling</c> progress or a program's own text.</summary>
    Normal,

    /// <summary>A warning, such as <c>warning: unused variable</c> or Maven's <c>[WARNING]</c>.</summary>
    Warning,

    /// <summary>An error, a panic, an uncaught exception or a traceback, with the lines that belong to it.</summary>
    Error,
}

/// <summary>
/// Colors output by what it says, not by the stream it arrived on. Compilers and package managers write their
/// progress to standard error (Cargo's <c>Compiling …</c>, pip's and Gradle's progress), and programs write
/// ordinary messages there too, so a red standard error hides the line that matters. A classifier reads one
/// stream's lines in order and recognizes compiler diagnostics (<c>error:</c>, <c>warning:</c>, <c>error[E0308]:</c>
/// and the source excerpts below them), build tools' levels (<c>[ERROR]</c>, <c>npm ERR!</c>, <c>FAILURE:</c>),
/// log levels, and the blocks of a crash: Python tracebacks, Rust and Go panics, Java and Node.js exceptions.
/// </summary>
/// <remarks>
/// A classifier keeps state between lines, for the lines of a block, so each stream of a run needs its own.
/// Patterns that are only safe for one toolchain, such as Go's <c>file.go:3:5: message</c> errors, apply to it alone.
/// </remarks>
public sealed partial class OutputClassifier
{
    private readonly string? _toolchain;
    private Block _block;
    private OutputLineKind _blockKind;

    /// <summary>Creates a classifier for one stream of a run.</summary>
    /// <param name="toolchainId">The toolchain that runs the program or build, such as <c>rust</c>, or <see langword="null"/> when it is not known.</param>
    public OutputClassifier(string? toolchainId = null) => _toolchain = toolchainId;

    private enum Block
    {
        None,

        /// <summary>A compiler diagnostic: indented excerpt lines follow until a blank line.</summary>
        Diagnostic,

        /// <summary>A Python traceback: indented frames, then the exception line, which ends it.</summary>
        PythonTraceback,

        /// <summary>A Rust panic: the message, then its note or backtrace.</summary>
        RustPanic,

        /// <summary>A JVM or Node.js exception: its <c>at</c> frames and causes.</summary>
        StackTrace,

        /// <summary>A Go panic: goroutine dumps until a blank line after the frames.</summary>
        GoPanic,
    }

    /// <summary>Classifies the next whole line of the stream.</summary>
    /// <param name="line">The line, without its line break.</param>
    /// <returns>What it is.</returns>
    public OutputLineKind Classify(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        line = line.TrimEnd('\r');
        if (Continue(line) is { } continued)
        {
            return continued;
        }

        return Start(line);
    }

    /// <summary>
    /// Classifies the start of a line that has not ended yet, such as a prompt, without changing the classifier's
    /// state: the whole line is classified when it ends.
    /// </summary>
    /// <param name="partial">The text of the line so far.</param>
    public OutputLineKind Peek(string partial)
    {
        ArgumentNullException.ThrowIfNull(partial);
        var block = _block;
        var kind = _blockKind;
        try
        {
            return Classify(partial);
        }
        finally
        {
            _block = block;
            _blockKind = kind;
        }
    }

    /// <summary>The kind of a line that continues the current block, or <see langword="null"/> when it ends it.</summary>
    private OutputLineKind? Continue(string line)
    {
        switch (_block)
        {
            case Block.Diagnostic:
                if (line.Length > 0 && !CargoProgress().IsMatch(line) && (char.IsWhiteSpace(line[0]) || ExcerptLine().IsMatch(line)))
                {
                    return _blockKind;
                }

                break;

            case Block.PythonTraceback:
                if (line.Length > 0 && char.IsWhiteSpace(line[0]))
                {
                    return OutputLineKind.Error;
                }

                // The exception, such as "ValueError: bad value", ends the traceback.
                if (line.Length > 0)
                {
                    _block = Block.None;
                    return OutputLineKind.Error;
                }

                break;

            case Block.RustPanic:
                if (line.Length == 0)
                {
                    break;
                }

                if (line.StartsWith("note: run with `RUST_BACKTRACE", StringComparison.Ordinal))
                {
                    _block = Block.None;
                }

                return OutputLineKind.Error;

            case Block.StackTrace:
                if (StackFrame().IsMatch(line) || line.StartsWith("Caused by:", StringComparison.Ordinal) || line.TrimStart().StartsWith("Suppressed:", StringComparison.Ordinal))
                {
                    return OutputLineKind.Error;
                }

                break;

            case Block.GoPanic:
                // A dump is blank lines between goroutines, their headers, the calls and their indented locations.
                if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line.StartsWith("goroutine ", StringComparison.Ordinal)
                    || line.StartsWith("created by ", StringComparison.Ordinal) || line.StartsWith("[signal ", StringComparison.Ordinal)
                    || GoFrame().IsMatch(line))
                {
                    return OutputLineKind.Error;
                }

                break;
        }

        _block = Block.None;
        return null;
    }

    private OutputLineKind Start(string line)
    {
        if (line.Length == 0 || CargoProgress().IsMatch(line))
        {
            return OutputLineKind.Normal;
        }

        if (line.StartsWith("Traceback (most recent call last):", StringComparison.Ordinal)
            || (_toolchain is "python" or null && PythonFrame().IsMatch(line)))
        {
            return Enter(Block.PythonTraceback, OutputLineKind.Error);
        }

        if (line.StartsWith("During handling of the above exception", StringComparison.Ordinal)
            || line.StartsWith("The above exception was the direct cause", StringComparison.Ordinal))
        {
            return OutputLineKind.Error;
        }

        if (RustPanic().IsMatch(line))
        {
            return Enter(Block.RustPanic, OutputLineKind.Error);
        }

        if (line.StartsWith("panic: ", StringComparison.Ordinal) || line.StartsWith("fatal error: all goroutines are asleep", StringComparison.Ordinal))
        {
            return Enter(_toolchain is "go" or null ? Block.GoPanic : Block.None, OutputLineKind.Error);
        }

        if (JavaException().IsMatch(line) || (_toolchain is "node" or null && NodeError().IsMatch(line)))
        {
            return Enter(Block.StackTrace, OutputLineKind.Error);
        }

        if (Diagnostic().Match(line) is { Success: true } diagnostic)
        {
            var kind = diagnostic.Groups["level"].Value.StartsWith('w') || diagnostic.Groups["level"].Value.StartsWith('W') ? OutputLineKind.Warning : OutputLineKind.Error;
            return Enter(Block.Diagnostic, kind);
        }

        if (_toolchain is "go" && GoCompilerError().IsMatch(line))
        {
            return OutputLineKind.Error;
        }

        if (ToolError().IsMatch(line) || LogLevel().Match(line) is { Success: true, Groups: var groups } && groups["level"].Value is "ERROR" or "FATAL" or "SEVERE" or "CRITICAL")
        {
            return OutputLineKind.Error;
        }

        if (ToolWarning().IsMatch(line) || LogLevel().Match(line) is { Success: true, Groups: var levels } && levels["level"].Value is "WARN" or "WARNING")
        {
            return OutputLineKind.Warning;
        }

        return OutputLineKind.Normal;
    }

    private OutputLineKind Enter(Block block, OutputLineKind kind)
    {
        _block = block;
        _blockKind = kind;
        return kind;
    }

    /// <summary>Cargo's, rustup's and Go's progress lines: a right-aligned verb, written to standard error.</summary>
    [GeneratedRegex(@"^\s*(Compiling|Checking|Finished|Running|Downloading|Downloaded|Updating|Locking|Adding|Fresh|Blocking|Building|Documenting|Installing|Installed|Replacing|Packaging|Verifying|Archiving|Uploading|Removed|Removing|Unpacking|Doc-tests|Executable|Starting|Waiting|Packaged|Summary|Fetching|Fetched|Vendoring)\s", RegexOptions.CultureInvariant)]
    private static partial Regex CargoProgress();

    /// <summary>A compiler excerpt line, such as <c>12 |     let x = 1;</c> or <c>   = note: …</c>.</summary>
    [GeneratedRegex(@"^\s*(\d+\s*)?[|=^~]", RegexOptions.CultureInvariant)]
    private static partial Regex ExcerptLine();

    /// <summary>
    /// A compiler or tool diagnostic: <c>error:</c>, <c>error[E0308]:</c>, <c>warning:</c> and <c>fatal error:</c>
    /// at the start of the line, or after a location such as <c>src/main.c:3:5:</c>.
    /// </summary>
    [GeneratedRegex(@"^(\s*|.*?:\d+(:\d+)?:\s*)(?<level>(fatal )?error|warning|Error|Warning)(\[[\w-]+\])?:\s", RegexOptions.CultureInvariant)]
    private static partial Regex Diagnostic();

    /// <summary>Build tools' error lines: Maven, npm, pip, Gradle, Make and Kotlin.</summary>
    [GeneratedRegex(@"^(\[ERROR\]|\[FATAL\]|ERROR:|ERROR\s|npm ERR!|npm error|FAILURE: |BUILD FAILURE|BUILD FAILED|e: |make(\[\d+\])?: \*\*\*|error: |\s*× )", RegexOptions.CultureInvariant)]
    private static partial Regex ToolError();

    /// <summary>Build tools' warning lines: Maven, npm, pip, Gradle and Kotlin, and Python's warnings.</summary>
    [GeneratedRegex(@"^(\[WARNING\]|\[WARN\]|WARNING:|npm WARN|npm warn|w: |.*:\d+: \w*Warning: )", RegexOptions.CultureInvariant)]
    private static partial Regex ToolWarning();

    /// <summary>A timestamped log line's level, as Spring Boot, Log4j and Python's logging print it.</summary>
    [GeneratedRegex(@"^(\d{4}-\d\d-\d\d[T ][\d:.,]+\S*|\[[^\]]+\])\s+\[?(?<level>ERROR|FATAL|SEVERE|CRITICAL|WARN|WARNING)\]?\s", RegexOptions.CultureInvariant)]
    private static partial Regex LogLevel();

    /// <summary>A frame of a Python traceback, which a syntax error prints without the traceback's header.</summary>
    [GeneratedRegex(@"^\s+File "".+"", line \d+", RegexOptions.CultureInvariant)]
    private static partial Regex PythonFrame();

    /// <summary>
    /// Rust's panic message, such as <c>thread 'main' panicked at src/main.rs:2:5:</c>, with the thread's id that newer
    /// Rust versions add: <c>thread 'main' (29844) panicked at …</c>.
    /// </summary>
    [GeneratedRegex(@"^thread '[^']*'(?: \(\d+\))? panicked at ", RegexOptions.CultureInvariant)]
    private static partial Regex RustPanic();

    /// <summary>A JVM exception: <c>Exception in thread "main" java.lang.IllegalStateException: …</c>.</summary>
    [GeneratedRegex(@"^(Exception in thread "".*"" |Caused by: )[\w.$]+", RegexOptions.CultureInvariant)]
    private static partial Regex JavaException();

    /// <summary>An uncaught Node.js error, such as <c>TypeError: x is not a function</c> or <c>Uncaught Error: …</c>.</summary>
    [GeneratedRegex(@"^(Uncaught )?([A-Z]\w*)?Error( \[\w+\])?: ", RegexOptions.CultureInvariant)]
    private static partial Regex NodeError();

    /// <summary>A stack frame of a JVM or Node.js exception, or the frames it elided.</summary>
    [GeneratedRegex(@"^\s+(at |\.\.\. \d+ (more|common frames omitted))", RegexOptions.CultureInvariant)]
    private static partial Regex StackFrame();

    /// <summary>A call in a Go goroutine dump, such as <c>main.main()</c> or <c>main.(*T).Run(0x1, …)</c>.</summary>
    [GeneratedRegex(@"^[\w./*()\[\]-]+\(.*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex GoFrame();

    /// <summary>Go's compiler errors: a location and the message, without a level.</summary>
    [GeneratedRegex(@"^\S+\.go:\d+:\d+: ", RegexOptions.CultureInvariant)]
    private static partial Regex GoCompilerError();
}
