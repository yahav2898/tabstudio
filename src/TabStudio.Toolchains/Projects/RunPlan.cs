using System.Text.Json;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Projects;

/// <summary>How F5 runs a toolchain project or file: an optional build, then the program.</summary>
/// <param name="Build">A command to run first, such as <c>cargo build</c>, or <see langword="null"/>.</param>
/// <param name="Run">The program.</param>
/// <param name="EntryPoint">What runs, for the Output panel, such as <c>main.py</c> or <c>npm start</c>.</param>
public sealed record RunPlan(ProcessStartSpec? Build, ProcessStartSpec Run, string EntryPoint);

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
        if (project.Toolchain.Id == "java")
        {
            return PlanJavaProject(project);
        }

        var run = project.Toolchain.Run;
        if (run is null)
        {
            return null;
        }

        foreach (var rule in run.Entries)
        {
            var file = Path.Combine(project.Directory, rule.File);
            if (!File.Exists(file))
            {
                continue;
            }

            string? json = null;
            if (rule.Json is { } jsonPath && (json = ReadJson(file, jsonPath)) is null)
            {
                continue;
            }

            var variables = Variables(project, file: rule.File, json);
            var entryPoint = rule.Json is null ? rule.File : string.Join(' ', rule.Command.Select(a => Expand(a, variables)));
            return new RunPlan(
                rule.Build.Count > 0 ? Spec(project, rule.Build, variables) : null,
                Spec(project, rule.Command, variables),
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
    public static DependencyInstallDefinition? DependenciesOf(ToolchainProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Toolchain.Dependencies.FirstOrDefault(d => File.Exists(Path.Combine(project.Directory, d.Manifest)));
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
            && !Path.Exists(Path.Combine(project.Directory, installed))
            && ListsPackages(Path.Combine(project.Directory, dependency.Manifest), dependency);
    }

    /// <summary>Whether a manifest lists any package: a JSON one in its <see cref="DependencyInstallDefinition.Declares"/>, a text one on any line.</summary>
    private static bool ListsPackages(string manifest, DependencyInstallDefinition dependency)
    {
        try
        {
            if (dependency.Declares.Count > 0)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifest), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
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
        if (project.Toolchain.ProjectEnvironment is { } environment
            && !Directory.Exists(Path.Combine(project.Directory, environment.Directory))
            && dependency.Command.Count > 0 && dependency.Command[0].StartsWith(environment.Directory + "/", StringComparison.Ordinal))
        {
            steps.Add(Spec(project, environment.Create, variables));
        }

        steps.Add(Spec(project, dependency.Command, variables));
        return steps;
    }

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

    private static ProcessStartSpec Spec(ToolchainProject project, IReadOnlyList<string> command, Dictionary<string, string> variables)
    {
        var arguments = command.Select(a => Expand(a, variables)).ToList();
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in project.Toolchain.Run?.Environment ?? new Dictionary<string, string>())
        {
            environment[name] = value;
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
