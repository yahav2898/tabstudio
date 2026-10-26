using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainLanguageToolsTests
{
    [RealServerAvaloniaFact("java", "javac")]
    public async Task Java_standalone_source_compiles_and_runs_without_a_build_tool()
    {
        var folder = _temp.Resolve("single-java");
        _temp.Write("single-java/Hello.java", "public class Hello { public static void main(String[] args) { System.out.println(\"Standalone Java says 42\"); } }\n");
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        using var trace = new SavedLogs(workspace, "toolchains-java-standalone");
        await RunJavaLayoutAsync(workspace);
        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.Contains("Standalone Java says 42", workspace.Output.Text, StringComparison.Ordinal);
        trace.Passed = true;
    }

    [RealServerAvaloniaFact("java", "gradle")]
    public async Task Java_Gradle_projects_run_with_both_Groovy_and_Kotlin_build_files()
    {
        foreach (var build in new[] { "build.gradle", "build.gradle.kts" })
        {
            var name = build.EndsWith(".kts", StringComparison.Ordinal) ? "kotlin" : "groovy";
            var folder = _temp.Resolve(name);
            _temp.Write(name + "/" + build, build.EndsWith(".kts", StringComparison.Ordinal) ? "plugins { java }\n" : "plugins { id 'java' }\n");
            _temp.Write(name + "/src/main/java/Hello.java", "public class Hello { public static void main(String[] args) { System.out.println(\"Gradle says 42\"); } }\n");
            await _main.OpenWorkspaceAsync(folder);
            var workspace = _main.Workspace!;
            using var trace = new SavedLogs(workspace, "toolchains-java-gradle-" + name);
            await RunJavaLayoutAsync(workspace);
            Assert.True(workspace.State == IdeState.Ready, workspace.Output.Text);
            Assert.Contains("Gradle says 42", workspace.Output.Text, StringComparison.Ordinal);
            trace.Passed = true;
        }
    }

    private static async Task RunJavaLayoutAsync(WorkspaceViewModel workspace)
    {
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await run.WaitAsync(TimeSpan.FromMinutes(2));
        }
        catch (TimeoutException)
        {
            workspace.StopCommand.Execute(null);
            await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));
            throw;
        }
    }
}
