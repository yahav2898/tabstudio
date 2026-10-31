using Microsoft.CodeAnalysis;
using TabStudio.Compilation.WindowsForms;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// Windows Forms projects: they compile against TabStudio's Forms layer, with the global usings and the
/// <c>ApplicationConfiguration</c> class the Windows Desktop SDK gives them.
/// </summary>
public sealed partial class RoslynWorkspaceHost
{
    private IReadOnlyList<MetadataReference> _windowsFormsReferences = [];

    /// <summary>
    /// Sets the assemblies Windows Forms projects compile against: the reference assembly of TabStudio's Forms layer,
    /// with its documentation for IntelliSense. Projects already loaded get them too.
    /// </summary>
    /// <param name="references">The references; empty to compile Windows Forms projects against the framework only.</param>
    public void SetWindowsFormsReferences(IReadOnlyList<MetadataReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        lock (_gate)
        {
            _windowsFormsReferences = references;
            foreach (var project in _projects.Values.Where(WindowsFormsProject.UsesFormsLayer).ToList())
            {
                ApplyMetadataReferences(project.FilePath);
            }
        }
    }

    /// <summary>
    /// The Forms layer's references for a Windows Forms project, or a .NET Framework project that draws with
    /// System.Drawing (see <see cref="WindowsFormsProject.UsesFormsLayer"/>), or none. The caller holds the gate.
    /// </summary>
    private IReadOnlyList<MetadataReference> WindowsFormsReferencesFor(TsProject project) =>
        WindowsFormsProject.UsesFormsLayer(project) ? _windowsFormsReferences : [];

    /// <summary>The generated documents of a Windows Forms app: its <c>ApplicationConfiguration</c> class.</summary>
    private static IEnumerable<DocumentInfo> WindowsFormsDocuments(ProjectId id, TsProject project)
    {
        if (WindowsFormsProject.CreateApplicationConfigurationSource(project) is not { } source)
        {
            yield break;
        }

        var path = Path.Combine(project.DirectoryPath, "obj", "System.Windows.Forms.Generators", "ApplicationConfiguration.g.cs");
        yield return CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, source, isGenerated: true);
    }
}
