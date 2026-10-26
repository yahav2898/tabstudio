using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>Go module boundaries, command layouts, build plans and compiler diagnostics.</summary>
public sealed class GoProjectTests : IDisposable
{
    private readonly TempDirectory _folder = new();
    private static ToolchainDefinition Go => ToolchainCatalog.Default.Find("go")!;

    [Fact]
    public void Each_module_is_a_project_even_inside_another_module()
    {
        _folder.Write("go.mod", "module example.com/root\ngo 1.24\n");
        _folder.Write("cmd/main.go", "package main\nfunc main() {}\n");
        _folder.Write("tools/go.mod", "module example.com/tools\ngo 1.24\n");
        _folder.Write("vendor/dependency/go.mod");
        _folder.Write(".cache/go.mod");
        _folder.Write("build/go.mod");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        Assert.Equal([_folder.Path, _folder.Resolve("tools")], projects.Select(p => p.Directory));
        Assert.Equal(_folder.Resolve("tools"), ToolchainProjectDetector.ForFile(projects, _folder.Resolve("tools/main.go"))!.Directory);
        Assert.Null(ToolchainProjectDetector.ForFile(projects, _folder.Resolve("README.md")));
    }

    [Fact]
    public void Builds_the_module_command_once_then_runs_its_debuggable_binary()
    {
        _folder.Write("go.mod", "module example.com/app\ngo 1.24\n");
        _folder.Write("main.go", "package main\nfunc main() {}\n");
        var plan = RunPlanner.PlanProject(new(Go, _folder.Path, "go.mod"))!;

        Assert.Equal("go", plan.Build!.Program);
        Assert.Equal(["build", "-gcflags=all=-N -l", "-o", ".tabstudio/bin/app", "."], plan.Build.Arguments);
        Assert.Equal(_folder.Resolve(".tabstudio/bin/app"), plan.Run.Program);
        Assert.Equal("local", plan.Run.Environment["GOTOOLCHAIN"]);
        Assert.Equal(_folder.Path, plan.Run.WorkingDirectory);
    }

    [Fact]
    public void A_command_under_cmd_runs_as_its_whole_package()
    {
        _folder.Write("go.mod");
        var file = _folder.Write("cmd/hello/main.go", "package main\nfunc main() {}\n");
        var project = new ToolchainProject(Go, _folder.Path, "go.mod");

        Assert.Equal("./cmd/hello", RunPlanner.PlanProject(project)!.Build!.Arguments[^1]);
        Assert.Equal("./cmd/hello", RunPlanner.PlanFile(project, file).Build!.Arguments[^1]);
        _folder.Write("cmd/second/main.go", "package main\nfunc main() {}\n");
        Assert.Null(RunPlanner.PlanProject(project));
    }

    [Fact]
    public void A_library_has_no_command_and_an_unrelated_file_cannot_run()
    {
        _folder.Write("go.mod");
        _folder.Write("library.go", "package library\n");
        var project = new ToolchainProject(Go, _folder.Path, "go.mod");
        Assert.Null(RunPlanner.PlanProject(project));
        Assert.Throws<ArgumentException>(() => RunPlanner.PlanFile(project, _folder.Resolve("main.py")));
    }

    [Theory]
    [InlineData("./main.go:12:4: undefined: nmae", "./main.go", 12, 4)]
    [InlineData("/work/my app/main.go:3: undefined: value", "/work/my app/main.go", 3, null)]
    public void Go_build_errors_keep_their_message_and_location(string text, string file, int line, int? column)
    {
        var error = Assert.Single(new ProblemMatcher(Go).FindErrors([text], _ => true));
        Assert.StartsWith("undefined:", error.Message, StringComparison.Ordinal);
        Assert.Equal((file, line, column), (error.Location!.File, error.Location.Line, error.Location.Column));
    }

    [Theory]
    [InlineData("go: downloading example.com/lib v1.0.0")]
    [InlineData("main.go:0:2: undefined: value")]
    [InlineData("Hello, world!")]
    public void Go_progress_and_ordinary_output_are_not_compiler_errors(string text) =>
        Assert.Empty(new ProblemMatcher(Go).FindErrors([text], _ => true));

    public void Dispose() => _folder.Dispose();
}
