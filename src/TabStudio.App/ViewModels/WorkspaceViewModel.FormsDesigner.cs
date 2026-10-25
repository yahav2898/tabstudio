using TabStudio.Compilation.WindowsForms;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The Windows Forms designer: the code file of a form of a Windows Forms project, such as Form1.cs next to
/// Form1.Designer.cs, opens on the designer, as in Visual Studio.
/// </summary>
public sealed partial class WorkspaceViewModel : IFormsDesignerHost
{
    /// <inheritdoc />
    DocumentViewModel? IFormsDesignerHost.FindOpenDocument(string path) =>
        Documents.FirstOrDefault(d => !d.IsComparison && string.Equals(d.FilePath, path, StringComparison.Ordinal));

    /// <inheritdoc />
    void IFormsDesignerHost.UpdateLanguageText(string path, string text)
    {
        if (_language is { } language && language.ContainsDocument(path))
        {
            language.UpdateText(path, text);
        }
    }

    /// <inheritdoc />
    void IFormsDesignerHost.ShowStatus(string message) => StatusText = message;

    /// <summary>Gives a form's code file its designer as it opens.</summary>
    private void AttachFormsDesigner(DocumentViewModel document)
    {
        var path = document.FilePath;
        if (document.IsComparison
            || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || Workspace?.FindProjectForFile(path) is not { Language: ProjectLanguage.CSharp } project
            || !(project.Kind == ProjectKind.WinForms || WindowsFormsProject.UsesWindowsForms(project)))
        {
            return;
        }

        var designer = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + ".Designer.cs");
        try
        {
            if (!File.Exists(designer) || !File.ReadAllText(designer).Contains("InitializeComponent", StringComparison.Ordinal))
            {
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        document.FormsDesigner = new FormsDesignerViewModel(document, designer, project.ImplicitUsings, this);
    }

    /// <summary>Ends the designer of a closing document.</summary>
    private static void DetachFormsDesigner(DocumentViewModel document)
    {
        document.FormsDesigner?.Dispose();
        document.FormsDesigner = null;
    }
}
