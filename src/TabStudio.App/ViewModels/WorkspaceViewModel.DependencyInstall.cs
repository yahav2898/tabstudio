using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Python;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Installing a project's packages, for the project setup and Tools &gt; Install Project Packages. Python gets the
/// version the project asks for, and within it the newest its pinned packages install on from wheels (uv's builds
/// of CPython when that is not the toolchain's own Python); a package pip must compile gets the compilers and the
/// libraries it builds against, and pip runs again; and a failure becomes one message that says which package,
/// why, and what TabStudio did or what to do, with pip's full log one tap away in Output &gt; Project Setup.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The last failed package install of each project folder, which the notice over the editor offers.</summary>
    private readonly Dictionary<string, string> _installFailures = new(StringComparer.Ordinal);

    /// <summary>The title of each project folder's install failure notice, such as "psycopg2 could not be installed".</summary>
    private readonly Dictionary<string, string> _installFailureTitles = new(StringComparer.Ordinal);

    /// <summary>Whether the last pip install ran again after installing build tools.</summary>
    private bool _pipRetried;

    /// <summary>The Alpine packages the last retry installed.</summary>
    private IReadOnlyList<string> _lastBuildPackages = [];

    /// <summary>
    /// Runs a project's dependency install: creating its environment when needed, then the install. Its outcome is
    /// in the Output panel; pip's own output is in the Project Setup log.
    /// </summary>
    /// <returns>The failed step's result, or <see langword="null"/> when the install succeeded.</returns>
    private async Task<OutputProcessResult?> RunDependencyInstallAsync(ToolchainProject project, DependencyInstallDefinition dependency, CancellationToken cancellationToken)
    {
        var python = IsPipInstall(project, dependency);
        var steps = RunPlanner.PlanDependencies(project, dependency).ToList();
        string? version = null;
        if (python)
        {
            ProjectSetupOutput.WriteLine($"------ {project.Name}: {dependency.Name} ------", OutputKind.Info);
            (steps, version) = await PlanPythonEnvironmentAsync(project, dependency, steps, cancellationToken);
        }

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            if (BlockLocalToolchainActionForRemote())
            {
                return new OutputProcessResult(null, [], "The workspace is remote.");
            }

            Output.WriteLine("> " + step.CommandLine, OutputKind.Info);
            if (!python)
            {
                var result = await OutputProcess.RunAsync(_toolchains.Runtime, step, Output, null, cancellationToken, toolchain: project.Toolchain.Id);
                if (!result.Succeeded)
                {
                    return result;
                }

                continue;
            }

            ProjectSetupOutput.WriteLine("> " + step.CommandLine, OutputKind.Info);
            var outcome = await OutputProcess.RunAsync(_toolchains.Runtime, step, ProjectSetupOutput, null, cancellationToken, toolchain: "python");
            var creates = index < steps.Count - 1;
            if (!outcome.Succeeded && creates && step.Program == "uv" && !cancellationToken.IsCancellationRequested)
            {
                // uv could not install the Python the project needs here; the toolchain's own Python still works.
                var fallback = UvFallback(project, step);
                Output.WriteLine($"uv could not create the environment with Python {version} ({LastLine(outcome)}), so it uses the toolchain's Python instead.", OutputKind.Info);
                Output.WriteLine("> " + fallback.CommandLine, OutputKind.Info);
                ProjectSetupOutput.WriteLine("> " + fallback.CommandLine, OutputKind.Info);
                version = null;
                outcome = await OutputProcess.RunAsync(_toolchains.Runtime, fallback, ProjectSetupOutput, null, cancellationToken, toolchain: "python");
            }

            if (!outcome.Succeeded && !creates && !cancellationToken.IsCancellationRequested)
            {
                outcome = await RetryPipWithBuildToolsAsync(project, step, outcome, cancellationToken);
            }

            if (!outcome.Succeeded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!creates)
                {
                    ReportPipFailure(project, outcome, retried: _pipRetried);
                }
                else
                {
                    Output.WriteLine($"{project.Name}'s environment could not be created: {LastLine(outcome)}. The full log is in Output > Project Setup.", OutputKind.Failure);
                }

                return outcome;
            }

            if (!creates && outcome.Lines.LastOrDefault(l => l.StartsWith("Successfully installed ", StringComparison.Ordinal)) is { } installed)
            {
                var count = installed["Successfully installed ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                Output.WriteLine(string.Create(CultureInfo.CurrentCulture, $"Installed {count} package{(count == 1 ? string.Empty : "s")} into .venv{(VenvVersion(project.Directory) is { } python3 ? " with Python " + python3 : string.Empty)}. The full log is in Output > Project Setup."), OutputKind.Program);
            }
        }

        _installFailures.Remove(project.Directory);
        return null;
    }

    /// <summary>Whether a dependency install is pip's, into the project's <c>.venv</c>.</summary>
    private static bool IsPipInstall(ToolchainProject project, DependencyInstallDefinition dependency) =>
        project.Toolchain.Id == "python" && dependency.Command.Count > 2 && dependency.Command[1] == "-m" && dependency.Command[2] == "pip";

    /// <summary>
    /// Plans the Python of the project's <c>.venv</c>: the version the project asks for, and within it the newest
    /// its pinned packages install on from wheels. A version other than the toolchain's own is created with uv,
    /// which installs that CPython; an environment left with another version by a failed install is recreated.
    /// </summary>
    private async Task<(List<ProcessStartSpec> Steps, string? Version)> PlanPythonEnvironmentAsync(ToolchainProject project, DependencyInstallDefinition dependency, List<ProcessStartSpec> steps, CancellationToken cancellationToken)
    {
        var existing = VenvVersion(project.Directory);
        var installedBefore = SetupSettings.Projects?.ContainsKey(Path.Combine(project.Directory, dependency.Manifest)) == true;
        if (existing is not null && installedBefore)
        {
            // An environment that installed once is the project's; it is never replaced.
            return (steps, existing);
        }

        var choice = await ChoosePythonAsync(project, dependency, cancellationToken);
        if (choice is null)
        {
            return (steps, existing);
        }

        var creates = steps.Count > 1;
        var recreate = existing is not null && existing != choice.Version;
        if (!creates && !recreate)
        {
            return (steps, existing);
        }

        Output.WriteLine($"Python {choice.Version} for {project.Name}: {choice.Reason}.", OutputKind.Info);
        if (recreate)
        {
            Output.WriteLine($"The project's .venv has Python {existing} from an install that did not finish; it is created again with Python {choice.Version}.", OutputKind.Info);
        }

        var template = creates ? steps[0] : steps[^1];
        ProcessStartSpec create;
        if (choice.UsesSystemPython)
        {
            create = template with { Program = "python3", Arguments = ["-m", "venv", .. recreate ? ["--clear"] : Array.Empty<string>(), ".venv"] };
        }
        else if (await EnsureUvAsync(cancellationToken))
        {
            create = template with
            {
                Program = "uv",
                Arguments = ["venv", "--seed", "--python", choice.Version, .. recreate ? ["--clear"] : Array.Empty<string>(), ".venv"],
                WorkingDirectory = project.Directory,
                Environment = new Dictionary<string, string?>(template.Environment, StringComparer.Ordinal) { ["UV_PYTHON_DOWNLOADS"] = "automatic" },
            };
        }
        else
        {
            Output.WriteLine($"uv is not installed here, so {project.Name} uses the toolchain's Python instead of {choice.Version}. Install uv to use the version the project needs.", OutputKind.Info);
            if (!creates)
            {
                return (steps, existing);
            }

            return (steps, null);
        }

        var result = new List<ProcessStartSpec>(steps);
        if (creates)
        {
            result[0] = create;
        }
        else
        {
            result.Insert(0, create);
        }

        return (result, choice.Version);
    }

    /// <summary>Chooses the project's Python from what it asks for and what its pins install on.</summary>
    private async Task<PythonVersionChoice?> ChoosePythonAsync(ToolchainProject project, DependencyInstallDefinition dependency, CancellationToken cancellationToken)
    {
        var request = PythonVersionRequest.Find(project.Directory);
        var system = SystemPythonMinor();
        (int Minor, IReadOnlyList<PythonRelease> Limiting)? pins = null;
        var platform = _toolchains.Runtime.Kind == RuntimeKind.Linux ? PythonPlatform.Alpine : OperatingSystem.IsLinux() ? PythonPlatform.Glibc : null;
        var manifest = Path.Combine(project.Directory, dependency.Manifest);
        if (platform is not null && dependency.Manifest.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && request?.Range.ExactMinor is null)
        {
            var pinned = PythonRequirements.Pins(manifest);
            if (pinned.Count > 0)
            {
                ProjectSetupOutput.WriteLine($"Reading which Python versions the {pinned.Count} pinned packages have wheels for, from PyPI.", OutputKind.Info);
                var releases = await PythonWheels.ReadAsync(_toolchains.Http, pinned, cancellationToken);
                var candidates = PythonVersionChoice.ManagedMinors.Concat(system is { } own ? [own] : []).Distinct();
                pins = PythonWheels.NewestSupported(releases, platform, candidates);
            }
        }

        return PythonVersionChoice.Choose(request, pins, system);
    }

    /// <summary>The toolchain's own Python's minor version, such as 14 for Python 3.14.8.</summary>
    private int? SystemPythonMinor() =>
        _toolchains.Manager.Status("python").Version is { } version && MinorVersion().Match(version) is { Success: true } match
            ? int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture)
            : null;

    /// <summary>The Python version of a project's <c>.venv</c>, such as <c>3.11</c>, from its <c>pyvenv.cfg</c>.</summary>
    private static string? VenvVersion(string projectDirectory)
    {
        var config = Path.Combine(projectDirectory, ".venv", "pyvenv.cfg");
        try
        {
            if (!File.Exists(config))
            {
                return null;
            }

            foreach (var line in File.ReadLines(config))
            {
                var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
                if (pair.Length == 2 && pair[0] is "version" or "version_info" && MinorVersion().Match(pair[1]) is { Success: true } match)
                {
                    return "3." + match.Groups["minor"].Value;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable environment is created again by the install that needs it.
        }

        return null;
    }

    /// <summary>Makes uv available: on the tablet, Alpine's package; on a computer, the one on the PATH.</summary>
    private async Task<bool> EnsureUvAsync(CancellationToken cancellationToken)
    {
        if (_toolchains.Runtime.Resolve("uv") is not null)
        {
            return true;
        }

        if (!_toolchains.CanInstallAlpinePackages)
        {
            return false;
        }

        Output.WriteLine("Installing uv, which installs the Python the project needs.", OutputKind.Info);
        try
        {
            await _toolchains.InstallAlpinePackagesAsync(["uv"], line => ProjectSetupOutput.WriteLine(line, OutputKind.Program), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or ToolchainInstallException)
        {
            Output.WriteLine($"uv could not be installed: {ex.Message}", OutputKind.Info);
            return false;
        }

        return _toolchains.Runtime.Resolve("uv") is not null;
    }

    /// <summary>The toolchain's own <c>python3 -m venv</c>, when uv could not create the environment.</summary>
    private static ProcessStartSpec UvFallback(ToolchainProject project, ProcessStartSpec uv) =>
        uv with { Program = "python3", Arguments = ["-m", "venv", "--clear", ".venv"], WorkingDirectory = project.Directory };

    /// <summary>
    /// When pip had to compile a package and its build failed for want of compilers or a library's headers,
    /// installs them, the build tools and the packages <c>python-build-packages.json</c> lists for it, and runs pip
    /// again, once.
    /// </summary>
    private async Task<OutputProcessResult> RetryPipWithBuildToolsAsync(ToolchainProject project, ProcessStartSpec pip, OutputProcessResult failed, CancellationToken cancellationToken)
    {
        _pipRetried = false;
        var failure = PipFailure.Analyze(failed.Lines);
        if (!failure.NeedsBuildTools || !_toolchains.CanInstallAlpinePackages)
        {
            return failed;
        }

        var table = PythonBuildPackages.Default;
        var packages = table.PackagesFor(failure.Package is { } name ? [name] : [], failure.MissingHeaders);
        Output.WriteLine($"{failure.Summary(VenvVersion(project.Directory) ?? "3", "on this tablet")} TabStudio is installing {string.Join(", ", packages)} and trying again.", OutputKind.Info);
        ProjectSetupStatus($"Setting up {project.Name}: installing {string.Join(", ", packages)}");
        try
        {
            await _toolchains.InstallAlpinePackagesAsync(packages, line => ProjectSetupOutput.WriteLine(line, OutputKind.Program), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or ToolchainInstallException)
        {
            ProjectSetupOutput.WriteLine(ex.Message, OutputKind.Failure);
            Output.WriteLine($"The build tools could not be installed: {ex.Message}", OutputKind.Failure);
            return failed;
        }

        _pipRetried = true;
        _lastBuildPackages = packages;
        Output.WriteLine("> " + pip.CommandLine, OutputKind.Info);
        ProjectSetupOutput.WriteLine("> " + pip.CommandLine, OutputKind.Info);
        var retried = await OutputProcess.RunAsync(_toolchains.Runtime, pip, ProjectSetupOutput, null, cancellationToken, toolchain: "python");
        if (retried.Succeeded)
        {
            Output.WriteLine($"{failure.PackageText} was compiled with {string.Join(", ", packages)}.", OutputKind.Program);
        }

        return retried;
    }

    /// <summary>Shows what the setup does now in the status bar, while it is running.</summary>
    private void ProjectSetupStatus(string text)
    {
        if (ProjectSetupText is not null)
        {
            ProjectSetupText = text;
        }
    }

    /// <summary>
    /// The one message a failed pip install leaves in the Output panel and over the editor: which package, why,
    /// what TabStudio did, and what to do next. pip's own report stays in Output &gt; Project Setup.
    /// </summary>
    private void ReportPipFailure(ToolchainProject project, OutputProcessResult result, bool retried)
    {
        var failure = PipFailure.Analyze(result.Lines);
        var python = VenvVersion(project.Directory) ?? (SystemPythonMinor() is { } minor ? "3." + minor.ToString(CultureInfo.InvariantCulture) : "3");
        var where = _toolchains.Runtime.Kind == RuntimeKind.Linux ? "on this tablet" : "on this computer";
        var did = retried
            ? $" TabStudio installed {string.Join(", ", _lastBuildPackages)} and tried again, and the build still failed."
            : failure.NeedsBuildTools && !_toolchains.CanInstallAlpinePackages
                ? $" Install a C compiler and Python's headers{(failure.Package is { } pkg && PythonBuildPackages.Default.For(pkg) is { Needs: { } needs } ? ", and " + needs : string.Empty)}, then use Tools > Install Project Packages."
                : string.Empty;
        var next = failure.Kind switch
        {
            PipFailureKind.Build => failure.Package is { } name && PythonBuildPackages.Default.For(name)?.Note is { } note
                ? $" {note}"
                : $" A .python-version file naming the Python the project was written for, such as 3.12, lets pip use a prebuilt wheel{(failure.Package is null ? "." : $", as does a {failure.Package} release that ships one.")}",
            PipFailureKind.NoMatchingVersion => " Pin a version that supports this Python, or add a .python-version file naming the Python the project was written for.",
            PipFailureKind.Network or PipFailureKind.DiskFull => " Then use Tools > Install Project Packages.",
            _ => string.Empty,
        };
        var message = failure.Summary(python, where) + did + next;
        Output.WriteLine(message, OutputKind.Failure);
        Output.WriteLine("The full log is in Output > Project Setup.", OutputKind.Info);
        ProjectSetupOutput.WriteLine(message, OutputKind.Failure);
        _installFailures[project.Directory] = message;
        _installFailureTitles[project.Directory] = failure.Package is null ? $"{project.Name}'s packages were not installed" : $"{failure.Package} could not be installed";
    }

    /// <summary>The offer over the editor after a failed install: its message, and the full log one tap away.</summary>
    private ToolchainNoticeViewModel? InstallFailureNotice(ToolchainProject project) =>
        _installFailures.TryGetValue(project.Directory, out var message)
            ? Notice($"install-failed:{project.Directory}:{message.GetHashCode(StringComparison.Ordinal)}", _installFailureTitles.GetValueOrDefault(project.Directory) ?? "Packages were not installed", message, "Show Full Log", () =>
            {
                ShowProjectSetupLog();
                return Task.CompletedTask;
            })
            : null;

    private static string LastLine(OutputProcessResult result) =>
        result.StartError ?? result.Lines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? result.Exit?.Description ?? "it did not start";

    [GeneratedRegex(@"^3\.(?<minor>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex MinorVersion();
}
