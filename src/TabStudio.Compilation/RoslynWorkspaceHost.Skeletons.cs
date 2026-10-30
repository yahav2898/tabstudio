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

    /// <summary>Skeleton images that could not be compiled, by path: their projects keep full compilations until their code changes.</summary>
    private readonly HashSet<string> _failedSkeletons = new(StringComparer.Ordinal);

    private bool _reloadRequested;
    private bool _reloading;
    private bool _disposed;
    private TaskCompletionSource _reloaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

            RequestReloadUnderGate();
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
                RequestReloadUnderGate();
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
    /// References the images of the referenced projects nobody is editing, from the solution just loaded with full
    /// project references. Returns whether any were found, in which case the caller loads the solution again.
    /// </summary>
    /// <remarks>
    /// Nothing is compiled here, under the gate every keystroke waits for: a project whose image is not on disk yet
    /// keeps its full compilation, and the background reload (<see cref="ReloadPending"/>) compiles the image.
    /// </remarks>
    private bool CreateSkeletons(IReadOnlyList<TsProject> projects)
    {
        _skeletons.Clear();
        if (_skeletonDirectory is null)
        {
            return false;
        }

        var inputs = SkeletonInputsUnderGate();
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var project in SkeletonCandidates(projects))
        {
            var path = SkeletonPath(inputs.Directory, project, SkeletonKey(project, keys, inputs));
            if (File.Exists(path))
            {
                _skeletons[project.FilePath] = CreateMappedReference(path);
            }
            else if (!HasFailed(path))
            {
                // An image that could not be compiled is not asked for again until the project's code changes.
                RequestReloadUnderGate();
            }
        }

        return _skeletons.Count > 0;
    }

    /// <summary>The projects that become skeletons: those another project references and nobody is editing.</summary>
    private IEnumerable<TsProject> SkeletonCandidates(IReadOnlyList<TsProject> projects)
    {
        var referenced = projects.SelectMany(CompileReferencesOf).ToHashSet(StringComparer.Ordinal);
        return projects.Where(p => referenced.Contains(p.FilePath) && !_activeProjects.Contains(p.FilePath));
    }

    private static string SkeletonPath(string directory, TsProject project, string key) => Path.Combine(directory, $"{project.Name}-{key}.dll");

    /// <summary>What making skeleton keys and images reads, captured under the gate so the work can run outside it.</summary>
    private SkeletonInputs SkeletonInputsUnderGate() => new(
        _skeletonDirectory ?? string.Empty,
        _workspace.CurrentSolution,
        new Dictionary<string, ProjectId>(_projectIds, StringComparer.Ordinal),
        [.. _projects.Values],
        new Dictionary<string, IReadOnlyList<MetadataReference>>(_packageReferences, StringComparer.Ordinal),
        _optimize);

    /// <summary>Compiles a project's metadata-only image; a failure is remembered, so it is not tried again for the same content.</summary>
    private bool TryEmitSkeleton(TsProject project, string path, SkeletonInputs inputs)
    {
        var emitted = TryEmitSkeletonImage(project, path, inputs);
        if (!emitted)
        {
            lock (_failedSkeletons)
            {
                _failedSkeletons.Add(path);
            }
        }

        return emitted;
    }

    private bool HasFailed(string path)
    {
        lock (_failedSkeletons)
        {
            return _failedSkeletons.Contains(path);
        }
    }

    private static bool TryEmitSkeletonImage(TsProject project, string path, SkeletonInputs inputs)
    {
        if (!inputs.ProjectIds.TryGetValue(project.FilePath, out var id)
            || inputs.Solution.GetProject(id) is not { } roslynProject
            || roslynProject.GetCompilationAsync().GetAwaiter().GetResult() is not { } compilation)
        {
            return false;
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
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
    private static string SkeletonKey(TsProject project, Dictionary<string, string> keys, SkeletonInputs inputs)
    {
        if (keys.TryGetValue(project.FilePath, out var known))
        {
            return known;
        }

        keys[project.FilePath] = string.Empty; // Guards against reference cycles.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value + "\n"));

        var roslynProject = inputs.Solution.GetProject(inputs.ProjectIds[project.FilePath])!;
        Add(roslynProject.AssemblyName);
        Add(project.NullableEnabled.ToString(CultureInfo.InvariantCulture));
        Add(project.GetProperty("AllowUnsafeBlocks") ?? string.Empty);
        Add(inputs.Optimize.ToString(CultureInfo.InvariantCulture));
        Add(StrongNameKeyFile(project) is { } keyFile ? keyFile + "\n" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(keyFile))) : string.Empty);
        Add(CreateAssemblyAttributesSource(project) ?? string.Empty);
        foreach (var document in roslynProject.Documents.OrderBy(d => d.FilePath, StringComparer.Ordinal))
        {
            Add(document.FilePath ?? document.Name);
            Add(document.GetTextAsync().GetAwaiter().GetResult().ToString());
        }

        if (inputs.PackageReferences.TryGetValue(project.FilePath, out var packages))
        {
            foreach (var package in packages.Select(p => (p as PortableExecutableReference)?.FilePath ?? p.Display ?? string.Empty).Order(StringComparer.Ordinal))
            {
                Add(package);
            }
        }

        foreach (var reference in project.ProjectReferences.Where(inputs.Projects.Select(p => p.FilePath).Contains))
        {
            Add(SkeletonKey(inputs.Projects.First(p => p.FilePath == reference), keys, inputs));
        }

        var key = Convert.ToHexStringLower(hash.GetHashAndReset())[..16];
        keys[project.FilePath] = key;
        return key;
    }

    /// <summary>Loads the same workspace again with the current text of every document, without compiling any image.</summary>
    private void ReloadKeepingTexts()
    {
        if (_loaded is null)
        {
            return;
        }

        LoadUnderGate(_loaded, new SnapshotTextProvider(CurrentTexts()));
    }

    /// <summary>
    /// Asks for the solution to be loaded again, on a background thread, as an edit to a skeleton's project, a change
    /// of the edited projects or memory pressure need. Call under the gate. These come from the UI thread: an edit to
    /// another project's file during a Rename, a callback from Android. Reloading there froze the IDE for a whole
    /// reload and a skeleton compile, and a reload on another thread held the gate every keystroke waits for.
    /// </summary>
    private void RequestReloadUnderGate()
    {
        _reloadRequested = true;
        if (_reloading || _disposed)
        {
            return;
        }

        _reloading = true;
        _reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(ReloadPending);
    }

    /// <summary>
    /// Completes when no reload asked for by <see cref="TrimMemory"/>, <see cref="SetActiveProjects"/>,
    /// <see cref="UseSkeletonReferences"/> or an edit to a skeleton's project is pending any more.
    /// </summary>
    public Task WhenReloadedAsync()
    {
        lock (_gate)
        {
            return _reloading ? _reloaded.Task : Task.CompletedTask;
        }
    }

    /// <summary>
    /// Reloads until nothing is pending. The slow part, compiling the images of projects whose code changed, runs
    /// outside the gate from an immutable snapshot of the solution; the reload under the gate then only builds the
    /// solution again from texts in memory and images on disk, which takes milliseconds.
    /// </summary>
    private void ReloadPending()
    {
        try
        {
            while (true)
            {
                SkeletonInputs? inputs = null;
                List<TsProject>? candidates = null;
                lock (_gate)
                {
                    if (!_reloadRequested || _disposed || _loaded is null)
                    {
                        _reloadRequested = false;
                        _reloading = false;
                        _reloaded.TrySetResult();
                        return;
                    }

                    _reloadRequested = false;
                    if (_skeletonDirectory is not null)
                    {
                        inputs = SkeletonInputsUnderGate();
                        candidates = [.. SkeletonCandidates(inputs.Projects)];
                    }
                }

                var made = 0;
                if (inputs is not null && candidates is not null)
                {
                    BeforeSkeletonImages?.Invoke();
                    var keys = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var project in candidates)
                    {
                        var image = SkeletonPath(inputs.Directory, project, SkeletonKey(project, keys, inputs));
                        if (!HasFailed(image) && !File.Exists(image) && TryEmitSkeleton(project, image, inputs))
                        {
                            made++;
                        }
                    }
                }

                lock (_gate)
                {
                    if (!_disposed)
                    {
                        // Asked for while the images were compiled: an edit, a change of the edited projects.
                        var askedMeanwhile = _reloadRequested;
                        _reloadRequested = false;
                        ReloadKeepingTexts();

                        // The reload asks again when it misses an image. When nothing changed and this pass had
                        // nothing to make, another pass would make nothing either: stop instead of going round.
                        _reloadRequested = askedMeanwhile || (_reloadRequested && made > 0);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A reload that failed leaves the solution as it was, which still answers; the next request tries again.
            lock (_gate)
            {
                _reloading = false;
                _reloaded.TrySetResult();
            }
        }
    }

    /// <summary>Called on the background thread before it compiles skeleton images; lets tests hold a reload there.</summary>
    internal Action? BeforeSkeletonImages { get; set; }

    /// <summary>What making skeleton keys and images reads: an immutable solution and copies of the host's tables.</summary>
    private sealed record SkeletonInputs(
        string Directory,
        Solution Solution,
        IReadOnlyDictionary<string, ProjectId> ProjectIds,
        IReadOnlyList<TsProject> Projects,
        IReadOnlyDictionary<string, IReadOnlyList<MetadataReference>> PackageReferences,
        bool Optimize);
}
