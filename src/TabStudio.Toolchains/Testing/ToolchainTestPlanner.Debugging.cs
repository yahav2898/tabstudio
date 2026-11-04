using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Testing;

/// <summary>How a toolchain project's test run is debugged.</summary>
/// <param name="Results">The run whose reports hold the results, read as <see cref="ToolchainTestPlanner.ReadResults"/> reads them.</param>
/// <param name="Prepare">What runs first without the debugger, such as installing pytest or building Go's test binary; or <see langword="null"/>.</param>
/// <param name="Debuggee">What the toolchain's debug adapter launches, as a run plan's program.</param>
/// <param name="OutputReport">
/// Where the debugged run's output is saved for its results, for a runner whose results are its output, such as
/// <c>go test -v</c>; <see langword="null"/> when the runner writes a report itself.
/// </param>
/// <param name="OutputFooter">A line that ends the saved output, such as the package line <c>go test</c> prints and a test binary does not.</param>
public sealed record ToolchainTestDebugPlan(ToolchainTestPlan Results, ProcessStartSpec? Prepare, ProcessStartSpec Debuggee, string? OutputReport = null, string? OutputFooter = null)
{
    /// <summary>A line that starts the saved output, such as the <c>Running</c> line <c>cargo test</c> prints before a test binary's output.</summary>
    public string? OutputHeader { get; init; }

    /// <summary>
    /// What the debug adapter launches once <see cref="Prepare"/> ran, from its output: the test binary <c>cargo test
    /// --no-run</c> built. <see langword="null"/> when <see cref="Debuggee"/> is known before; the function returns
    /// <see langword="null"/> when the output names no test binary.
    /// </summary>
    public Func<IReadOnlyList<string>, ToolchainTestDebugPlan?>? AfterPrepare { get; init; }
}

public static partial class ToolchainTestPlanner
{
    /// <summary>Installs pytest when the interpreter (<c>$0</c>) lacks it, and fails when it cannot.</summary>
    internal const string PytestInstall =
        "if ! \"$0\" -c 'import pytest' 2> /dev/null; then echo 'Installing pytest, which the tests need'; "
        + "\"$0\" -m pip install --disable-pip-version-check -q pytest || exit 1; fi";

    /// <summary>
    /// How to debug a test run, or <see langword="null"/> when TabStudio cannot debug the runner's tests yet: pytest
    /// under debugpy (what the tests need installed first, as for a run), Vitest, Jest, Mocha and <c>node --test</c>
    /// under js-debug, which debugs every Node.js process the runner starts, PHPUnit and Pest under Xdebug, one Go
    /// package's test binary, built without optimizations, under Delve, and the test binary of one Rust target under
    /// LLDB. Each writes the same results as a run.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="framework">Its test runner.</param>
    /// <param name="selection">The classes and methods to debug, or <see langword="null"/> for every test.</param>
    /// <param name="reportDirectory">The folder of the run's reports, as for <see cref="Plan"/>.</param>
    public static ToolchainTestDebugPlan? PlanDebug(ToolchainProject project, ToolchainTestFramework framework, IReadOnlyList<ToolchainTestSelector>? selection, string reportDirectory)
    {
        ArgumentNullException.ThrowIfNull(project);
        switch (framework)
        {
            case ToolchainTestFramework.Pytest:
            {
                // sh -c <install, then exec "$0" -m pytest "$@"> <python> <pytest's arguments>
                var plan = Plan(project, framework, selection, reportDirectory);
                var arguments = plan.Command.Arguments;
                if (arguments.Count < 3)
                {
                    return null;
                }

                var python = arguments[2];
                if (python.Contains('/', StringComparison.Ordinal) && !Path.IsPathRooted(python))
                {
                    python = Path.Combine(project.Directory, python);
                }

                var prepare = plan.Command with { Arguments = ["-c", PythonTestInstall(PythonTestRequirements(project.Directory)) + PytestInstall, python] };
                return new(plan, prepare, plan.Command with { Program = python, Arguments = ["-m", "pytest", .. arguments.Skip(3)] });
            }

            case ToolchainTestFramework.Jest:
            {
                // Jest's workers are processes of their own; in one process a breakpoint is hit where it was set.
                var plan = Plan(project, framework, selection, reportDirectory);
                return new(plan, null, plan.Command with { Arguments = [.. plan.Command.Arguments, "--runInBand"] });
            }

            case ToolchainTestFramework.Vitest or ToolchainTestFramework.Mocha or ToolchainTestFramework.NodeTest
                or ToolchainTestFramework.PhpUnit or ToolchainTestFramework.Pest:
            {
                var plan = Plan(project, framework, selection, reportDirectory);
                return new(plan, null, plan.Command);
            }

            case ToolchainTestFramework.GoTest:
                return PlanGoDebug(project, selection, reportDirectory);

            case ToolchainTestFramework.Cargo:
                return PlanCargoDebug(project, selection, reportDirectory);

            default:
                return null;
        }
    }

    /// <summary>
    /// Debugs the tests of one Rust target: <c>cargo test --no-run</c> builds the test binaries and names them, and LLDB
    /// runs the one of the selected tests' target (the first unit tests without a selection) with their filters, in
    /// the package's folder, as <c>cargo test</c> does. Its output, under the <c>Running</c> line, is read as a run's.
    /// </summary>
    private static ToolchainTestDebugPlan PlanCargoDebug(ToolchainProject project, IReadOnlyList<ToolchainTestSelector>? selection, string reportDirectory)
    {
        var results = Plan(project, ToolchainTestFramework.Cargo, selection, reportDirectory);
        List<string> build = ["cargo", "test", "--no-run"];
        if (ReadSmall(Path.Combine(project.Directory, "Cargo.toml")).Contains("[workspace]", StringComparison.Ordinal))
        {
            build.Add("--workspace");
        }

        var prepare = RunPlanner.CommandSpec(project, build);
        var selected = selection is { Count: > 0 } ? selection : null;
        var plan = new ToolchainTestDebugPlan(results, prepare, prepare, Path.Combine(reportDirectory, CargoReport));
        return plan with
        {
            AfterPrepare = lines => CargoDebuggee(project, lines, selected) is { } debuggee
                ? plan with { Debuggee = debuggee, OutputHeader = "     Running " + debuggee.Program, AfterPrepare = null }
                : null,
        };
    }

    /// <summary>The test binary to debug, from the <c>Executable</c> lines of <c>cargo test --no-run</c>, with the selection's filters.</summary>
    internal static ProcessStartSpec? CargoDebuggee(ToolchainProject project, IReadOnlyList<string> lines, IReadOnlyList<ToolchainTestSelector>? selection)
    {
        var binaries = lines.Select(line => CargoExecutable().Match(line)).Where(m => m.Success)
            .Select(m => (Kind: m.Groups["kind"].Value, Path: m.Groups["path"].Value)).ToList();
        if (binaries.Count == 0)
        {
            return null;
        }

        var target = selection?.Select(s => s.ClassName.Split('.')[0]).FirstOrDefault();
        var chosen = binaries.FirstOrDefault(b => target is not null && CargoBinaryHash().Replace(Path.GetFileName(b.Path), string.Empty) == target);
        if (chosen.Path is null)
        {
            chosen = binaries.FirstOrDefault(b => b.Kind == "unittests");
        }

        if (chosen.Path is null)
        {
            chosen = binaries[0];
        }

        var path = Path.IsPathRooted(chosen.Path) ? chosen.Path : Path.GetFullPath(Path.Combine(project.Directory, chosen.Path));
        List<string> command = [path];
        if (selection is not null)
        {
            command.AddRange(selection.Select(CargoFilter).Distinct(StringComparer.Ordinal));
        }

        return RunPlanner.CommandSpec(project, command) with { WorkingDirectory = project.Directory };
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*Executable\s+(?:(?<kind>unittests|tests|benches)\s+)?\S+\s+\((?<path>[^)]+)\)\s*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex CargoExecutable();

    [System.Text.RegularExpressions.GeneratedRegex(@"-[0-9a-f]{16}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex CargoBinaryHash();

    /// <summary>
    /// Debugs the tests of one Go package: <c>go test -c</c> builds its test binary without optimizations, and Delve runs
    /// it with <c>-test.v</c> in the package's folder, as <c>go test</c> does. Without a selection, the module's root
    /// package is debugged.
    /// </summary>
    private static ToolchainTestDebugPlan? PlanGoDebug(ToolchainProject project, IReadOnlyList<ToolchainTestSelector>? selection, string reportDirectory)
    {
        var module = ToolchainTestOutput.GoModule(project.Directory);
        var selected = selection is { Count: > 0 } ? selection : null;
        var arguments = GoArguments(module, selected).ToList();
        var run = arguments.IndexOf("-run");
        var packages = run >= 0 ? arguments.Take(run).ToList() : arguments;
        var package = packages.FirstOrDefault() is { } first && first != "./..." ? first : ".";
        if (package == "." && !Directory.EnumerateFiles(project.Directory, "*_test.go").Any())
        {
            return null;
        }

        Directory.CreateDirectory(reportDirectory);
        var binary = Path.Combine(reportDirectory, "tests.test");
        var folder = package == "." ? project.Directory
            : package.StartsWith("./", StringComparison.Ordinal) ? Path.Combine(project.Directory, package[2..])
            : project.Directory;
        List<string> test = [binary, "-test.v"];
        if (run >= 0 && run + 1 < arguments.Count)
        {
            test.AddRange(["-test.run", arguments[run + 1]]);
        }

        var importPath = module is null ? package
            : package == "." ? module
            : package.StartsWith("./", StringComparison.Ordinal) ? module + "/" + package[2..]
            : package;
        var results = new ToolchainTestPlan(ToolchainTestFramework.GoTest, RunPlanner.CommandSpec(project, ["go", "test", "-v", package]), reportDirectory);
        return new(
            results,
            RunPlanner.CommandSpec(project, ["go", "test", "-c", "-gcflags=all=-N -l", "-o", binary, package]),
            RunPlanner.CommandSpec(project, test) with { WorkingDirectory = folder },
            Path.Combine(reportDirectory, GoReport),
            "ok  \t" + importPath + "\t0.000s");
    }
}
