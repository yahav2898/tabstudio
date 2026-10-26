using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Projects;

/// <summary>How F5 runs a toolchain project or file: an optional build, then the program.</summary>
/// <param name="Build">A command to run first, such as <c>cargo build</c>, or <see langword="null"/>.</param>
/// <param name="Run">The program.</param>
/// <param name="Preparation">Commands that configure a build before it starts, such as CMake.</param>
/// <param name="EntryPoint">What runs, for the Output panel, such as <c>main.py</c> or <c>npm start</c>.</param>
public sealed record RunPlan(ProcessStartSpec? Build, ProcessStartSpec Run, string EntryPoint, IReadOnlyList<ProcessStartSpec>? Preparation = null);

/// <summary>A project's file that an entry rule names.</summary>
/// <param name="FullPath">The file.</param>
/// <param name="RelativePath">Its path in the project folder, with <c>/</c> between folders, such as <c>src/greeter/__main__.py</c>.</param>
/// <param name="Match">The folder name the rule's <c>*</c> matched, such as <c>greeter</c>, or <see langword="null"/>.</param>
public sealed record EntryFile(string FullPath, string RelativePath, string? Match);

/// <summary>
/// Works out how to run a project or a file from its toolchain's catalog entry: the entry rules in order, the
/// project's environment (Python's <c>.venv</c>), and the toolchain's run environment.
/// </summary>
public static partial class RunPlanner
{
    /// <summary>Plans running a project's entry point, or <see langword="null"/> when none of its rules applies.</summary>
    /// <param name="project">The project.</param>
    public static RunPlan? PlanProject(ToolchainProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (EmbeddedProjectDetector.Read(project.Directory) is { } embedded
            && EmbeddedBuildPlanner.ToolchainId(embedded.Kind) == project.Toolchain.Id)
        {
            return EmbeddedBuildPlanner.RunPlan(embedded);
        }
        if (project.Toolchain.Id == "go")
        {
            return PlanGoProject(project);
        }

        if (project.Toolchain.Id == "cpp")
        {
            return PlanCppProject(project);
        }

        if (project.Toolchain.Id == "java")
        {
            return PlanJavaProject(project);
        }

        if (project.Toolchain.Id == "flutter")
        {
            return FlutterProject.Plan(project.Directory);
        }

        var run = project.Toolchain.Run;
        if (run is null)
        {
            return null;
        }

        foreach (var rule in run.Entries)
        {
            if (FindEntry(project.Directory, rule.File) is not { } entry)
            {
                continue;
            }

            var file = entry.FullPath;
            string? json = null;
            if (rule.Json is { } jsonPath && (json = ReadJson(file, jsonPath)) is null)
            {
                continue;
            }

            var variables = Variables(project, file: entry.RelativePath, json);
            if (entry.Match is { } match)
            {
                variables["match"] = match;
            }

            var entryPoint = rule.Json is not null ? string.Join(' ', rule.Command.Select(a => Expand(a, variables)))
                : project.Toolchain.Id == "rust" ? CargoEntryPoint(entry)
                : entry.RelativePath;
            return new RunPlan(
                rule.Build.Count > 0 ? Spec(project, rule.Build, variables, rule.Environment) : null,
                Spec(project, rule.Command, variables, rule.Environment),
                entryPoint);
        }

        return null;
    }

    /// <summary>Plans running one file of a project.</summary>
    /// <param name="project">The project the file is in.</param>
    /// <param name="file">The file.</param>
    /// <exception cref="ArgumentException">The project's toolchain does not run the file.</exception>
    public static RunPlan PlanFile(ToolchainProject project, string file)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(file);
        if (!project.Toolchain.RunsFile(file) || project.Toolchain.Run is not { } run)
        {
            throw new ArgumentException($"{project.Toolchain.Name} does not run {Path.GetFileName(file)}.", nameof(file));
        }

        if (project.Toolchain.Id == "go")
        {
            return PlanGoFile(project, file);
        }

        if (project.Toolchain.Id == "cpp")
        {
            return PlanCppFile(project, file);
        }

        if (project.Toolchain.Id == "java")
        {
            return PlanJavaFile(project, file);
        }

        var relative = Path.GetRelativePath(project.Directory, file).Replace('\\', '/');
        var variables = Variables(project, relative, json: null);
        return new RunPlan(null, Spec(project, run.File, variables), relative);
    }

    /// <summary>
    /// The first of a project's dependency installs whose manifest is there, such as <c>npm install</c> for a
    /// <c>package.json</c>, or <see langword="null"/>.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="useLockFile">
    /// Whether a lock file beside the manifest decides, as <c>npm ci</c> for <c>package-lock.json</c>: the exact
    /// packages it records. <see langword="false"/> installs from the manifest, updating the lock file.
    /// </param>
    public static DependencyInstallDefinition? DependenciesOf(ToolchainProject project, bool useLockFile = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        var dependency = project.Toolchain.Dependencies.FirstOrDefault(d => File.Exists(Path.Combine(project.Directory, d.Manifest)));
        return useLockFile && dependency?.Locked is { } locked && locked.Files.Any(f => File.Exists(Path.Combine(project.Directory, f)))
            ? dependency with { Name = locked.Name, Description = locked.Description ?? dependency.Description, Command = locked.Command }
            : dependency;
    }

    /// <summary>
    /// A fingerprint of what a dependency install reads: its manifest and the lock files beside it. It changes when
    /// one of them does, so that a project is set up again only then.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="dependency">Its dependency install, from <see cref="DependenciesOf"/>.</param>
    /// <param name="projects">All detected projects, to include npm workspace member manifests.</param>
    /// <param name="includeLocks">Whether to include lock files.</param>
    /// <returns>A hexadecimal SHA-256 hash.</returns>
    public static string FingerprintOf(ToolchainProject project, DependencyInstallDefinition dependency, IEnumerable<ToolchainProject>? projects = null, bool includeLocks = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(dependency);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in DependencyInputs(project, dependency, projects, includeLocks))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(project.Directory, file)));
            hash.AppendData("\n"u8);
            try
            {
                if (File.Exists(file))
                {
                    hash.AppendData(File.ReadAllBytes(file));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file that cannot be read counts as missing; the install reports the problem when it runs.
            }

            hash.AppendData("\n"u8);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>The manifests and lock files read by an install, including npm workspace members.</summary>
    /// <param name="project">The install's root project.</param>
    /// <param name="dependency">The install.</param>
    /// <param name="projects">All detected projects, to include workspace member manifests.</param>
    /// <param name="includeLocks">Whether to include lock files as well as manifests.</param>
    public static IEnumerable<string> DependencyInputs(ToolchainProject project, DependencyInstallDefinition dependency, IEnumerable<ToolchainProject>? projects = null, bool includeLocks = true) =>
        new[] { Path.Combine(project.Directory, dependency.Manifest) }
            .Concat(includeLocks ? (dependency.Locked?.Files ?? []).Select(f => Path.Combine(project.Directory, f)) : [])
            .Concat((projects ?? []).Where(p => NodeWorkspaces.Contains(project, p)).Select(p => Path.Combine(p.Directory, "package.json")))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    /// <summary>Whether the manifest declares packages or npm workspaces that need installing.</summary>
    /// <param name="project">The project.</param>
    /// <param name="dependency">The install.</param>
    public static bool RequiresPackages(ToolchainProject project, DependencyInstallDefinition dependency) =>
        ListsPackages(Path.Combine(project.Directory, dependency.Manifest), dependency);

    /// <summary>
    /// Finds an entry rule's file in a project folder. A <c>*</c> folder in the rule, as in <c>src/*/__main__.py</c>,
    /// matches the first folder there, in name order, that has the rest of the path.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    /// <param name="pattern">The rule's file (<see cref="EntryRule.File"/>), with <c>/</c> between folders.</param>
    /// <returns>The file, or <see langword="null"/> when the project has none.</returns>
    public static EntryFile? FindEntry(string directory, string pattern)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(pattern);
        var parts = pattern.Split('/');
        var star = Array.IndexOf(parts, "*");
        if (star < 0)
        {
            var file = Path.Combine(directory, Path.Combine(parts));
            return File.Exists(file) ? new EntryFile(file, pattern, null) : null;
        }

        var parent = Path.Combine([directory, .. parts[..star]]);
        List<string> folders;
        try
        {
            folders = Directory.Exists(parent) ? [.. Directory.EnumerateDirectories(parent).Order(StringComparer.Ordinal)] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        foreach (var folder in folders)
        {
            var file = Path.Combine([folder, .. parts[(star + 1)..]]);
            if (File.Exists(file))
            {
                var name = Path.GetFileName(folder);
                return new EntryFile(file, string.Join('/', parts.Select((part, index) => index == star ? name : part)), name);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a dependency install should run: the manifest lists packages, and what the install creates is
    /// missing.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="dependency">The install.</param>
    public static bool IsPending(ToolchainProject project, DependencyInstallDefinition dependency)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(dependency);
        return dependency.Installed is { } installed
            && (!Path.Exists(Path.Combine(project.Directory, installed)) || NeedsEnvironment(project, dependency))
            && RequiresPackages(project, dependency);
    }

    /// <summary>Whether a manifest lists any package: a JSON one in its <see cref="DependencyInstallDefinition.Declares"/>, a text one on any line.</summary>
    private static bool ListsPackages(string manifest, DependencyInstallDefinition dependency)
    {
        try
        {
            if (dependency.Declares.Count > 0)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifest), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("workspaces", out var workspaces)
                    && workspaces.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                {
                    return true;
                }

                return dependency.Declares.Any(name =>
                    document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(name, out var packages)
                    && packages.ValueKind == JsonValueKind.Object
                    && packages.EnumerateObject().Any());
            }

            return !manifest.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                || File.ReadLines(manifest).Any(line => line.Trim() is { Length: > 0 } text && !text.StartsWith('#'));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A manifest that cannot be read is offered: the install's own output says what is wrong with it.
            return true;
        }
    }

    /// <summary>
    /// The commands that install a project's dependencies: creating its environment first when the install uses
    /// it and it does not exist yet (<c>python3 -m venv .venv</c>), then the install itself.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="dependency">The install.</param>
    public static IReadOnlyList<ProcessStartSpec> PlanDependencies(ToolchainProject project, DependencyInstallDefinition dependency)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(dependency);
        var steps = new List<ProcessStartSpec>();
        var variables = Variables(project, file: null, json: null);
        if (project.Toolchain.ProjectEnvironment is { } environment && NeedsEnvironment(project, dependency))
        {
            steps.Add(Spec(project, environment.Create, variables));
        }

        steps.Add(Spec(project, dependency.Command, variables));
        return steps;
    }

    private static bool NeedsEnvironment(ToolchainProject project, DependencyInstallDefinition dependency) =>
        project.Toolchain.ProjectEnvironment is { } environment && dependency.Command.Count > 0
        && dependency.Command[0].StartsWith(environment.Directory + "/", StringComparison.Ordinal)
        && !File.Exists(Path.Combine(project.Directory, dependency.Command[0]));

    /// <summary>The placeholders' values: the file, the JSON value, and the project environment's variables.</summary>
    private static Dictionary<string, string> Variables(ToolchainProject project, string? file, string? json)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (file is not null)
        {
            variables["file"] = file;
        }

        if (json is not null)
        {
            variables["json"] = json;
        }

        if (project.Toolchain.ProjectEnvironment is { } environment)
        {
            // A variable takes its value from the environment while the file it names is there (.venv/bin/python),
            // and its default otherwise (python3).
            foreach (var (name, value) in environment.Defaults)
            {
                var own = environment.Variables.GetValueOrDefault(name);
                variables[name] = own is not null && File.Exists(Path.Combine(project.Directory, own)) ? own : value;
            }
        }

        return variables;
    }

    private static ProcessStartSpec Spec(ToolchainProject project, IReadOnlyList<string> command, Dictionary<string, string> variables, IReadOnlyDictionary<string, string>? entryEnvironment = null)
    {
        var arguments = command.Select(a => Expand(a, variables)).ToList();
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in project.Toolchain.Run?.Environment ?? new Dictionary<string, string>())
        {
            environment[name] = value;
        }

        foreach (var (name, value) in entryEnvironment ?? new Dictionary<string, string>())
        {
            environment[name] = Expand(value, variables);
        }

        if (project.Toolchain.ProjectEnvironment is { Path: { } bin } projectEnvironment
            && Directory.Exists(Path.Combine(project.Directory, projectEnvironment.Directory)))
        {
            environment["PATH"] = $"{Path.Combine(project.Directory, bin)}:$PATH";
            environment["VIRTUAL_ENV"] = Path.Combine(project.Directory, projectEnvironment.Directory);
        }

        // A program in the project, such as .venv/bin/python, is found from the project folder.
        var program = arguments[0];
        if (program.Contains('/', StringComparison.Ordinal) && !Path.IsPathRooted(program))
        {
            program = Path.Combine(project.Directory, program);
        }

        return new ProcessStartSpec
        {
            Program = program,
            Arguments = arguments[1..],
            WorkingDirectory = project.Directory,
            Environment = environment,
        };
    }

    private static string Expand(string argument, Dictionary<string, string> variables)
    {
        foreach (var (name, value) in variables)
        {
            argument = argument.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return argument;
    }

    /// <summary>Reads a string at a dotted path in a JSON file, such as <c>scripts.start</c>; <see langword="null"/> when there is none.</summary>
    /// <param name="file">The JSON file, such as <c>package.json</c>.</param>
    /// <param name="path">The path, with dots between property names.</param>
    public static string? ReadJson(string file, string path)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var element = document.RootElement;
            foreach (var part in path.Split('.'))
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(part, out element))
                {
                    return null;
                }
            }

            return element.ValueKind == JsonValueKind.String && element.GetString() is { Length: > 0 } value ? value : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
