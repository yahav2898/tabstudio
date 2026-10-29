using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

// Source generators built from the workspace: a project that references another with OutputItemType="Analyzer" runs
// that project's generators, as the SDK builds the referenced project first and hands its assembly to the compiler as an
// analyzer. The main part of the class (RoslynWorkspaceHost.cs) holds the documentation.
public sealed partial class RoslynWorkspaceHost
{
    /// <summary>
    /// The generator references handed out: by generator project file, shared by every project that uses it, or by
    /// consumer and generator project file for one that cannot work for that consumer, such as a cycle. The caller holds
    /// the gate.
    /// </summary>
    private readonly Dictionary<string, GeneratorProjectEntry> _generatorProjects = new(StringComparer.Ordinal);

    private readonly Lock _generatorBuildGate = new();

    /// <summary>
    /// The last build of each generator project, by project file, reused while what it was built from is unchanged, so
    /// reloading the workspace does not load the same generators again (Android cannot unload assemblies).
    /// </summary>
    private readonly Dictionary<string, ProjectGeneratorBuild> _generatorBuilds = new(StringComparer.Ordinal);

    /// <summary>
    /// The source generators of the projects a project references with <c>OutputItemType="Analyzer"</c>, one reference
    /// per generator project. The caller holds the gate.
    /// </summary>
    private List<AnalyzerReference> GeneratorProjectReferences(TsProject project) =>
        [.. project.ProjectReferences
            .Where(path => project.GetProjectReference(path).IsAnalyzer)
            .Select(path => GeneratorProjectReference(project, path))];

    /// <summary>The reference to one generator project for a project that uses it. The caller holds the gate.</summary>
    private ProjectGeneratorReference GeneratorProjectReference(TsProject consumer, string generatorPath)
    {
        // A generator project that needs its consumer would have to be built before itself.
        if (string.Equals(generatorPath, consumer.FilePath, StringComparison.Ordinal) || ReachesProject(generatorPath, consumer.FilePath))
        {
            return FailedGeneratorReference(
                consumer.FilePath + "\n" + generatorPath,
                generatorPath,
                $"it references {consumer.Name}, which uses it as a source generator, so each would have to be built before the other.");
        }

        if (!_projectIds.TryGetValue(generatorPath, out var generatorId))
        {
            return FailedGeneratorReference(generatorPath, generatorPath, "it is not a C# project of the open workspace that TabStudio compiles.");
        }

        if (_generatorProjects.TryGetValue(generatorPath, out var known))
        {
            return known.Reference;
        }

        // The generator and the projects it compiles against, whose assemblies it loads when it runs.
        var generator = _projects[generatorId];
        var inputs = new List<GeneratorInput> { GeneratorInputOf(generatorId, generator) };
        foreach (var path in CompileReferencesOf(generator))
        {
            if (_projectIds.TryGetValue(path, out var id))
            {
                inputs.Add(GeneratorInputOf(id, _projects[id]));
            }
        }

        var reference = new ProjectGeneratorReference(generator.Name, () => BuildGeneratorProject(generator, inputs));
        _generatorProjects[generatorPath] = new GeneratorProjectEntry(reference, new HashSet<string>(inputs.Select(i => i.Project.FilePath), StringComparer.Ordinal));
        return reference;
    }

    /// <summary>A reference that gives no generators, with the reason, kept so its identity stays the same. The caller holds the gate.</summary>
    private ProjectGeneratorReference FailedGeneratorReference(string key, string generatorPath, string failure)
    {
        if (_generatorProjects.TryGetValue(key, out var known))
        {
            return known.Reference;
        }

        var name = _projectsByPath.TryGetValue(generatorPath, out var project) ? project.Name : Path.GetFileNameWithoutExtension(generatorPath);
        var reference = new ProjectGeneratorReference(name, () => ProjectGeneratorBuild.Failed(string.Empty, failure));
        _generatorProjects[key] = new GeneratorProjectEntry(reference, new HashSet<string>(StringComparer.Ordinal));
        return reference;
    }

    /// <summary>A project of a generator build, with the package assemblies it compiles against. The caller holds the gate.</summary>
    private GeneratorInput GeneratorInputOf(ProjectId id, TsProject project)
    {
        var packages = _packageReferences.TryGetValue(project.FilePath, out var references)
            ? references.OfType<PortableExecutableReference>().Select(r => r.FilePath).OfType<string>().ToList()
            : [];
        return new GeneratorInput(id, project, packages);
    }

    /// <summary>Whether a project references another, directly or not, in any way. The caller holds the gate.</summary>
    private bool ReachesProject(string fromPath, string targetPath)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([fromPath]);
        while (pending.TryPop(out var path))
        {
            if (!seen.Add(path) || !_projectsByPath.TryGetValue(path, out var project))
            {
                continue;
            }

            foreach (var reference in project.ProjectReferences)
            {
                if (string.Equals(reference, targetPath, StringComparison.Ordinal))
                {
                    return true;
                }

                pending.Push(reference);
            }
        }

        return false;
    }

    /// <summary>
    /// Forgets the generator builds made from a project after it changed (its sources, packages or analyzers), and gives
    /// the projects that use them new references, so their next compilation builds the generators again. The caller
    /// holds the gate.
    /// </summary>
    /// <param name="projectFilePath">The project that changed.</param>
    private void InvalidateGenerators(string projectFilePath)
    {
        // A consumer whose generated code changes may itself be a generator project, or one a generator compiles against.
        var changed = new Queue<string>([projectFilePath]);
        var seen = new HashSet<string>(StringComparer.Ordinal) { projectFilePath };
        while (changed.TryDequeue(out var path))
        {
            var stale = _generatorProjects.Where(e => e.Value.Inputs.Contains(path)).Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
            if (stale.Count == 0)
            {
                continue;
            }

            foreach (var key in stale)
            {
                _generatorProjects.Remove(key);
            }

            // Only shared references have inputs, and their keys are the generator projects' files.
            var consumers = _projects.Values
                .Where(p => p.ProjectReferences.Any(r => stale.Contains(r) && p.GetProjectReference(r).IsAnalyzer))
                .Select(p => p.FilePath)
                .ToList();
            foreach (var consumer in consumers)
            {
                ApplyAnalyzerReferences(consumer);
                if (seen.Add(consumer))
                {
                    changed.Enqueue(consumer);
                }
            }
        }
    }

    /// <summary>
    /// Compiles a generator project and the projects it compiles against into memory and loads its generators. Roslyn
    /// calls this from the compilation of a project that uses them, on any thread, so it reads the workspace without the
    /// gate, and it never throws: a failure is the build's <see cref="ProjectGeneratorBuild.Failure"/>.
    /// </summary>
    private ProjectGeneratorBuild BuildGeneratorProject(TsProject generator, List<GeneratorInput> inputs)
    {
        try
        {
            var solution = _workspace.CurrentSolution;
            var compilations = new List<(GeneratorInput Input, RoslynCompilation Compilation)>();
            foreach (var input in inputs)
            {
                var compilation = solution.GetProject(input.Id) is { } project ? project.GetCompilationAsync().GetAwaiter().GetResult() : null;
                if (compilation is null)
                {
                    // The workspace is being loaded again; the reference is replaced, or asks again.
                    return ProjectGeneratorBuild.Failed(string.Empty, $"{input.Project.Name} is not loaded.") with { IsTransient = true };
                }

                compilations.Add((input, compilation));
            }

            var key = GeneratorBuildKey(compilations);
            lock (_generatorBuildGate)
            {
                if (_generatorBuilds.TryGetValue(generator.FilePath, out var previous) && string.Equals(previous.Key, key, StringComparison.Ordinal))
                {
                    return previous;
                }
            }

            var build = EmitAndLoadGenerators(generator, compilations, key);
            lock (_generatorBuildGate)
            {
                _generatorBuilds[generator.FilePath] = build;
            }

            return build;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Whatever goes wrong, the project that uses the generators still compiles, without their code.
            return ProjectGeneratorBuild.Failed(string.Empty, $"building it failed: {ex.Message}");
        }
    }

    /// <summary>Emits the generator project and its references, loads them in a context of their own and creates the generators.</summary>
    private static ProjectGeneratorBuild EmitAndLoadGenerators(TsProject generator, List<(GeneratorInput Input, RoslynCompilation Compilation)> compilations, string key)
    {
        // A project referenced as an analyzer only for its types, as StronglyTypedId's attributes project is, has
        // nothing to load.
        if (!DeclaresGenerators(compilations[0].Compilation))
        {
            return new ProjectGeneratorBuild(key, [], null);
        }

        var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < compilations.Count; i++)
        {
            var (input, compilation) = compilations[i];
            using var image = new MemoryStream();
            var emit = compilation.Emit(image, manifestResources: ManifestResources.For(input.Project));
            if (!emit.Success)
            {
                var errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                var count = errors.Count.ToString(CultureInfo.InvariantCulture) + (errors.Count == 1 ? " error" : " errors");
                var first = errors.Count > 0 ? DiagnosticMapper.Map(errors[0], input.Project.Name).ToString() : "none was reported";
                var subject = i == 0 ? "it does not compile" : $"it references {input.Project.Name}, which does not compile";
                return ProjectGeneratorBuild.Failed(key, $"{subject} ({count}; the first: {first}).");
            }

            images[compilation.AssemblyName ?? input.Project.Name] = image.ToArray();
        }

        var packages = compilations.SelectMany(c => c.Input.PackageAssemblies).Distinct(StringComparer.Ordinal);
        var context = new GeneratorLoadContext(generator.Name, images, packages);
        var assembly = context.LoadProject(compilations[0].Compilation.AssemblyName ?? generator.Name);

        var generators = ImmutableArray.CreateBuilder<ISourceGenerator>();
        var failures = new List<string>();
        foreach (var type in LoadableTypes(assembly, failures))
        {
            try
            {
                if (type.IsAbstract || type.ContainsGenericParameters
                    || type.GetCustomAttribute<GeneratorAttribute>(inherit: false) is not { } attribute
                    || !attribute.Languages.Contains(LanguageNames.CSharp, StringComparer.Ordinal))
                {
                    continue;
                }

                var instance = Activator.CreateInstance(type);
                if (instance is IIncrementalGenerator incremental)
                {
                    generators.Add(incremental.AsSourceGenerator());
                }
                else if (instance is ISourceGenerator sourceGenerator)
                {
                    generators.Add(sourceGenerator);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // As the compiler's CS8032: the other generators of the project still run.
                failures.Add($"{type.FullName} ({(ex as TargetInvocationException)?.InnerException?.Message ?? ex.Message})");
            }
        }

        var failure = failures.Count == 0 ? null : $"some of its generators cannot be created: {string.Join("; ", failures)}.";
        return new ProjectGeneratorBuild(key, generators.ToImmutable(), failure);
    }

    /// <summary>
    /// Whether a compilation declares a type with <c>[Generator]</c>, which the compiler would create from its assembly,
    /// or with a <c>[Generator]</c> that does not resolve, as when the project's packages are not restored, so that its
    /// errors are reported.
    /// </summary>
    private static bool DeclaresGenerators(RoslynCompilation compilation)
    {
        static bool IsGeneratorAttribute(INamedTypeSymbol? type) =>
            type is IErrorTypeSymbol { Name: "Generator" or "GeneratorAttribute" }
            || string.Equals(type?.ToDisplayString(), "Microsoft.CodeAnalysis.GeneratorAttribute", StringComparison.Ordinal);

        var pending = new Stack<INamespaceOrTypeSymbol>([compilation.Assembly.GlobalNamespace]);
        while (pending.TryPop(out var container))
        {
            foreach (var member in container.GetMembers())
            {
                if (member is not INamespaceOrTypeSymbol inner)
                {
                    continue;
                }

                if (inner is INamedTypeSymbol type && type.GetAttributes().Any(a => IsGeneratorAttribute(a.AttributeClass)))
                {
                    return true;
                }

                pending.Push(inner);
            }
        }

        return false;
    }

    /// <summary>
    /// The types of an assembly that load. One whose base type or interface cannot be loaded, such as a generator that
    /// needs a newer Roslyn than TabStudio's, is left out, and the first reason is added to <paramref name="failures"/>.
    /// </summary>
    private static IEnumerable<Type> LoadableTypes(Assembly assembly, List<string> failures)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (ex.LoaderExceptions.OfType<Exception>().FirstOrDefault() is { } first)
            {
                failures.Add(first.Message);
            }

            return ex.Types.OfType<Type>();
        }
    }

    /// <summary>
    /// A hash of everything a generator build is made from: the sources and references of each compilation, the files
    /// it embeds as resources, such as the templates StronglyTypedId's generator reads, and the package assemblies its
    /// generators may load.
    /// </summary>
    private static string GeneratorBuildKey(List<(GeneratorInput Input, RoslynCompilation Compilation)> compilations)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value + "\n"));

        foreach (var (input, compilation) in compilations)
        {
            Add(compilation.AssemblyName ?? string.Empty);
            foreach (var resource in ProjectSourceFiles.EnumerateItems(input.Project, input.Project.EmbeddedResourceIncludes, input.Project.EmbeddedResourceRemoves))
            {
                // A file that is gone reads as 1601-01-01, which still differs from when it was there.
                Add(resource);
                Add(File.GetLastWriteTimeUtc(resource).Ticks.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var package in input.PackageAssemblies)
            {
                Add(package);
            }

            foreach (var tree in compilation.SyntaxTrees)
            {
                Add(tree.FilePath);
                Add(tree.GetText().ToString());
            }

            foreach (var reference in compilation.References)
            {
                Add((reference as PortableExecutableReference)?.FilePath ?? reference.Display ?? string.Empty);
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>
    /// The warnings of a project whose generator projects did not run (see <see cref="ProjectBuilder.GeneratorProjectFailedId"/>),
    /// one per generator project.
    /// </summary>
    private static IEnumerable<BuildDiagnostic> GeneratorProjectFailures(TsProject project, IEnumerable<AnalyzerReference> analyzers) =>
        analyzers
            .OfType<ProjectGeneratorReference>()
            .Select(reference => (reference.Display, reference.Failure))
            .Where(failed => failed.Failure is not null)
            .Select(failed => new BuildDiagnostic(
                ProjectBuilder.GeneratorProjectFailedId,
                DiagnosticSeverityLevel.Warning,
                $"The source generators of project {failed.Display} did not run: {failed.Failure} The code they generate is missing.",
                null,
                project.Name));

    /// <summary>A generator reference handed out, and the projects its build is made from.</summary>
    /// <param name="Reference">The reference.</param>
    /// <param name="Inputs">The project files whose changes make it stale; empty for one that never builds.</param>
    private sealed record GeneratorProjectEntry(ProjectGeneratorReference Reference, HashSet<string> Inputs);

    /// <summary>A project a generator build compiles.</summary>
    /// <param name="Id">Its Roslyn project.</param>
    /// <param name="Project">The project.</param>
    /// <param name="PackageAssemblies">The restored package assemblies it compiles against, which its code may load when it runs.</param>
    private sealed record GeneratorInput(ProjectId Id, TsProject Project, List<string> PackageAssemblies);
}
