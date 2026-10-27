using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Python;

/// <summary>Why <c>pip install</c> failed.</summary>
public enum PipFailureKind
{
    /// <summary>The cause is not one TabStudio recognizes; the log says it.</summary>
    Unknown,

    /// <summary>A package had to be compiled, and the build failed.</summary>
    Build,

    /// <summary>A pinned version does not exist for this Python, or at all.</summary>
    NoMatchingVersion,

    /// <summary>Pinned versions conflict with each other.</summary>
    Conflict,

    /// <summary>The package index could not be reached.</summary>
    Network,

    /// <summary>The device ran out of storage.</summary>
    DiskFull,
}

/// <summary>
/// What a failed <c>pip install</c> printed, reduced to what a person can act on: which package failed, why, and
/// what its build was missing. pip's own report of a failed build is dozens of folded lines from the build
/// backend, ending in generic advice; this reads the lines that name the package and the missing compiler,
/// header or tool.
/// </summary>
/// <param name="Kind">Why it failed.</param>
/// <param name="Package">The package, as the requirement names it, such as <c>psycopg2</c>, or <see langword="null"/>.</param>
/// <param name="Version">Its version, when pip printed it, such as <c>2.9.9</c>.</param>
/// <param name="Reason">What was missing, in words, such as "PostgreSQL's pg_config was missing", or <see langword="null"/>.</param>
/// <param name="MissingHeaders">The C headers the compiler did not find, such as <c>ffi.h</c>.</param>
/// <param name="LastError">The last error line pip or the build printed, for a cause it does not recognize.</param>
public sealed partial record PipFailure(PipFailureKind Kind, string? Package, string? Version, string? Reason, IReadOnlyList<string> MissingHeaders, string? LastError)
{
    /// <summary>Whether installing build tools and libraries may fix it: a build that failed for want of them.</summary>
    public bool NeedsBuildTools => Kind == PipFailureKind.Build && (Reason is not null || MissingHeaders.Count > 0 || Package is not null);

    /// <summary>The package and version, such as <c>psycopg2 2.9.9</c>, or "A package".</summary>
    public string PackageText => Package is null ? "A package" : Version is null ? Package : $"{Package} {Version}";

    /// <summary>Reads pip's output.</summary>
    /// <param name="lines">Everything pip printed, standard output and error, in order.</param>
    public static PipFailure Analyze(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        string? collecting = null;
        string? collectingVersion = null;
        string? failed = null;
        string? failedVersion = null;
        string? reason = null;
        string? lastError = null;
        var headers = new List<string>();
        var kind = PipFailureKind.Unknown;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var text = line.TrimStart(' ', '│', '╰', '─', '>', '×', '|').Trim();
            if (Collecting().Match(text) is { Success: true } collect)
            {
                collecting = collect.Groups["name"].Value;
                collectingVersion = collect.Groups["version"].Success ? collect.Groups["version"].Value : null;
                continue;
            }

            if (Archive().Match(text) is { Success: true } archive && collecting is not null
                && PythonRequirements.Normalize(archive.Groups["name"].Value) == PythonRequirements.Normalize(collecting))
            {
                collectingVersion ??= archive.Groups["version"].Value;
                continue;
            }

            if (kind is PipFailureKind.Unknown or PipFailureKind.Build && BuildFailed().IsMatch(text))
            {
                kind = PipFailureKind.Build;
                if (failed is null && collecting is not null)
                {
                    (failed, failedVersion) = (collecting, collectingVersion);
                }
            }

            if (FailedPackage().Match(text) is { Success: true } named)
            {
                kind = PipFailureKind.Build;
                var name = named.Groups["name"].Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)[0];
                if (failed is null || PythonRequirements.Normalize(failed) != PythonRequirements.Normalize(name))
                {
                    failed = name;
                    failedVersion = collecting is not null && PythonRequirements.Normalize(collecting) == PythonRequirements.Normalize(name) ? collectingVersion : null;
                }
            }

            if (MissingHeader().Match(text) is { Success: true } header)
            {
                headers.Add(header.Groups["header"].Value);
                reason ??= header.Groups["header"].Value == "Python.h" ? "Python's headers were missing" : $"the C header {header.Groups["header"].Value} was missing";
            }
            else if (text.Contains("pg_config executable not found", StringComparison.Ordinal) || PgConfigMissing().IsMatch(text))
            {
                reason = "PostgreSQL's pg_config was missing";
            }
            else if (text.Contains("mysql_config not found", StringComparison.Ordinal) || text.Contains("Can not find valid pkg-config name", StringComparison.Ordinal))
            {
                reason = "the MySQL client library was missing";
            }
            else if (CompilerMissing().IsMatch(text))
            {
                reason ??= "the C compiler was missing";
            }
            else if (RustMissing().IsMatch(text))
            {
                reason ??= "a Rust compiler was missing";
            }

            if (NoVersion().Match(text) is { Success: true } none)
            {
                kind = PipFailureKind.NoMatchingVersion;
                failed ??= RequirementName().Match(none.Groups["requirement"].Value).Value;
                failedVersion ??= RequirementVersion().Match(none.Groups["requirement"].Value) is { Success: true } v ? v.Groups["version"].Value : null;
            }
            else if (text.Contains("ResolutionImpossible", StringComparison.Ordinal) || text.Contains("have conflicting dependencies", StringComparison.Ordinal))
            {
                kind = PipFailureKind.Conflict;
            }
            else if (kind == PipFailureKind.Unknown && Network().IsMatch(text))
            {
                kind = PipFailureKind.Network;
            }
            else if (text.Contains("No space left on device", StringComparison.Ordinal))
            {
                kind = PipFailureKind.DiskFull;
            }

            if (ErrorLine().IsMatch(text) && !Generic().IsMatch(text))
            {
                lastError = text.Length > 200 ? text[..200] + "…" : text;
            }
        }

        return new PipFailure(kind, failed, failedVersion, kind == PipFailureKind.Build ? reason : null, headers.Distinct(StringComparer.Ordinal).ToList(), lastError);
    }

    /// <summary>
    /// The first sentence of the message: which package failed and why, such as "psycopg2 2.9.9 could not be
    /// installed: it has no prebuilt wheel for Python 3.14 here, so pip compiled it, and PostgreSQL's pg_config
    /// was missing."
    /// </summary>
    /// <param name="python">The Python version, such as <c>3.14</c>.</param>
    /// <param name="where">Where it ran, such as "on this tablet".</param>
    public string Summary(string python, string where)
    {
        ArgumentNullException.ThrowIfNull(python);
        ArgumentNullException.ThrowIfNull(where);
        return Kind switch
        {
            PipFailureKind.Build => $"{PackageText} could not be installed: it has no prebuilt wheel for Python {python} {where}, so pip compiled it, and {Reason ?? "the build failed" + (LastError is null ? string.Empty : $" ({LastError})")}.",
            PipFailureKind.NoMatchingVersion => $"{PackageText} could not be installed: PyPI has no release of it that installs on Python {python}{(LastError is null ? string.Empty : $" ({LastError})")}.",
            PipFailureKind.Conflict => "The project's packages could not be installed: the versions it pins conflict with each other.",
            PipFailureKind.Network => "The project's packages could not be installed: the package index could not be reached. Check the connection.",
            PipFailureKind.DiskFull => "The project's packages could not be installed: the device ran out of storage.",
            _ => $"The project's packages could not be installed{(LastError is null ? "." : $": {LastError}")}",
        };
    }

    [GeneratedRegex(@"^Collecting (?<name>[A-Za-z0-9][A-Za-z0-9._-]*)(\[[^\]]*\])?(\s*==\s*(?<version>[A-Za-z0-9._+!-]+))?", RegexOptions.CultureInvariant)]
    private static partial Regex Collecting();

    [GeneratedRegex(@"^(Downloading|Using cached) (?<name>[A-Za-z0-9._]+?)-(?<version>\d[A-Za-z0-9._+!]*?)(\.tar\.gz|\.zip|\.tar\.bz2|-[^-]+-[^-]+-[^-]+\.whl)\b", RegexOptions.CultureInvariant)]
    private static partial Regex Archive();

    [GeneratedRegex(@"subprocess-exited-with-error|Getting requirements to build wheel did not run successfully|Preparing metadata \(pyproject\.toml\) did not run successfully|python setup\.py egg_info did not run successfully|Building wheel for \S+ .*error|Running setup\.py install for \S+ .*error", RegexOptions.CultureInvariant)]
    private static partial Regex BuildFailed();

    [GeneratedRegex(@"^(ERROR: Failed building wheel for|Failed to build|ERROR: Could not build wheels for|ERROR: Failed to build installable wheels for some pyproject\.toml based projects \(|ERROR: Failed to build '?)(?<name>[A-Za-z0-9][A-Za-z0-9._, -]*)", RegexOptions.CultureInvariant)]
    private static partial Regex FailedPackage();

    [GeneratedRegex(@"fatal error: (?<header>[\w/.+-]+\.h): No such file or directory", RegexOptions.CultureInvariant)]
    private static partial Regex MissingHeader();

    [GeneratedRegex(@"pg_config: (not found|No such file)|Could not find pg_config|You need to install postgresql-server-dev", RegexOptions.CultureInvariant)]
    private static partial Regex PgConfigMissing();

    [GeneratedRegex(@"error: command '[^']*(gcc|cc|clang|c\+\+|g\+\+)' failed|unable to execute '(gcc|cc|clang)'|(^|\s|/)(gcc|cc|clang|c\+\+|g\+\+): (not found|No such file)|C compiler cannot create executables|Unknown compiler\(s\)|no acceptable C compiler found", RegexOptions.CultureInvariant)]
    private static partial Regex CompilerMissing();

    [GeneratedRegex(@"can't find Rust compiler|error: can't find Rust|Rust not found|(^|\s)cargo: (not found|No such file)|Cargo, the Rust package manager, is not installed", RegexOptions.CultureInvariant)]
    private static partial Regex RustMissing();

    [GeneratedRegex(@"^ERROR: (No matching distribution found for|Could not find a version that satisfies the requirement) (?<requirement>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex NoVersion();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*", RegexOptions.CultureInvariant)]
    private static partial Regex RequirementName();

    [GeneratedRegex(@"==(?<version>[A-Za-z0-9._+!-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex RequirementVersion();

    [GeneratedRegex(@"Failed to establish a new connection|Temporary failure in name resolution|Name or service not known|No route to host|Network is unreachable|Read timed out|Could not fetch URL|ConnectTimeoutError", RegexOptions.CultureInvariant)]
    private static partial Regex Network();

    [GeneratedRegex(@"(^|:\s)(error|Error|ERROR)\b|fatal error", RegexOptions.CultureInvariant)]
    private static partial Regex ErrorLine();

    /// <summary>pip's generic closing lines, which say nothing about the cause.</summary>
    [GeneratedRegex(@"subprocess-exited-with-error|This error originates from a subprocess|See above for output|Failed building wheel|Could not build wheels|Failed to build installable wheels|metadata-generation-failed|exit code: \d+|ERROR: Failed to build", RegexOptions.CultureInvariant)]
    private static partial Regex Generic();
}
