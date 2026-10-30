using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

// Skeleton references: on small devices, projects nobody is editing are referenced as metadata-only images
// instead of full compilations. The main part of the class (RoslynWorkspaceHost.cs) holds the documentation.
public sealed partial class RoslynWorkspaceHost
{
    private readonly Dictionary<string, MetadataReference> _skeletons = new(StringComparer.Ordinal);
    private HashSet<string> _activeProjects = new(StringComparer.Ordinal);
    private string? _skeletonDirectory;

    /// <summary>
    /// Turns skeleton references on: a project that nobody is editing is compiled once into a metadata-only
    /// image (its public and internal signatures, no method bodies), and the projects that reference it compile
    /// against that image. Only projects with open documents keep a full compilation, which is what lets large
    /// solutions fit in the memory of a small tablet. Images are cached on disk, keyed by the project's content.
    /// </summary>
    /// <param name="cacheDirectory">Where to keep the images, or <see langword="null"/> to turn skeletons off.</param>
    /// <remarks>
    /// With skeletons on, IntelliSense sees the declarations of an inactive project but not its sources; Go to
    /// Definition still finds them. Builds compile every project in full, as always.
    /// </remarks>
    public void UseSkeletonReferences(string? cacheDirectory)
    {
        lock (_gate)
        {
            _skeletonDirectory = cacheDirectory;
            if (cacheDirectory is not null)
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            ReloadKeepingTexts();
        }
    }

    /// <summary>Whether skeleton references are on.</summary>
    public bool UsesSkeletonReferences
    {
        get
        {
            lock (_gate)
            {
                return _skeletonDirectory is not null;
            }
        }
    }

    /// <summary>
    /// Tells the host which projects are being edited, such as the projects of the open documents. With skeleton
    /// references on, those projects get full compilations and the others become skeletons.
    /// </summary>
    /// <param name="projectFilePaths">The project files.</param>
    public void SetActiveProjects(IEnumerable<string> projectFilePaths)
    {
        ArgumentNullException.ThrowIfNull(projectFilePaths);
        lock (_gate)
        {
            var active = new HashSet<string>(projectFilePaths, StringComparer.Ordinal);
            if (active.SetEquals(_activeProjects))
            {
                return;
            }

            _activeProjects = active;
            if (_skeletonDirectory is not null)
            {
                ReloadKeepingTexts();
            }
        }
    }

    /// <summary>Whether a project is referenced through a skeleton image instead of its compilation.</summary>
    /// <param name="projectFilePath">The project file.</param>
    public bool IsSkeleton(string projectFilePath)
    {
        lock (_gate)
        {
            return _skeletons.ContainsKey(projectFilePath);
        }
    }

    /// <summary>The skeleton images a project references in place of project references.</summary>
    private IEnumerable<MetadataReference> SkeletonReferencesFor(TsProject project) =>
        CompileReferencesOf(project).Where(_skeletons.ContainsKey).Select(r => ReferenceAliases(project, r) is { IsDefaultOrEmpty: false } aliases ? _skeletons[r].WithAliases(aliases) : _skeletons[r]);

    /// <summary>
    /// The extern aliases a project gives a project it references, from its own <c>ProjectReference</c>'s <c>Aliases</c>;
    /// a project it only gets through another is in the global namespace, as the SDK passes it.
    /// </summary>
    private static System.Collections.Immutable.ImmutableArray<string> ReferenceAliases(TsProject project, string referencePath) =>
        project.ProjectReferenceItems.FirstOrDefault(r => string.Equals(r.Path, referencePath, StringComparison.Ordinal)) is { Aliases.Count: > 0 } reference
            ? [.. reference.Aliases]
            : default;

    /// <summary>
    /// Creates skeletons for the referenced projects nobody is editing, from the solution just loaded with full
    /// project references. Returns whether any were created, in which case the caller loads the solution again.
    /// </summary>
    private bool CreateSkeletons(IReadOnlyList<TsProject> projects)
    {
        _skeletons.Clear();
        if (_skeletonDirectory is null)
        {
            return false;
        }

        var referenced = projects.SelectMany(CompileReferencesOf).ToHashSet(StringComparer.Ordinal);
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var project in projects.Where(p => referenced.Contains(p.FilePath) && !_activeProjects.Contains(p.FilePath)))
        {
            var key = SkeletonKey(project, keys);
            var path = Path.Combine(_skeletonDirectory, $"{project.Name}-{key}.dll");
            if (!File.Exists(path) && !TryEmitSkeleton(project, path))
            {
                continue;
            }

            _skeletons[project.FilePath] = CreateMappedReference(path);
        }

        return _skeletons.Count > 0;
    }

    private bool TryEmitSkeleton(TsProject project, string path)
    {
        if (_workspace.CurrentSolution.GetProject(_projectIds[project.FilePath]) is not { } roslynProject
            || roslynProject.GetCompilationAsync().GetAwaiter().GetResult() is not { } compilation)
        {
            return false;
        }

        var temporary = path + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            {
                var result = compilation.Emit(stream, options: new EmitOptions(metadataOnly: true));
                if (!result.Success)
                {
                    // A project with errors in its declarations keeps its full compilation, so its errors show.
                    return false;
                }
            }

            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>
    /// A hash of everything a project's skeleton depends on: its sources, options, packages and the keys of the
    /// projects it references.
    /// </summary>
    private string SkeletonKey(TsProject project, Dictionary<string, string> keys)
    {
        if (keys.TryGetValue(project.FilePath, out var known))
        {
            return known;
        }

        keys[project.FilePath] = string.Empty; // Guards against reference cycles.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value + "\n"));

        var roslynProject = _workspace.CurrentSolution.GetProject(_projectIds[project.FilePath])!;
        Add(roslynProject.AssemblyName);
        Add(project.NullableEnabled.ToString(CultureInfo.InvariantCulture));
        Add(project.GetProperty("AllowUnsafeBlocks") ?? string.Empty);
        Add(_optimize.ToString(CultureInfo.InvariantCulture));
        Add(StrongNameKeyFile(project) is { } keyFile ? keyFile + "\n" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(keyFile))) : string.Empty);
        Add(CreateAssemblyAttributesSource(project) ?? string.Empty);
        foreach (var document in roslynProject.Documents.OrderBy(d => d.FilePath, StringComparer.Ordinal))
        {
            Add(document.FilePath ?? document.Name);
            Add(document.GetTextAsync().GetAwaiter().GetResult().ToString());
        }

        if (_packageReferences.TryGetValue(project.FilePath, out var packages))
        {
            foreach (var package in packages.Select(p => (p as PortableExecutableReference)?.FilePath ?? p.Display ?? string.Empty).Order(StringComparer.Ordinal))
            {
                Add(package);
            }
        }

        foreach (var reference in project.ProjectReferences.Where(_projects.Values.Select(p => p.FilePath).Contains))
        {
            Add(SkeletonKey(_projects.Values.First(p => p.FilePath == reference), keys));
        }

        var key = Convert.ToHexStringLower(hash.GetHashAndReset())[..16];
        keys[project.FilePath] = key;
        return key;
    }

    /// <summary>Loads the same workspace again with the current text of every document.</summary>
    private void ReloadKeepingTexts()
    {
        if (_loaded is null)
        {
            return;
        }

        LoadUnderGate(_loaded, new SnapshotTextProvider(CurrentTexts()));
    }
}
