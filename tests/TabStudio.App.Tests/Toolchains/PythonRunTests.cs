using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Compatibility;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// F5 on Python projects, with the computer's own python3 as the toolchain (CI's runners have one): the program's
/// console in the Output panel, its input from the input box, and its errors in the Error List with links.
/// </summary>
public sealed class PythonRunTests
{
    [AvaloniaFact]
    public async Task F5_runs_a_Python_app_that_reads_input()
    {
        using var fixture = new WorkspaceFixture();
        var folder = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.PythonId, "Greeter");
        fixture.AnswerInput("Ada");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument is not null, because: "main.py");

        Assert.Equal("Greeter", workspace.StartupProject?.Name);
        Assert.Equal("Python toolchain", workspace.RunHostText);
        Assert.EndsWith("main.py", workspace.ActiveDocument!.FilePath, StringComparison.Ordinal);
        await workspace.RunCommand.ExecuteAsync(null);

        var output = workspace.Output.Text;
        Assert.Contains("Running main.py with Python 3.", output, StringComparison.Ordinal);
        Assert.Contains("What is your name? Ada", output, StringComparison.Ordinal);
        Assert.Contains("Hello, Ada! This is Python 3.", output, StringComparison.Ordinal);
        Assert.Contains("The program 'main.py' has exited with code 0 (0x0).", output, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.Contains(workspace.Compatibility, r => r.ToolchainId == "python" && r.Report.Status == CompatibilityStatus.Ready);
    }

    [AvaloniaFact]
    public async Task A_Python_error_lands_in_the_Error_List_with_a_link_to_its_line()
    {
        using var fixture = new WorkspaceFixture();
        using var folder = new TempDirectory();
        var main = folder.Write("main.py", "def greet(name):\n    return 'Hello, ' + nme\n\n\nprint(greet('Ada'))\n");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder.Path);

        await workspace.RunCommand.ExecuteAsync(null);

        Assert.Equal(IdeState.Failed, workspace.State);
        var error = Assert.Single(workspace.ErrorList.Items);
        Assert.StartsWith("NameError: name 'nme' is not defined", error.Description, StringComparison.Ordinal);
        Assert.Equal(("main.py", "2"), (error.File, error.Line));

        var frame = workspace.Output.Text.Split('\n').First(l => l.Contains("line 2", StringComparison.Ordinal));
        var link = Assert.Single(workspace.Output.LinkFinder!(frame));
        Assert.Equal((main, 2), (link.FilePath, link.Line));
        workspace.Output.ActivateLink(link);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.FilePath == main, because: "the link to open main.py");
        Assert.Equal(2, workspace.ActiveDocument!.CaretLine);
    }

    [AvaloniaFact]
    public async Task Run_Current_File_runs_the_script_being_edited()
    {
        using var fixture = new WorkspaceFixture();
        using var folder = new TempDirectory();
        folder.Write("main.py", "print('main')\n");
        var tool = folder.Write("tools/report.py", "import sys\nprint('report', sys.argv[0])\n");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder.Path);
        await workspace.OpenDocumentAsync(tool);

        Assert.True(workspace.RunCurrentFileCommand.CanExecute(null));
        await workspace.RunCurrentFileCommand.ExecuteAsync(null);

        Assert.Contains("report tools/report.py", workspace.Output.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("\nmain\n", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Screenshot_of_a_Python_run()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        using var services = AppServices.Create(paths);
        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        await main.OpenWorkspaceAsync(new ProjectTemplates(paths).Create(ProjectTemplates.PythonId, "Greeter"));
        var workspace = main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument is not null, because: "main.py");
        workspace.Output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.InputText = "Ada";
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        };

        await workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "32-python-run");

        Assert.Contains("Hello, Ada!", workspace.Output.Text, StringComparison.Ordinal);
        workspace.Dispose();
    }
}
