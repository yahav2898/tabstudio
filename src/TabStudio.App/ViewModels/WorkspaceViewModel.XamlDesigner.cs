using TabStudio.App.Previews;
using TabStudio.App.Services.Agent;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The XAML designer: every <c>.axaml</c> file of an Avalonia app opens with a live preview beside its code. The
/// preview uses a build of the project made in the background, made again when one of its other files is saved.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private XamlDesignBuilds? _designBuilds;

    /// <summary>Gives an Avalonia XAML file its designer as it opens.</summary>
    private void AttachXamlDesigner(DocumentViewModel document)
    {
        if (document.IsComparison
            || !string.Equals(Path.GetExtension(document.FilePath), ".axaml", StringComparison.OrdinalIgnoreCase)
            || _language is not { } language
            || Workspace?.FindProjectForFile(document.FilePath) is not { Kind: ProjectKind.Avalonia, Language: ProjectLanguage.CSharp } project)
        {
            return;
        }

        _designBuilds ??= new XamlDesignBuilds(language, () => _restore);
        var sketches = new SketchConverter(new AgentSettingsStore(_paths), AgentHttp);
        document.Designer = new XamlDesignerViewModel(document, project, _designBuilds, sketches, () => ShowAgentCommand.Execute(null));
    }

    /// <summary>Ends the designer of a closing document.</summary>
    private static void DetachXamlDesigner(DocumentViewModel document)
    {
        document.Designer?.Dispose();
        document.Designer = null;
    }

    /// <summary>
    /// After a file of an Avalonia app is saved, its designers build the project again, so they show the new code
    /// and the views other XAML files use. The XAML being designed is previewed as it is typed.
    /// </summary>
    private void OnDocumentSavedForXamlDesigner(DocumentViewModel document)
    {
        if (_designBuilds is { } builds
            && Workspace?.FindProjectForFile(document.FilePath) is { Kind: ProjectKind.Avalonia } project)
        {
            builds.Invalidate(project);
        }
    }
}
