using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.Compilation.Wpf;

namespace TabStudio.App.ViewModels;

/// <summary>Attaches WPF XAML editing to the local compilation used by this workspace.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>Connects the XAML document before its editor is shown, as for C# and language-server documents.</summary>
    private void AttachWpfXaml(DocumentViewModel document)
    {
        if (document.IsComparison || !document.FilePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
            || Workspace?.FindProjectForFile(document.FilePath) is not { } project || !WpfProject.UsesWpf(project))
        {
            return;
        }

        var local = _language switch
        {
            LocalLanguageService service => service,
            LinkedLanguageService linked => linked.Local,
            _ => null,
        };
        document.AttachWpfXaml(local?.Host, SyncOpenDocuments);
    }

    /// <summary>Refreshes XAML errors when an open code-behind file changes, including a newly added handler.</summary>
    private void RefreshWpfXaml(DocumentViewModel changed)
    {
        if (!changed.HasLanguageService || Workspace?.FindProjectForFile(changed.FilePath) is not { } project)
        {
            return;
        }

        changed.SyncText();
        foreach (var document in Documents.Where(d => d.HasXamlFeatures && Workspace.FindProjectForFile(d.FilePath)?.FilePath == project.FilePath))
        {
            _ = document.AnalyzeAgainAsync();
        }
    }

    /// <summary>Refreshes XAML after cross-file fixes, including handlers written to closed code-behind files.</summary>
    private void RefreshWpfXamlAfterEdits(IReadOnlyList<Compilation.Features.DocumentEdit> edits)
    {
        if (Workspace is not { } workspace || !Documents.Any(d => d.HasXamlFeatures))
        {
            return;
        }

        SyncOpenDocuments();
        var projects = edits.Select(e => workspace.FindProjectForFile(e.FilePath)?.FilePath).ToHashSet(StringComparer.Ordinal);
        foreach (var document in Documents.Where(d => d.HasXamlFeatures && projects.Contains(workspace.FindProjectForFile(d.FilePath)?.FilePath)))
        {
            _ = document.AnalyzeAgainAsync();
        }
    }
}
