using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Testing;

/// <summary>A toolchain project's test run: the command, and where its JUnit XML reports are written.</summary>
/// <param name="Framework">The runner.</param>
/// <param name="Command">The command, run in the project's folder.</param>
/// <param name="ReportDirectory">The folder of this run's own reports (PHPUnit and SwiftPM write them there).</param>
public sealed record ToolchainTestPlan(ToolchainTestFramework Framework, ProcessStartSpec Command, string ReportDirectory);

/// <summary>
/// Runs a toolchain project's tests with its standard runner, as its own documentation does, and reads their results
/// from the JUnit XML reports the runner writes: <c>vendor/bin/phpunit --log-junit</c> (or Pest), Gradle's
/// <c>test</c> tasks, Maven's Surefire, <c>swift test --xunit-output</c> and <c>pytest --junitxml</c>, and from the
/// output of <c>go test -v</c> and <c>cargo test</c> and the JSON reports of <c>dart test</c>, <c>flutter test</c> and
/// Jest (see <see cref="ToolchainTestOutput"/>); Vitest, Mocha and <c>node --test</c> write JUnit XML.
/// </summary>
public static partial class ToolchainTestPlanner
{
    /// <summary>
    /// The Gradle init script of a test run: every <c>test</c> task runs, even when Gradle thinks it is up to date,
    /// writes its XML report, and does not stop the build on a failed test or a filter a module has no tests for,
    /// so every module's results are read.
    /// </summary>
    public const string GradleInit = """
        allprojects {
            tasks.withType(Test).configureEach {
                ignoreFailures = true
                filter.failOnNoMatchingTests = false
                outputs.upToDateWhen { false }
                reports.junitXml.required.set(true)
            }
        }
        """;

    /// <summary>
    /// Runs pytest with the project's Python, installing it first when the project names it only among its
    /// development requirements, which the project's own install leaves out.
    /// </summary>
    internal const string PytestScript =
        "if ! \"$0\" -c 'import pytest' 2> /dev/null; then echo 'Installing pytest, which the tests need'; "
        + "\"$0\" -m pip install --disable-pip-version-check -q pytest || exit 1; fi; exec \"$0\" -m pytest \"$@\"";

    /// <summary>
    /// Runs a test runner that writes no report file of its own and keeps its output, as the Output panel shows it, in the
    /// report file named first (<c>$0</c>), with the runner's exit code: <c>go test -v</c> and <c>cargo test</c>.
    /// </summary>
    internal const string TeeScript =
        "report=\"$0\"; { \"$@\"; echo $? > \"$report.status\"; } 2>&1 | tee \"$report\"; exit \"$(cat \"$report.status\" 2> /dev/null || echo 1)\"";

    private static readonly string[] SkippedFolders = [".git", ".gradle", ".idea", ".tabstudio", "node_modules", "vendor", ".build"];

    /// <summary>The files that name pytest in a Python project: its requirements, its build file and its test settings.</summary>
    private static readonly string[] PythonTestSettings =
        ["pyproject.toml", "setup.cfg", "setup.py", "tox.ini", "Pipfile", "requirements.txt", "requirements-dev.txt", "requirements-test.txt", "dev-requirements.txt", "test-requirements.txt", "requirements/dev.txt", "requirements/test.txt", "requirements/testing.txt"];

    /// <summary>
    /// The runner of a project's tests, or <see langword="null"/> when it has none TabStudio runs: PHPUnit or Pest
    /// in a Composer project that requires it, JUnit tests in a Gradle or Maven build with <c>src/test</c>, a
    /// Swift package with test targets, a Python project that uses pytest, a Go module with <c>_test.go</c> files, a Rust
    /// package with tests, a Dart or Flutter package with a <c>test</c> folder, and a Node.js package tested with Vitest,
    /// Jest, Mocha or <c>node --test</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    public static ToolchainTestFramework? Detect(ToolchainProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var directory = project.Directory;
        switch (project.Toolchain.Id)
        {
            case "php":
                var composer = Path.Combine(directory, "composer.json");
                var manifest = File.Exists(composer) ? ReadSmall(composer) : string.Empty;
                if (manifest.Contains("\"pestphp/pest\"", StringComparison.Ordinal))
                {
                    return ToolchainTestFramework.Pest;
                }

                return manifest.Contains("\"phpunit/phpunit\"", StringComparison.Ordinal)
                    || File.Exists(Path.Combine(directory, "phpunit.xml")) || File.Exists(Path.Combine(directory, "phpunit.xml.dist"))
                    ? ToolchainTestFramework.PhpUnit : null;
            case "java" or "kotlin":
                if (!HasJvmTests(directory))
                {
                    return null;
                }

                return File.Exists(Path.Combine(directory, "build.gradle.kts")) || File.Exists(Path.Combine(directory, "build.gradle"))
                    || File.Exists(Path.Combine(directory, "settings.gradle.kts")) || File.Exists(Path.Combine(directory, "settings.gradle"))
                    ? ToolchainTestFramework.Gradle
                    : File.Exists(Path.Combine(directory, "pom.xml")) ? ToolchainTestFramework.Maven : null;
            case "swift":
                return SwiftProject.HasTests(directory) ? ToolchainTestFramework.SwiftPM : null;
            case "python":
                return File.Exists(Path.Combine(directory, "pytest.ini")) || File.Exists(Path.Combine(directory, "conftest.py"))
                    || PythonTestSettings.Any(file => ReadSmall(Path.Combine(directory, file)).Contains("pytest", StringComparison.OrdinalIgnoreCase))
                    ? ToolchainTestFramework.Pytest : null;
            case "go":
                return File.Exists(Path.Combine(directory, "go.mod")) && HasFile(directory, "*_test.go") ? ToolchainTestFramework.GoTest : null;
            case "rust":
                return File.Exists(Path.Combine(directory, "Cargo.toml"))
                    && (Directory.Exists(Path.Combine(directory, "tests")) || HasFile(directory, "*.rs", text => text.Contains("#[test]", StringComparison.Ordinal) || text.Contains("#[cfg(test)]", StringComparison.Ordinal)))
                    ? ToolchainTestFramework.Cargo : null;
            case "node":
                return NodeFramework(directory);
            case "flutter":
                return File.Exists(Path.Combine(directory, "pubspec.yaml")) && Directory.Exists(Path.Combine(directory, "test"))
                    ? FlutterProject.UsesFlutter(directory) ? ToolchainTestFramework.FlutterTest : ToolchainTestFramework.DartTest
                    : null;
            default:
                return null;
        }
    }

    /// <summary>A run of a project's tests, all of them or those selected.</summary>
    /// <param name="project">The project.</param>
    /// <param name="framework">Its runner, from <see cref="Detect"/>.</param>
    /// <param name="selection">The tests to run, or <see langword="null"/> or empty for all of them.</param>
    /// <param name="reportDirectory">An empty folder for this run's reports.</param>
    public static ToolchainTestPlan Plan(ToolchainProject project, ToolchainTestFramework framework, IReadOnlyList<ToolchainTestSelector>? selection, string reportDirectory)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(reportDirectory);
        Directory.CreateDirectory(reportDirectory);
        var selected = selection is { Count: > 0 } ? selection : null;
        List<string> command;
        switch (framework)
        {
            case ToolchainTestFramework.PhpUnit or ToolchainTestFramework.Pest:
                var runner = framework == ToolchainTestFramework.Pest ? "vendor/bin/pest" : "vendor/bin/phpunit";
                command = ["php", runner, "--colors=never", "--log-junit", Path.Combine(reportDirectory, "phpunit.xml")];
                if (selected is not null)
                {
                    command.AddRange(["--filter", PhpUnitFilter(selected)]);
                }

                break;
            case ToolchainTestFramework.Gradle:
                var init = Path.Combine(reportDirectory, "tabstudio-tests.init.gradle");
                File.WriteAllText(init, GradleInit);
                // A Kotlin Multiplatform build has no test task: its JVM target's tests are jvmTest (clikt,
                // kotlinx-datetime); allTests would also need browsers and native toolchains.
                command = ["gradle", "--no-daemon", "--console=plain", "--continue", "--init-script", init, IsKotlinMultiplatform(project.Directory) ? "jvmTest" : "test"];
                foreach (var test in selected ?? [])
                {
                    command.AddRange(["--tests", test.MethodName is null ? test.ClassName : test.ClassName + "." + test.MethodName]);
                }

                break;
            case ToolchainTestFramework.Maven:
                command = ["mvn", "-B", "test", "-Dmaven.test.failure.ignore=true", "-DfailIfNoTests=false", "-Dsurefire.failIfNoSpecifiedTests=false"];
                if (selected is not null)
                {
                    command.Add("-Dtest=" + MavenFilter(selected));
                }

                break;
            case ToolchainTestFramework.SwiftPM:
                // XCTest writes its report only when its tests run in parallel; Swift Testing writes its own beside it.
                command = ["swift", "test", "--parallel", "--xunit-output", Path.Combine(reportDirectory, "swift.xml"), "--experimental-xunit-message-failure"];
                if (selected is not null)
                {
                    command.AddRange(["--filter", SwiftFilter(selected)]);
                }

                break;
            case ToolchainTestFramework.Pytest:
                command = ["sh", "-c", PythonTestInstall(PythonTestRequirements(project.Directory)) + PytestScript, "{python}", "--color=no", "--junitxml=" + Path.Combine(reportDirectory, "pytest.xml")];
                if (selected is not null)
                {
                    command.AddRange(PytestNodeIds(project.Directory, selected));
                }

                break;
            case ToolchainTestFramework.GoTest:
                command = ["sh", "-c", TeeScript, Path.Combine(reportDirectory, GoReport), "go", "test", "-v"];
                command.AddRange(GoArguments(ToolchainTestOutput.GoModule(project.Directory), selected));
                break;
            case ToolchainTestFramework.Cargo:
                command = ["sh", "-c", TeeScript, Path.Combine(reportDirectory, CargoReport), "cargo", "test", "--no-fail-fast"];
                if (ReadSmall(Path.Combine(project.Directory, "Cargo.toml")).Contains("[workspace]", StringComparison.Ordinal))
                {
                    command.Add("--workspace");
                }

                if (selected is not null)
                {
                    // libtest runs the tests whose path contains any of the filters.
                    command.Add("--");
                    command.AddRange(selected.Select(CargoFilter).Distinct(StringComparer.Ordinal));
                }

                break;
            case ToolchainTestFramework.Vitest:
                command = ["npx", "--no-install", "vitest", "run", "--reporter=default", "--reporter=junit", "--outputFile.junit=" + Path.Combine(reportDirectory, "vitest.xml")];
                if (selected is not null)
                {
                    command.AddRange(["-t", NodeNamePattern(selected)]);
                }

                break;
            case ToolchainTestFramework.Jest:
                command = ["npx", "--no-install", "jest", "--ci", "--json", "--outputFile=" + Path.Combine(reportDirectory, JestReport)];
                if (selected is not null)
                {
                    command.AddRange(["-t", NodeNamePattern(selected)]);
                }

                break;
            case ToolchainTestFramework.Mocha:
                // The package's own mocha command (its files and --require), reporting to a JUnit file instead of the console.
                command = ["sh", "-c", "exec npx --no-install mocha \"$@\" " + MochaArguments(TestScript(project.Directory)), "mocha",
                    "--reporter", "xunit", "--reporter-option", "output=" + Path.Combine(reportDirectory, "mocha.xml")];
                if (selected is not null)
                {
                    command.AddRange(["--grep", NodeNamePattern(selected)]);
                }

                break;
            case ToolchainTestFramework.NodeTest:
                // Node's test runner writes JUnit XML beside its console report; the files come from the package's script.
                command = ["sh", "-c", "exec node --test \"$@\" " + NodeTestArguments(TestScript(project.Directory)), "node",
                    "--test-reporter=spec", "--test-reporter-destination=stdout", "--test-reporter=junit", "--test-reporter-destination=" + Path.Combine(reportDirectory, "node.xml")];
                if (selected is not null)
                {
                    command.AddRange(["--test-name-pattern", NodeNamePattern(selected)]);
                }

                break;
            case ToolchainTestFramework.DartTest or ToolchainTestFramework.FlutterTest:
                command = [framework == ToolchainTestFramework.FlutterTest ? "flutter" : "dart", "test", "--file-reporter", "json:" + Path.Combine(reportDirectory, DartReport)];
                if (selected is not null)
                {
                    command.AddRange(["--name", "^(?:" + string.Join('|', selected.Select(s => QuoteRegex(s.MethodName ?? string.Empty) + (s.MethodName is null ? string.Empty : "$")).Distinct(StringComparer.Ordinal)) + ")"]);
                    command.AddRange(selected.Select(s => s.ClassName.Replace('.', '/') + ".dart").Distinct(StringComparer.Ordinal).Where(f => File.Exists(Path.Combine(project.Directory, f))));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(framework), framework, null);
        }

        return new ToolchainTestPlan(framework, RunPlanner.CommandSpec(project, command), reportDirectory);
    }

    /// <summary>
    /// The results of a run: from its own reports, and, for Gradle and Maven, from the reports of every module
    /// written since it started.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="plan">The run.</param>
    /// <param name="startedUtc">When it started.</param>
    public static IReadOnlyList<ToolchainTestResult> ReadResults(ToolchainProject project, ToolchainTestPlan plan, DateTime startedUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(plan);
        switch (plan.Framework)
        {
            case ToolchainTestFramework.GoTest:
                return ToolchainTestOutput.ParseGo(ReadReport(plan, GoReport), ToolchainTestOutput.GoModule(project.Directory));
            case ToolchainTestFramework.Cargo:
                return ToolchainTestOutput.ParseCargo(ReadReport(plan, CargoReport));
            case ToolchainTestFramework.DartTest or ToolchainTestFramework.FlutterTest:
                return ToolchainTestOutput.ParseDart(ReadReport(plan, DartReport), project.Directory);
            case ToolchainTestFramework.Jest:
                return ToolchainTestOutput.ParseJest(ReadReport(plan, JestReport), project.Directory);
        }

        IEnumerable<string> reports = plan.Framework switch
        {
            ToolchainTestFramework.Gradle => FindReports(project.Directory, path => path.Contains($"{Path.DirectorySeparatorChar}test-results{Path.DirectorySeparatorChar}", StringComparison.Ordinal), startedUtc),
            ToolchainTestFramework.Maven => FindReports(project.Directory, path => path.Contains($"{Path.DirectorySeparatorChar}surefire-reports{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && Path.GetFileName(path).StartsWith("TEST-", StringComparison.Ordinal), startedUtc),
            _ => Directory.Exists(plan.ReportDirectory) ? Directory.EnumerateFiles(plan.ReportDirectory, "*.xml") : [],
        };
        var results = new List<ToolchainTestResult>();
        foreach (var report in reports.Order(StringComparer.Ordinal))
        {
            try
            {
                results.AddRange(JUnitReport.Parse(File.ReadAllText(report)));
            }
            catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
            {
                // A report cut off by a crash or a cancelled run: the results it has are read from the others.
            }
        }

        return results;
    }

    /// <summary>
    /// What a Python project's tests need besides the project, as it declares them: its test and development
    /// requirement files (<c>-r requirements-dev.txt</c>), the test extras of its own package (<c>-e .[tests]</c>, from
    /// <c>[project.optional-dependencies]</c>, <c>setup.cfg</c> or <c>setup.py</c>), and the requirements of its
    /// <c>test</c> and <c>dev</c> dependency groups (<c>[dependency-groups]</c>, PEP 735). The project's own install
    /// leaves them out, so attrs, marshmallow, httpie, requests and rich failed to collect their tests.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    internal static List<string> PythonTestRequirements(string directory)
    {
        var arguments = new List<string>();
        foreach (var file in PythonTestRequirementFiles.Where(f => File.Exists(Path.Combine(directory, f))).Concat(OtherTestRequirementFiles(directory)).Distinct(StringComparer.Ordinal))
        {
            arguments.AddRange(["-r", file]);
        }

        // Comment lines go first: an apostrophe in one ("doesn't") would start a string.
        var pyproject = TomlComment().Replace(ReadSmall(Path.Combine(directory, "pyproject.toml")), string.Empty);
        var extras = TomlTable(pyproject, "project.optional-dependencies").Keys
            .Concat(SetupExtras(ReadSmall(Path.Combine(directory, "setup.cfg")) + "\n" + ReadSmall(Path.Combine(directory, "setup.py"))))
            .Where(IsTestGroup).Distinct(StringComparer.Ordinal).ToList();
        if (extras.Count > 0)
        {
            arguments.AddRange(["-e", ".[" + string.Join(',', extras) + "]"]);
        }

        // Poetry's development dependencies name packages with Poetry's version syntax, which pip does not read.
        foreach (var table in new[] { "tool.poetry.dev-dependencies", "tool.poetry.group.dev.dependencies", "tool.poetry.group.test.dependencies", "tool.poetry.group.tests.dependencies" })
        {
            arguments.AddRange(TomlTable(pyproject, table).Keys.Where(name => name != "python" && !arguments.Contains(name)).ToList());
        }

        foreach (var (_, value) in TomlTable(pyproject, "dependency-groups").Where(g => IsTestGroup(g.Key)))
        {
            // A group's {include-group = "..."} entries name other groups, not packages.
            var requirements = TomlStrings().Matches(TomlInlineTable().Replace(value, string.Empty)).Select(m => m.Groups["value"].Value);
            arguments.AddRange(requirements.Where(r => r.Length > 0 && !arguments.Contains(r)).ToList());
        }

        return arguments;
    }

    private static readonly string[] PythonTestRequirementFiles =
    [
        "requirements-dev.txt", "requirements-test.txt", "requirements-tests.txt", "requirements_dev.txt", "requirements_test.txt",
        "test-requirements.txt", "test_requirements.txt", "dev-requirements.txt", "requirements/dev.txt", "requirements/test.txt",
        "requirements/tests.txt", "requirements/testing.txt", "tests/requirements.txt",
    ];

    /// <summary>
    /// Test requirement files elsewhere than the usual names: <c>*requirements*test*.txt</c> and
    /// <c>*test*requirements*.txt</c> in the project folder or a folder of it, such as tqdm's
    /// <c>.meta/requirements-test.txt</c>.
    /// </summary>
    private static List<string> OtherTestRequirementFiles(string directory)
    {
        var found = new List<string>();
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(directory).Where(d => !SkippedFolders.Contains(Path.GetFileName(d)) && Path.GetFileName(d) is not (".venv" or "venv" or ".git")).Prepend(directory).Take(64))
            {
                found.AddRange(Directory.EnumerateFiles(folder, "*.txt")
                    .Where(f => Path.GetFileName(f) is var name && name.Contains("requirements", StringComparison.OrdinalIgnoreCase) && name.Contains("test", StringComparison.OrdinalIgnoreCase))
                    .Select(f => Path.GetRelativePath(directory, f).Replace(Path.DirectorySeparatorChar, '/')));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only the usual files.
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private static bool IsTestGroup(string name) =>
        name.Trim('"', '\'') is "test" or "tests" or "testing" or "dev" or "develop" or "development";

    /// <summary>The <c>pip install</c> of a project's test requirements before pytest runs; nothing when it has none.</summary>
    internal static string PythonTestInstall(IReadOnlyList<string> requirements) =>
        requirements.Count == 0 ? string.Empty
        : "echo 'Installing what the tests need'; \"$0\" -m pip install --disable-pip-version-check -q "
            + string.Join(' ', requirements.Select(r => "'" + r.Replace("'", "'\\''", StringComparison.Ordinal) + "'"))
            + " || echo 'Some of what the tests need could not be installed'; ";

    /// <summary>The keys and values of a TOML table, such as <c>[project.optional-dependencies]</c>, each value as written.</summary>
    private static Dictionary<string, string> TomlTable(string toml, string table)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var start = toml.IndexOf("[" + table + "]", StringComparison.Ordinal);
        if (start < 0)
        {
            return result;
        }

        var body = toml[(start + table.Length + 2)..];
        var next = TomlTableHeader().Match(body);
        body = next.Success ? body[..next.Index] : body;
        foreach (Match entry in TomlEntry().Matches(body))
        {
            result[entry.Groups["key"].Value.Trim('"', '\'')] = entry.Groups["value"].Value;
        }

        return result;
    }

    /// <summary>The extras of a <c>setup.cfg</c> (<c>[options.extras_require]</c>) or <c>setup.py</c> (<c>extras_require</c>).</summary>
    private static IEnumerable<string> SetupExtras(string text) =>
        SetupExtra().Matches(text).Select(m => m.Groups["name"].Value);

    [GeneratedRegex(@"^\s*\[", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TomlTableHeader();

    [GeneratedRegex(@"^\s*(?<key>""[^""]+""|'[^']+'|[\w.-]+)\s*=\s*(?<value>\[(?:""[^""]*""|'[^']*'|[^\]""'])*\]|[^\n]*)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TomlEntry();

    [GeneratedRegex(@"^[ \t]*#[^\n]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TomlComment();

    [GeneratedRegex(@"\{[^}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex TomlInlineTable();

    [GeneratedRegex(@"""(?<value>[^""]*)""|'(?<value>[^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex TomlStrings();

    [GeneratedRegex(@"(?:^|[\s{,])['""]?(?<name>tests?|testing|dev|develop|development)['""]?\s*[:=]", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SetupExtra();

    private const string GoReport = "go-test.txt";
    private const string CargoReport = "cargo-test.txt";
    private const string DartReport = "dart-test.jsonl";
    private const string JestReport = "jest.json";

    /// <summary>
    /// The runner of a Node.js package's tests, from its development dependencies and its <c>test</c> script: Vitest,
    /// Jest or Mocha, or Node's own runner when the script runs <c>node --test</c>.
    /// </summary>
    private static ToolchainTestFramework? NodeFramework(string directory)
    {
        var manifest = Path.Combine(directory, "package.json");
        if (!File.Exists(manifest))
        {
            return null;
        }

        var text = ReadSmall(manifest);
        var script = TestScript(directory) ?? string.Empty;
        bool Uses(string package) => text.Contains("\"" + package + "\":", StringComparison.Ordinal) || Regex.IsMatch(script, @"(?:^|[\s&;|])" + package + @"(?:\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return Uses("vitest") ? ToolchainTestFramework.Vitest
            : Uses("jest") ? ToolchainTestFramework.Jest
            : Uses("mocha") ? ToolchainTestFramework.Mocha
            : script.Contains("node --test", StringComparison.Ordinal) ? ToolchainTestFramework.NodeTest
            : null;
    }

    private static string? TestScript(string directory) => RunPlanner.ReadJson(Path.Combine(directory, "package.json"), "scripts.test");

    /// <summary>The arguments of the <c>mocha</c> command in a test script, without its reporter: its files and options.</summary>
    internal static string MochaArguments(string? script) =>
        ScriptArguments(script, "mocha") is { } arguments ? MochaReporter().Replace(arguments, string.Empty).Trim() : string.Empty;

    /// <summary>The arguments of the <c>node --test</c> command in a test script: its test files.</summary>
    internal static string NodeTestArguments(string? script) => ScriptArguments(script, "node --test") ?? string.Empty;

    /// <summary>What follows a command in a script, up to the next command (<c>&amp;&amp;</c>, <c>;</c> or <c>|</c>).</summary>
    private static string? ScriptArguments(string? script, string command)
    {
        if (script is null || Regex.Match(script, @"(?:^|[\s&;|])" + Regex.Escape(command) + @"(?<arguments>(?:\s[^&;|]*)?)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) is not { Success: true } match)
        {
            return null;
        }

        return match.Groups["arguments"].Value.Trim();
    }

    /// <summary>A name pattern for Vitest's and Jest's <c>-t</c>, Mocha's <c>--grep</c> and Node's <c>--test-name-pattern</c>.</summary>
    private static string NodeNamePattern(IReadOnlyList<ToolchainTestSelector> selection) =>
        string.Join('|', selection.Select(s => QuoteRegex(s.MethodName ?? string.Empty)).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal)) is { Length: > 0 } pattern ? pattern : ".*";

    [GeneratedRegex(@"\s(?:--reporter|-R)(?:=|\s+)\S+", RegexOptions.CultureInvariant)]
    private static partial Regex MochaReporter();

    private static string ReadReport(ToolchainTestPlan plan, string name)
    {
        try
        {
            var path = Path.Combine(plan.ReportDirectory, name);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// <c>go test</c>'s packages and <c>-run</c> for tests: every package (<c>./...</c>), or the packages of the selected
    /// classes, and a pattern matching the selected functions.
    /// </summary>
    internal static IEnumerable<string> GoArguments(string? module, IReadOnlyList<ToolchainTestSelector>? selection)
    {
        if (selection is null)
        {
            yield return "./...";
            yield break;
        }

        var name = module is null ? null : module[(module.LastIndexOf('/') + 1)..];
        foreach (var package in selection.Select(s => s.ClassName).Distinct(StringComparer.Ordinal))
        {
            yield return name is not null && package == name ? "."
                : name is not null && package.StartsWith(name + ".", StringComparison.Ordinal) ? "./" + package[(name.Length + 1)..].Replace('.', '/')
                : package.Replace('.', '/');
        }

        var functions = selection.Where(s => s.MethodName is not null).Select(s => QuoteRegex(s.MethodName!)).Distinct(StringComparer.Ordinal).ToList();
        if (functions.Count > 0 && selection.All(s => s.MethodName is not null))
        {
            yield return "-run";
            yield return "^(?:" + string.Join('|', functions) + ")$";
        }
    }

    /// <summary>A libtest filter for a Rust test: its module path and function, without its target.</summary>
    internal static string CargoFilter(ToolchainTestSelector selector)
    {
        var parts = selector.ClassName.Split('.');
        var modules = parts.Length > 1 ? string.Join("::", parts[1..]) : string.Empty;
        return selector.MethodName is null ? modules + (modules.Length > 0 ? "::" : string.Empty)
            : modules.Length > 0 ? modules + "::" + selector.MethodName : selector.MethodName;
    }

    /// <summary>Whether a folder, outside dependencies and build output, has a file matching a pattern (and a test of its text).</summary>
    private static bool HasFile(string directory, string pattern, Func<string, bool>? matches = null)
    {
        var pending = new Stack<(string Folder, int Depth)>([(directory, 0)]);
        var seen = 0;
        while (pending.TryPop(out var next) && seen < 2000)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(next.Folder, pattern))
                {
                    seen++;
                    if (matches is null || matches(ReadSmall(file)))
                    {
                        return true;
                    }
                }

                if (next.Depth < 6)
                {
                    foreach (var child in Directory.EnumerateDirectories(next.Folder))
                    {
                        var name = Path.GetFileName(child);
                        if (!name.StartsWith('.') && !SkippedFolders.Contains(name) && name is not ("target" or "testdata"))
                        {
                            pending.Push((child, next.Depth + 1));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder removed or unreadable while it is looked at.
            }
        }

        return false;
    }

    /// <summary>PHPUnit's <c>--filter</c> for tests: a pattern matching <c>Class::method</c>, with any data set.</summary>
    internal static string PhpUnitFilter(IReadOnlyList<ToolchainTestSelector> selection)
    {
        var alternatives = selection.Select(s => QuotePcre(s.ClassName.Replace('.', '\\')) + "::" + (s.MethodName is null ? ".*" : QuotePcre(s.MethodName) + "(?: with data set .*)?"));
        return "/^(?:" + string.Join('|', alternatives.Distinct(StringComparer.Ordinal)) + ")$/";
    }

    /// <summary>Surefire's <c>-Dtest</c>: <c>Class#method+method,Class</c>; a method whose name has spaces runs with its class.</summary>
    internal static string MavenFilter(IReadOnlyList<ToolchainTestSelector> selection)
    {
        var parts = new List<string>();
        foreach (var group in selection.GroupBy(s => s.ClassName, StringComparer.Ordinal))
        {
            var methods = group.Select(s => s.MethodName).ToList();
            parts.Add(methods.Exists(m => m is null || m.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '$')))
                ? group.Key
                : group.Key + "#" + string.Join('+', methods.Distinct(StringComparer.Ordinal)));
        }

        return string.Join(',', parts);
    }

    /// <summary>
    /// <c>swift test --filter</c>: a pattern matching XCTest's <c>Module.Class/method</c> and Swift Testing's
    /// <c>Module.Suite/function()</c>.
    /// </summary>
    internal static string SwiftFilter(IReadOnlyList<ToolchainTestSelector> selection)
    {
        var alternatives = selection.Select(s => QuoteRegex(s.ClassName) + (s.MethodName is null ? "(?:[./]|$)" : "[./]" + QuoteRegex(s.MethodName) + @"(?:\(|$)"));
        return string.Join('|', alternatives.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// pytest's node ids for tests: <c>tests/test_math.py::TestDivide::test_divides</c>. pytest reports a test's
    /// class as its file's path with dots, then its class, so the file is the longest such path that exists.
    /// </summary>
    internal static IEnumerable<string> PytestNodeIds(string directory, IReadOnlyList<ToolchainTestSelector> selection)
    {
        foreach (var selector in selection)
        {
            var parts = selector.ClassName.Split('.');
            var id = selector.ClassName.Replace('.', '/') + ".py";
            for (var length = parts.Length; length > 0; length--)
            {
                var file = string.Join('/', parts[..length]) + ".py";
                if (File.Exists(Path.Combine(directory, file)))
                {
                    id = string.Join("::", parts[length..].Prepend(file));
                    break;
                }
            }

            yield return selector.MethodName is null ? id : id + "::" + selector.MethodName;
        }
    }

    private static string QuotePcre(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            if (@"\^$.|?*+()[]{}/#-:=!<>".Contains(c, StringComparison.Ordinal))
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string QuoteRegex(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            if (@"\^$.|?*+()[]{}".Contains(c, StringComparison.Ordinal))
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Whether a Gradle build uses the Kotlin Multiplatform plugin, in its root or one of its modules.</summary>
    internal static bool IsKotlinMultiplatform(string directory)
    {
        static bool Uses(string folder) =>
            ReadSmall(Path.Combine(folder, "build.gradle.kts")) is var build
            && (build.Contains("kotlin(\"multiplatform\")", StringComparison.Ordinal) || build.Contains("kotlin.multiplatform", StringComparison.Ordinal)
                || build.Contains("kotlinMultiplatform", StringComparison.Ordinal));
        try
        {
            return Uses(directory) || Directory.EnumerateDirectories(directory).Where(d => !Path.GetFileName(d).StartsWith('.')).Take(64).Any(Uses);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasJvmTests(string directory)
    {
        // A Kotlin Multiplatform module's tests are in commonTest and jvmTest.
        static bool Has(string folder) => Directory.Exists(Path.Combine(folder, "src", "test"))
            || Directory.Exists(Path.Combine(folder, "src", "commonTest")) || Directory.Exists(Path.Combine(folder, "src", "jvmTest"));
        if (Has(directory))
        {
            return true;
        }

        try
        {
            return Directory.EnumerateDirectories(directory)
                .Where(d => !SkippedFolders.Contains(Path.GetFileName(d)) && Path.GetFileName(d) is not ("build" or "target"))
                .Take(64).Any(Has);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static List<string> FindReports(string directory, Func<string, bool> isReport, DateTime startedUtc)
    {
        var found = new List<string>();
        var pending = new Stack<(string Folder, int Depth)>([(directory, 0)]);
        while (pending.TryPop(out var next) && found.Count < 4096)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(next.Folder, "*.xml"))
                {
                    if (isReport(file) && File.GetLastWriteTimeUtc(file) >= startedUtc.AddSeconds(-2))
                    {
                        found.Add(file);
                    }
                }

                if (next.Depth < 8)
                {
                    foreach (var child in Directory.EnumerateDirectories(next.Folder))
                    {
                        if (!SkippedFolders.Contains(Path.GetFileName(child)) && !new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            pending.Push((child, next.Depth + 1));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder removed or unreadable while the reports are looked for.
            }
        }

        return found;
    }

    private static string ReadSmall(string path)
    {
        try
        {
            return new FileInfo(path).Length <= 1024 * 1024 ? File.ReadAllText(path) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
