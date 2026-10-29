using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// Ctrl+Shift+B on a toolchain project compiles it as F5 would, and stops before running it: a compiled language's
/// errors land in the Error List, and a language that runs from its source says there is nothing to build.
/// </summary>
public sealed class ToolchainBuildTests
{
    private const string Makefile = "hello: hello.c\n\tcc -o hello hello.c\n\nrun: hello\n\t./hello\n";

    [RealServerAvaloniaFact("make", "cc")]
    public async Task Build_compiles_a_C_program_without_running_it_and_lists_its_errors()
    {
        using var fixture = new WorkspaceFixture();
        using var folder = new TempDirectory();
        folder.Write("Makefile", Makefile);
        var source = folder.Write("hello.c", "#include <stdio.h>\nint main(void) {\n  FILE *f = fopen(\"ran.txt\", \"w\");\n  fputs(\"ran\", f);\n  fclose(f);\n  return 0;\n}\n");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder.Path);

        await workspace.BuildCommand.ExecuteAsync(null);

        Assert.True(workspace.State == IdeState.Ready, workspace.Output.Text);
        Assert.Contains("Build succeeded", workspace.Output.Text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(folder.Path, "hello")), workspace.Output.Text);
        Assert.False(File.Exists(Path.Combine(folder.Path, "ran.txt")), "Build ran the program.");
        Assert.DoesNotContain("has exited with code", workspace.Output.Text, StringComparison.Ordinal);

        // A compile error is the build's, in the Error List at its line.
        File.WriteAllText(source, "int main(void) {\n  return missing;\n}\n");
        await workspace.BuildCommand.ExecuteAsync(null);

        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains("The build of", workspace.Output.Text, StringComparison.Ordinal);
        var error = Assert.Single(workspace.ErrorList.Items, i => i.File == "hello.c");
        Assert.Equal("2", error.Line);
    }

    [AvaloniaFact]
    public async Task Build_says_a_Python_project_runs_from_its_source()
    {
        using var fixture = new WorkspaceFixture();
        using var folder = new TempDirectory();
        folder.Write("main.py", "print('hello')\n");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder.Path);

        await workspace.BuildCommand.ExecuteAsync(null);

        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.Contains("needs no build", workspace.Output.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Running", workspace.Output.Text, StringComparison.Ordinal);
    }
}
