using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Testing;

/// <summary>
/// Reads the results of the runners that write no JUnit XML: <c>go test -v</c> and <c>cargo test</c>, from the output
/// they print (which the run also shows in the Output panel), and <c>dart test</c> and <c>flutter test</c>, from the
/// JSON report of their <c>--file-reporter</c>.
/// </summary>
public static partial class ToolchainTestOutput
{
    /// <summary>
    /// The results in <c>go test -v</c>'s output: each top-level test's <c>--- PASS</c>, <c>--- FAIL</c> or
    /// <c>--- SKIP</c>, in the package of the <c>ok</c> or <c>FAIL</c> line that ends its package's output (go test
    /// prints each package's output whole when it tests several), and a package that did not build as one errored
    /// result. A test's class is its package, named from the module's last segment (<c>cobra.doc</c> for
    /// <c>github.com/spf13/cobra/doc</c>), as <see cref="GoPackageClass"/> names the tests found in source.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="module">The module path in go.mod, or <see langword="null"/>.</param>
    public static IReadOnlyList<ToolchainTestResult> ParseGo(string output, string? module)
    {
        ArgumentNullException.ThrowIfNull(output);
        var results = new List<ToolchainTestResult>();
        var pending = new List<(string Name, ToolchainTestOutcome Outcome, TimeSpan Duration)>();
        var logs = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        var packageLines = new StringBuilder();
        string? current = null;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (GoControl().Match(line) is { Success: true } control)
            {
                current = control.Groups["name"].Value.Split('/')[0];
                continue;
            }

            if (GoResult().Match(line) is { Success: true } result)
            {
                var name = result.Groups["name"].Value;
                var outcome = result.Groups["outcome"].Value switch
                {
                    "PASS" => ToolchainTestOutcome.Passed,
                    "SKIP" => ToolchainTestOutcome.Skipped,
                    _ => ToolchainTestOutcome.Failed,
                };
                var seconds = double.TryParse(result.Groups["seconds"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 0;
                pending.Add((name, outcome, TimeSpan.FromSeconds(seconds)));
                current = name;
                continue;
            }

            if (GoPackage().Match(line) is { Success: true } package)
            {
                var className = GoPackageClass(package.Groups["package"].Value, module);
                foreach (var (name, outcome, duration) in pending)
                {
                    var log = logs.GetValueOrDefault(name)?.ToString().Trim();
                    results.Add(new ToolchainTestResult(className, name, name, outcome, duration, outcome == ToolchainTestOutcome.Passed || string.IsNullOrEmpty(log) ? null : log, null, null));
                }

                // A package that did not build, or whose tests panicked before any ended, is one errored result.
                if (package.Groups["status"].Value == "FAIL" && !pending.Exists(p => p.Outcome == ToolchainTestOutcome.Failed))
                {
                    var message = packageLines.ToString().Trim();
                    results.Add(new ToolchainTestResult(className, "(package)", "(package)", ToolchainTestOutcome.Errored, TimeSpan.Zero,
                        message.Length > 0 ? message : line.Trim(), null, null));
                }

                pending.Clear();
                logs.Clear();
                packageLines.Clear();
                current = null;
                continue;
            }

            if (line is "PASS" or "FAIL" || line.StartsWith("=== ", StringComparison.Ordinal))
            {
                continue;
            }

            // A test's output, indented, between its start and its end; a package's build errors before its FAIL line.
            if (current is not null && line.StartsWith(' ') && !line.TrimStart().StartsWith("--- ", StringComparison.Ordinal))
            {
                if (!logs.TryGetValue(current, out var log))
                {
                    logs[current] = log = new StringBuilder();
                }

                log.AppendLine(line.Trim());
            }
            else if (!line.TrimStart().StartsWith("--- ", StringComparison.Ordinal))
            {
                packageLines.AppendLine(line);
            }
        }

        return results;
    }

    /// <summary>
    /// The test class of a Go package: its path in the module after the module's last segment, with dots, such as
    /// <c>cobra.doc</c>; a package outside the module keeps its whole path.
    /// </summary>
    /// <param name="package">The package's import path.</param>
    /// <param name="module">The module path, or <see langword="null"/>.</param>
    public static string GoPackageClass(string package, string? module)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (module is { Length: > 0 } && (package == module || package.StartsWith(module + "/", StringComparison.Ordinal)))
        {
            var name = module[(module.LastIndexOf('/') + 1)..];
            var relative = package[module.Length..].Trim('/');
            return relative.Length == 0 ? name : name + "." + relative.Replace('/', '.');
        }

        return package.Replace('/', '.');
    }

    /// <summary>The module path a go.mod declares, or <see langword="null"/>.</summary>
    /// <param name="directory">The module's folder.</param>
    public static string? GoModule(string directory)
    {
        var file = Path.Combine(directory, "go.mod");
        try
        {
            return File.Exists(file) && GoModuleLine().Match(File.ReadAllText(file)) is { Success: true } match ? match.Groups["path"].Value.Trim('"') : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The results in <c>cargo test</c>'s output: each <c>test path::name ... ok</c>, <c>FAILED</c> or <c>ignored</c>,
    /// in the class of its test binary's target and its module path (<c>anyhow.tests</c> for <c>tests::name</c> in
    /// anyhow's unit tests), with the failure's <c>---- name stdout ----</c> section as its message. Documentation tests
    /// are in the class <c>&lt;crate&gt;.doctests</c>.
    /// </summary>
    /// <param name="output">The output.</param>
    public static IReadOnlyList<ToolchainTestResult> ParseCargo(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var results = new List<(string Target, string Name, ToolchainTestOutcome Outcome)>();
        var messages = new Dictionary<(string, string), StringBuilder>();
        var target = "tests";
        StringBuilder? failure = null;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (CargoRunning().Match(line) is { Success: true } running)
            {
                target = CargoHash().Replace(Path.GetFileName(running.Groups["binary"].Value.TrimEnd(')')), string.Empty);
                failure = null;
            }
            else if (CargoDocTests().Match(line) is { Success: true } doc)
            {
                target = doc.Groups["crate"].Value + ".doctests";
                failure = null;
            }
            else if (CargoTest().Match(line) is { Success: true } test)
            {
                var outcome = test.Groups["outcome"].Value switch
                {
                    "ok" => ToolchainTestOutcome.Passed,
                    "FAILED" => ToolchainTestOutcome.Failed,
                    _ => ToolchainTestOutcome.Skipped,
                };
                results.Add((target, test.Groups["name"].Value, outcome));
            }
            else if (CargoFailureHeader().Match(line) is { Success: true } header)
            {
                messages[(target, header.Groups["name"].Value)] = failure = new StringBuilder();
            }
            else if (line is "failures:" || line.StartsWith("test result:", StringComparison.Ordinal))
            {
                failure = null;
            }
            else
            {
                failure?.AppendLine(line);
            }
        }

        return [.. results.Select(r =>
        {
            var (className, method) = r.Target.EndsWith(".doctests", StringComparison.Ordinal) ? (r.Target, r.Name) : CargoName(r.Target, r.Name);
            var message = messages.GetValueOrDefault((r.Target, r.Name))?.ToString().Trim();
            return new ToolchainTestResult(className, method, method, r.Outcome, TimeSpan.Zero, string.IsNullOrEmpty(message) ? null : message, null, null);
        })];
    }

    /// <summary>A Rust test's class and method: its target and module path, and its function.</summary>
    internal static (string ClassName, string Method) CargoName(string target, string path)
    {
        var parts = path.Split("::");
        return (string.Join('.', parts[..^1].Prepend(target)), parts[^1]);
    }

    /// <summary>
    /// The results in the JSON report of <c>dart test --file-reporter json:</c> (and <c>flutter test</c>'s): every
    /// test that is not hidden (a suite's loading is), in the class of its file (<c>test.parser_test</c>), with its name,
    /// which includes its groups, and the errors it reported.
    /// </summary>
    /// <param name="report">The report: one JSON event per line.</param>
    /// <param name="directory">The package folder, which the files' paths are relative to.</param>
    public static IReadOnlyList<ToolchainTestResult> ParseDart(string report, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        var suites = new Dictionary<int, string>();
        var tests = new Dictionary<int, (string ClassName, string Name, string? File, int? Line, DateTime Start)>();
        var errors = new Dictionary<int, StringBuilder>();
        var results = new List<ToolchainTestResult>();
        foreach (var line in report.Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using var owner = document;
            var root = document.RootElement;
            switch (root.TryGetProperty("type", out var type) ? type.GetString() : null)
            {
                case "suite":
                    var suite = root.GetProperty("suite");
                    if (suite.TryGetProperty("path", out var path) && path.GetString() is { } file)
                    {
                        suites[suite.GetProperty("id").GetInt32()] = file;
                    }

                    break;
                case "testStart":
                    var test = root.GetProperty("test");
                    var suiteFile = suites.GetValueOrDefault(test.GetProperty("suiteID").GetInt32()) ?? "test";
                    var relative = Path.IsPathRooted(suiteFile) ? Path.GetRelativePath(directory, suiteFile) : suiteFile;
                    var className = Path.ChangeExtension(relative, null).Replace('\\', '/').Replace('/', '.');
                    var line2 = test.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : (int?)null;
                    tests[test.GetProperty("id").GetInt32()] = (className, test.GetProperty("name").GetString() ?? string.Empty,
                        Path.IsPathRooted(suiteFile) ? suiteFile : Path.Combine(directory, suiteFile), line2, DateTime.MinValue);
                    break;
                case "error":
                    var id = root.GetProperty("testID").GetInt32();
                    if (!errors.TryGetValue(id, out var error))
                    {
                        errors[id] = error = new StringBuilder();
                    }

                    error.AppendLine(root.GetProperty("error").GetString()).AppendLine(root.TryGetProperty("stackTrace", out var stack) ? stack.GetString() : null);
                    break;
                case "testDone":
                    var done = root.GetProperty("testID").GetInt32();
                    if ((root.TryGetProperty("hidden", out var hidden) && hidden.GetBoolean()) || !tests.TryGetValue(done, out var started))
                    {
                        break;
                    }

                    var outcome = root.TryGetProperty("skipped", out var skipped) && skipped.GetBoolean() ? ToolchainTestOutcome.Skipped
                        : root.GetProperty("result").GetString() switch
                        {
                            "success" => ToolchainTestOutcome.Passed,
                            "failure" => ToolchainTestOutcome.Failed,
                            _ => ToolchainTestOutcome.Errored,
                        };
                    results.Add(new ToolchainTestResult(started.ClassName, started.Name, started.Name, outcome, TimeSpan.Zero,
                        errors.GetValueOrDefault(done)?.ToString().Trim(), started.File, started.Line));
                    break;
            }
        }

        return results;
    }

    [GeneratedRegex(@"^=== (?:RUN|CONT|PAUSE|NAME)\s+(?<name>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex GoControl();

    [GeneratedRegex(@"^--- (?<outcome>PASS|FAIL|SKIP): (?<name>[^\s/]+) \((?<seconds>[0-9.]+)s\)", RegexOptions.CultureInvariant)]
    private static partial Regex GoResult();

    [GeneratedRegex(@"^(?<status>ok|FAIL|\?)\s+(?<package>[^\s\[]+)(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex GoPackage();

    [GeneratedRegex(@"^\s*module\s+(?<path>\S+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GoModuleLine();

    [GeneratedRegex(@"^\s+Running\s+(?:unittests\s+)?(?:\S+\s+\()?(?<binary>[^\s()]+)\)?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CargoRunning();

    [GeneratedRegex(@"-[0-9a-f]{16}$", RegexOptions.CultureInvariant)]
    private static partial Regex CargoHash();

    [GeneratedRegex(@"^\s+Doc-tests\s+(?<crate>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex CargoDocTests();

    [GeneratedRegex(@"^test (?<name>.+?)(?: - should panic)? \.\.\. (?<outcome>ok|FAILED|ignored)", RegexOptions.CultureInvariant)]
    private static partial Regex CargoTest();

    [GeneratedRegex(@"^---- (?<name>.+?) stdout ----$", RegexOptions.CultureInvariant)]
    private static partial Regex CargoFailureHeader();
}
