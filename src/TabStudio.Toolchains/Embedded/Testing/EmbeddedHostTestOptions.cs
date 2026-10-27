using System.Text.RegularExpressions;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Testing;

/// <summary>A workspace's explicit native host-test invocation; never a shell command.</summary>
public sealed record EmbeddedHostTestOptions
{
    /// <summary>The format emitted by the host harness.</summary>
    public EmbeddedTestFormat Format { get; init; } = EmbeddedTestFormat.Unity;

    /// <summary>A Unity, TAP or GoogleTest executable; Cargo is selected automatically for Rust.</summary>
    public string? Executable { get; init; }

    /// <summary>Arguments passed as individual strings to the executable, or to cargo test before its harness arguments.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>An optional explicit Cargo host target; otherwise the installed rustc host is queried.</summary>
    public string? HostTarget { get; init; }

    /// <summary>The host identity shown separately from physical board results.</summary>
    public string HostId { get; init; } = "host";
}

/// <summary>Plans host tests with argument vectors and explicitly overrides embedded Cargo targets.</summary>
public static class EmbeddedHostTestPlanner
{
    /// <summary>The workspace-local file describing host tests.</summary>
    public const string OptionsFile = ".tabstudio/embedded-tests.json";

    /// <summary>Reads the installed rustc's host triple from rustc -vV, without inferring it from the tablet OS.</summary>
    /// <param name="output">rustc -vV output.</param>
    public static string RustHostTarget(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Length > 64 * 1024)
        {
            throw new InvalidDataException("rustc version output exceeds 64 KB.");
        }
        var match = Regex.Match(output, @"(?m)^host:\s*([a-zA-Z0-9_.-]+)\s*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups[1].Value : throw new InvalidDataException("rustc did not report its host target.");
    }

    /// <summary>Plans tests against the host, avoiding an inherited Cortex-M/RISC-V build target.</summary>
    /// <param name="directory">The project directory.</param>
    /// <param name="options">The explicit test configuration.</param>
    /// <param name="rustHostTarget">The actual rustc host triple when not specified in options.</param>
    /// <param name="googleTestReport">A fresh report path for GoogleTest's XML output.</param>
    public static ProcessStartSpec Plan(string directory, EmbeddedHostTestOptions options,
        string? rustHostTarget = null, string? googleTestReport = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.Format))
        {
            throw new ArgumentException("Unknown embedded test format.", nameof(options));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(options.HostId);
        if (options.Arguments is null || options.Arguments.Any(a => a is null || a.Any(char.IsControl)))
        {
            throw new ArgumentException("Test arguments must be individual strings without control characters.", nameof(options));
        }
        if (options.Format == EmbeddedTestFormat.Rust)
        {
            var host = options.HostTarget ?? rustHostTarget;
            if (host is null || !Regex.IsMatch(host, @"^[A-Za-z0-9_.]+(?:-[A-Za-z0-9_.]+){2,}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
                || host.Contains("none", StringComparison.Ordinal))
            {
                throw new ArgumentException("Host-side Rust tests require the installed rustc's host triple, not a bare-metal target.", nameof(rustHostTarget));
            }
            if (options.Arguments.Any(a => a == "--" || a.StartsWith("--target", StringComparison.Ordinal)))
            {
                throw new ArgumentException("Configure HostTarget separately; test arguments cannot override the host target or harness format.", nameof(options));
            }
            return new ProcessStartSpec
            {
                Program = "cargo", Arguments = ["test", "--target", host, .. options.Arguments, "--", "--color", "never"],
                WorkingDirectory = directory,
            };
        }
        if (options.Format == EmbeddedTestFormat.DefmtTest)
        {
            throw new ArgumentException("defmt-test is an on-target harness; import its actual decoded target log instead.", nameof(options));
        }
        if (options.Executable is not { Length: > 0 } executable || executable.StartsWith('-') || executable.Any(char.IsControl))
        {
            throw new ArgumentException("Set Executable to the host test binary's path.", nameof(options));
        }
        var program = Path.GetFullPath(executable, directory);
        var arguments = options.Arguments.ToList();
        if (options.Format == EmbeddedTestFormat.GoogleTest)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(googleTestReport);
            if (arguments.Any(a => a.StartsWith("--gtest_output", StringComparison.Ordinal)))
            {
                throw new ArgumentException("GoogleTest report output is assigned a fresh file per run.", nameof(options));
            }
            arguments.Add("--gtest_output=xml:" + Path.GetFullPath(googleTestReport, directory));
            arguments.Add("--gtest_color=no");
        }
        return new ProcessStartSpec { Program = program, Arguments = arguments, WorkingDirectory = directory };
    }
}
