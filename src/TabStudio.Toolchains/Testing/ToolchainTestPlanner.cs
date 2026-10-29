using System.Text;
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
/// <c>test</c> tasks, Maven's Surefire, and <c>swift test --xunit-output</c>.
/// </summary>
public static class ToolchainTestPlanner
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

    private static readonly string[] SkippedFolders = [".git", ".gradle", ".idea", ".tabstudio", "node_modules", "vendor", ".build"];

    /// <summary>
    /// The runner of a project's tests, or <see langword="null"/> when it has none TabStudio runs: PHPUnit or Pest
    /// in a Composer project that requires it, JUnit tests in a Gradle or Maven build with <c>src/test</c>, and a
    /// Swift package with test targets.
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
                command = ["gradle", "--no-daemon", "--console=plain", "--continue", "--init-script", init, "test"];
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

    private static bool HasJvmTests(string directory)
    {
        static bool Has(string folder) => Directory.Exists(Path.Combine(folder, "src", "test"));
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
