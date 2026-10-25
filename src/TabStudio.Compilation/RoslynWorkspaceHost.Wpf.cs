using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using TabStudio.Compilation.Wpf;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// WPF projects, which build on OpenSilver: their XAML files are additional files of the project, read by TabStudio's
/// <see cref="WpfXamlGenerator"/> as they are typed (the first pass of OpenSilver's XAML compiler), and a build runs
/// the second pass with OpenSilver's own compiler (<see cref="SetWpfXamlCompiler"/>) before it emits the assembly.
/// </summary>
public sealed partial class RoslynWorkspaceHost
{
    private static readonly Xaml.GeneratorReference WpfXaml = new(new WpfXamlGenerator(), "TabStudio WPF XAML");

    private IWpfXamlCompiler? _wpfXamlCompiler;

    /// <summary>
    /// Sets what runs the second pass of OpenSilver's XAML compiler when a WPF project builds. Without one, a WPF
    /// project builds from the first pass alone: its code compiles, and <c>InitializeComponent</c> says the XAML was
    /// not compiled.
    /// </summary>
    /// <param name="compiler">The compiler, or <see langword="null"/>.</param>
    public void SetWpfXamlCompiler(IWpfXamlCompiler? compiler)
    {
        lock (_gate)
        {
            _wpfXamlCompiler = compiler;
        }
    }

    /// <summary>Whether a file is a XAML file of a loaded WPF project.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public bool IsWpfXamlDocument(string filePath)
    {
        lock (_gate)
        {
            return _additionalDocuments.TryGetValue(filePath, out var id)
                && filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                && _projects.TryGetValue(id.ProjectId, out var project)
                && WpfProject.UsesWpf(project);
        }
    }

    /// <summary>
    /// A XAML file of a WPF project with what the editor's XAML features need: its current text, and its project's
    /// compilation, whose types the XAML names.
    /// </summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The file, or <see langword="null"/> when it is not a XAML file of a loaded WPF project.</returns>
    public async Task<WpfXamlFile?> GetWpfXamlFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Project? project;
        TextDocument? document;
        TsProject? info;
        lock (_gate)
        {
            if (!_additionalDocuments.TryGetValue(filePath, out var id) || !_projects.TryGetValue(id.ProjectId, out info) || !WpfProject.UsesWpf(info))
            {
                return null;
            }

            project = _workspace.CurrentSolution.GetProject(id.ProjectId);
            document = project?.GetAdditionalDocument(id);
        }

        if (project is null || document is null || await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is not { } compilation)
        {
            return null;
        }

        var text = (await document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        var projectXaml = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var other in project.AdditionalDocuments)
        {
            if (other.FilePath is { } path && path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                projectXaml[path] = (await other.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
            }
        }

        return new WpfXamlFile(filePath, text, info, compilation, projectXaml);
    }

    /// <summary>The text of a XAML file of a loaded WPF project as the editor last sent it, when it is at hand without waiting.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public string? TryGetWpfXamlText(string filePath)
    {
        lock (_gate)
        {
            return _additionalDocuments.TryGetValue(filePath, out var id)
                && _workspace.CurrentSolution.GetAdditionalDocument(id) is { } document
                && document.TryGetText(out var text)
                    ? text.ToString()
                    : null;
        }
    }

    /// <summary>The project's XAML files as additional documents, which the generator and the editor read. The caller holds the gate.</summary>
    private void AddWpfDocuments(TsProject project, ProjectId id, ISourceTextProvider sources, List<DocumentInfo> additionalDocuments)
    {
        if (!WpfProject.UsesWpf(project))
        {
            return;
        }

        foreach (var item in WpfProject.XamlItems(project))
        {
            var documentId = DocumentId.CreateNewId(id, item.FilePath);
            additionalDocuments.Add(CreateDocumentInfo(documentId, item.FilePath, ReadSource(sources, item.FilePath), isGenerated: false));
            _additionalDocuments[item.FilePath] = documentId;
        }
    }

    /// <summary>How each XAML file of a WPF project is built, as MSBuild hands item metadata to generators.</summary>
    private static void AppendWpfAnalyzerConfig(TsProject project, StringBuilder sections)
    {
        if (!WpfProject.UsesWpf(project))
        {
            return;
        }

        foreach (var item in WpfProject.XamlItems(project))
        {
            sections.Append(CultureInfo.InvariantCulture, $"\n[{item.FilePath.Replace('\\', '/')}]\n");
            sections.Append(CultureInfo.InvariantCulture, $"{WpfXamlGenerator.ItemKindMetadata} = {item.Kind}\n");
        }
    }

    /// <summary>TabStudio's XAML generator, for a WPF project.</summary>
    private static IEnumerable<Microsoft.CodeAnalysis.Diagnostics.AnalyzerReference> WpfAnalyzers(TsProject project) =>
        WpfProject.UsesWpf(project) ? [WpfXaml] : [];

    /// <summary>
    /// Builds a WPF project: its first pass, then the second pass of OpenSilver's XAML compiler, then the assembly
    /// with the second pass's code. Programs run in the browser, so the in-process checks do not apply.
    /// </summary>
    /// <returns>The build, or <see langword="null"/> for a project that is not a WPF project.</returns>
    private async Task<CompilationResult?> EmitWpfAsync(TsProject info, RoslynCompilation compilation, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        if (!WpfProject.UsesWpf(info))
        {
            return null;
        }

        IWpfXamlCompiler? compiler;
        lock (_gate)
        {
            compiler = _wpfXamlCompiler;
        }

        var resources = ManifestResources.For(info);
        var first = CSharpCompiler.Emit(compilation, checkInProcessSafety: false, stopwatch, info.Name, resources, d => WpfDiagnostics.Map(d, info), cancellationToken);
        if (compiler is null || first.Assembly is not { } image)
        {
            return first;
        }

        var second = await compiler.CompileAsync(
            new WpfSecondPassRequest { Project = info, Compilation = compilation, FirstPassImage = image.Image },
            cancellationToken).ConfigureAwait(false);
        if (second.Compilation is not { } completed)
        {
            stopwatch.Stop();
            return new CompilationResult(first.AssemblyName, [.. second.Diagnostics, .. first.Diagnostics], null, stopwatch.Elapsed);
        }

        stopwatch.Start();
        var final = CSharpCompiler.Emit(completed, checkInProcessSafety: false, stopwatch, info.Name, ManifestResources.For(info), d => WpfDiagnostics.Map(d, info), cancellationToken);
        return final with { Diagnostics = [.. second.Diagnostics, .. final.Diagnostics] };
    }
}

/// <summary>A XAML file of a WPF project, with its project's compilation.</summary>
/// <param name="FilePath">The file.</param>
/// <param name="Text">Its current text, as the editor has it.</param>
/// <param name="Project">Its project.</param>
/// <param name="Compilation">Its project's compilation, whose types its XAML names.</param>
/// <param name="ProjectXaml">The text of every XAML file of the project, this one too, by path.</param>
public sealed record WpfXamlFile(string FilePath, string Text, TsProject Project, RoslynCompilation Compilation, IReadOnlyDictionary<string, string> ProjectXaml)
{
    /// <summary>The XAML type system of the compilation.</summary>
    public XamlTypeSystem Types => XamlTypeSystem.For(Compilation);
}
