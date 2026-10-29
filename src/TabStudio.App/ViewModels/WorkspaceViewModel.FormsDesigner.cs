using TabStudio.Compilation.VisualBasic;
using TabStudio.Compilation.WindowsForms;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The Windows Forms designer: the code file of a form of a Windows Forms project, such as Form1.cs next to
/// Form1.Designer.cs, or a Visual Basic project's Form1.vb next to Form1.Designer.vb, opens on the designer, as in
/// Visual Studio.
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
        var extension = Path.GetExtension(path);
        var language = string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ? ProjectLanguage.CSharp
            : string.Equals(extension, ".vb", StringComparison.OrdinalIgnoreCase) ? ProjectLanguage.VisualBasic
            : (ProjectLanguage?)null;
        if (document.IsComparison
            || language is null
            || path.EndsWith(".Designer" + extension, StringComparison.OrdinalIgnoreCase)
            || Workspace?.FindProjectForFile(path) is not { } project
            || project.Language != language
            || !(project.Kind == ProjectKind.WinForms || WindowsFormsProject.UsesWindowsForms(project)))
        {
            return;
        }

        var designer = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + ".Designer" + extension);
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

        var simplified = project.IsVisualBasic ? ImportsWindowsForms(project) : project.ImplicitUsings;
        document.FormsDesigner = new FormsDesignerViewModel(document, designer, simplified, this);
    }

    /// <summary>
    /// Whether a Visual Basic project imports System.Drawing and System.Windows.Forms into every file, as the Windows
    /// Desktop SDK does: Visual Studio then writes its designer files without <c>Me.</c> and namespaces.
    /// </summary>
    private static bool ImportsWindowsForms(ProjectInfo project)
    {
        var imports = VisualBasicCompiler.ImportsOf(project);
        return imports.Contains("System.Drawing", StringComparer.OrdinalIgnoreCase) && imports.Contains("System.Windows.Forms", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Ends the designer of a closing document.</summary>
    private static void DetachFormsDesigner(DocumentViewModel document)
    {
        document.FormsDesigner?.Dispose();
        document.FormsDesigner = null;
    }
}
