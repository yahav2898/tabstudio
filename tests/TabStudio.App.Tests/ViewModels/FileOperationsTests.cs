using Avalonia.Headless.XUnit;
using TabStudio.App.ViewModels;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Tests.ViewModels;

public sealed class FileOperationsTests
{
    [AvaloniaFact]
    public async Task New_file_in_a_project_creates_a_class_in_the_right_namespace()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greetings = Node(fixture, n => n.Kind == WorkspaceNodeKind.Project && n.Name == "Greetings");

        await AnswerAsync(fixture, ((IExplorerActions)fixture.Workspace).NewFileAsync(greetings), "Farewell");

        var path = Path.Combine(Path.GetDirectoryName(greetings.FullPath!)!, "Farewell.cs");
        Assert.True(File.Exists(path));
        Assert.Contains("namespace Greetings;", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
        Assert.Contains("public class Farewell", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
        Assert.Equal("Farewell.cs", fixture.Workspace.ActiveDocument?.FileName);
        Assert.True(fixture.Workspace.ActiveDocument!.HasLanguageService);
        Assert.Contains(fixture.Workspace.Explorer.Rows, r => r.Name == "Farewell.cs");
    }

    [AvaloniaFact]
    public async Task A_new_class_is_usable_from_other_files_right_away()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greetings = Node(fixture, n => n.Kind == WorkspaceNodeKind.Project && n.Name == "Greetings");
        await AnswerAsync(fixture, ((IExplorerActions)fixture.Workspace).NewFileAsync(greetings), "Farewell");

        var program = fixture.Workspace.Documents.Single(d => d.FileName == "Program.cs");
        var afterUsings = program.Document.Text.IndexOf("using Greetings;", StringComparison.Ordinal) + "using Greetings;".Length;
        program.Document.Insert(afterUsings, "\nSystem.Console.WriteLine(new Farewell());");
        await program.AnalyzeNowAsync();

        Assert.DoesNotContain(program.Diagnostics, d => d.Severity == TabStudio.Core.Diagnostics.DiagnosticSeverityLevel.Error);
    }

    [AvaloniaFact]
    public async Task Renaming_a_file_moves_it_and_reopens_its_tab()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greeter = Node(fixture, n => n.Name == "Greeter.cs");
        await fixture.Workspace.OpenDocumentAsync(greeter.FullPath!);

        await AnswerAsync(fixture, ((IExplorerActions)fixture.Workspace).RenameAsync(greeter), "Welcomer.cs");

        var renamed = Path.Combine(Path.GetDirectoryName(greeter.FullPath!)!, "Welcomer.cs");
        Assert.True(File.Exists(renamed));
        Assert.False(File.Exists(greeter.FullPath));
        Assert.Contains(fixture.Workspace.Documents, d => d.FilePath == renamed);
        Assert.DoesNotContain(fixture.Workspace.Documents, d => d.FilePath == greeter.FullPath);
    }

    [AvaloniaFact]
    public async Task Renaming_keeps_unsaved_edits_when_they_cannot_be_saved_and_a_read_only_folder_says_so()
    {
        // The rename closed the tab whatever the save said, then the move threw: the edits were gone and the app ended.
        // (A read-only folder does not stop root.)
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }

        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greeter = Node(fixture, n => n.Name == "Greeter.cs");
        var folder = Path.GetDirectoryName(greeter.FullPath!)!;
        var document = (await fixture.Workspace.OpenDocumentAsync(greeter.FullPath!))!;
        document.Document.Insert(0, "// unsaved\n");
        var mode = File.GetUnixFileMode(folder);
        File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            await AnswerAsync(fixture, ((IExplorerActions)fixture.Workspace).RenameAsync(greeter), "Welcomer.cs");

            Assert.True(File.Exists(greeter.FullPath));
            Assert.Contains(document, fixture.Workspace.Documents);
            Assert.True(document.IsDirty);
            Assert.StartsWith("// unsaved", document.Document.Text, StringComparison.Ordinal);
            Assert.Contains("was not renamed", fixture.Workspace.StatusText, StringComparison.Ordinal);

            await AnswerAsync(fixture, ((IExplorerActions)fixture.Workspace).NewFileAsync(Node(fixture, n => n.Name == "Greetings" && n.Kind == WorkspaceNodeKind.Project)), "Farewell");
            Assert.StartsWith("Could not create Farewell.cs", fixture.Workspace.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(folder, mode);
        }
    }

    [AvaloniaFact]
    public async Task Deleting_moves_the_file_to_the_trash()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greeter = Node(fixture, n => n.Name == "Greeter.cs");

        var delete = ((IExplorerActions)fixture.Workspace).DeleteAsync(greeter);
        Assert.True(fixture.Workspace.Prompt.IsOpen);
        Assert.False(fixture.Workspace.Prompt.HasInput);
        fixture.Workspace.Prompt.ConfirmCommand.Execute(null);
        await delete;

        Assert.False(File.Exists(greeter.FullPath));
        Assert.Single(Directory.EnumerateFiles(fixture.Paths.Trash, "Greeter.cs", SearchOption.AllDirectories));
        Assert.DoesNotContain(fixture.Workspace.Explorer.Rows, r => r.Name == "Greeter.cs");
    }

    [AvaloniaFact]
    public async Task Cancelling_the_dialog_changes_nothing()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greeter = Node(fixture, n => n.Name == "Greeter.cs");

        var delete = ((IExplorerActions)fixture.Workspace).DeleteAsync(greeter);
        fixture.Workspace.Prompt.CancelCommand.Execute(null);
        await delete;

        Assert.True(File.Exists(greeter.FullPath));
    }

    private static WorkspaceNode Node(WorkspaceFixture fixture, Func<WorkspaceNode, bool> predicate) =>
        fixture.Workspace.Workspace!.Tree.DescendantsAndSelf().First(predicate);

    private static async Task AnswerAsync(WorkspaceFixture fixture, Task action, string answer)
    {
        Assert.True(fixture.Workspace.Prompt.IsOpen);
        fixture.Workspace.Prompt.Text = answer;
        fixture.Workspace.Prompt.ConfirmCommand.Execute(null);
        await action;
    }
}
