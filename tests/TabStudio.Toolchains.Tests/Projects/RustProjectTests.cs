using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>Cargo packages, virtual workspaces, compiler diagnostics and actual binary artifacts.</summary>
public sealed class RustProjectTests : IDisposable
{
    private readonly TempDirectory _folder = new();
    private static ToolchainDefinition Rust => ToolchainCatalog.Default.Find("rust")!;

    [Fact]
    public void A_workspace_and_each_member_are_selectable_without_build_or_dependency_folders()
    {
        _folder.Write("Cargo.toml", "[workspace]\nmembers = [\"crates/hello\", \"crates/core\"]\n");
        _folder.Write("crates/hello/Cargo.toml", "[package]\nname = \"hello\"\n");
        _folder.Write("crates/core/Cargo.toml", "[package]\nname = \"core\"\n");
        _folder.Write("target/dependency/Cargo.toml");
        _folder.Write("vendor/dep/Cargo.toml");
        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);
        Assert.Equal(3, projects.Count);
        Assert.Equal(_folder.Resolve("crates/hello"), ToolchainProjectDetector.ForFile(projects, _folder.Resolve("crates/hello/src/main.rs"))!.Directory);
        Assert.Null(ToolchainProjectDetector.ForFile(projects, _folder.Resolve("main.c")));
    }

    [Fact]
    public void Cargo_builds_the_selected_manifest_and_reports_real_binary_paths()
    {
        _folder.Write("Cargo.toml");
        var plan = RunPlanner.PlanProject(new(Rust, _folder.Path, "Cargo.toml"))!;
        Assert.Equal("cargo", plan.Build!.Program);
        Assert.Equal(["build", "--message-format=json"], plan.Build.Arguments);
        Assert.Equal("never", plan.Run.Environment["CARGO_TERM_COLOR"]);
        Assert.Equal(_folder.Path, plan.Build.WorkingDirectory);
    }

    [Fact]
    public void A_Cargo_run_is_named_after_its_crate_not_its_manifest()
    {
        _folder.Write("Cargo.toml", "# A greeting\n[package]\nname = \"selftestrust\" # the crate\nversion = \"0.1.0\"\n\n[[bin]]\nname = \"other\"\n");
        Assert.Equal("selftestrust", RunPlanner.PlanProject(new(Rust, _folder.Path, "Cargo.toml"))!.EntryPoint);

        _folder.Write("Cargo.toml", "[dependencies]\nname = \"not-the-crate\"\n\n[package]\nname = 'quoted'\n");
        Assert.Equal("quoted", RunPlanner.CargoPackageName(_folder.Resolve("Cargo.toml")));

        // A virtual workspace has no crate of its own; its manifest stays the name.
        _folder.Write("Cargo.toml", "[workspace]\nmembers = [\"crates/hello\"]\n");
        Assert.Null(RunPlanner.CargoPackageName(_folder.Resolve("Cargo.toml")));
        Assert.Equal("Cargo.toml", RunPlanner.PlanProject(new(Rust, _folder.Path, "Cargo.toml"))!.EntryPoint);
    }

    [Fact]
    public void Artifact_selection_handles_custom_targets_and_excludes_test_harnesses_and_build_scripts()
    {
        const string output = """
            {"reason":"compiler-artifact","target":{"kind":["bin"],"src_path":"/app/src/main.rs"},"profile":{"test":false},"executable":"/custom/target/triple/debug/hello"}
            {"reason":"compiler-artifact","target":{"kind":["bin"],"src_path":"/app/src/main.rs"},"profile":{"test":true},"executable":"/app/test-harness"}
            {"reason":"compiler-artifact","target":{"kind":["custom-build"],"src_path":"/app/build.rs"},"profile":{"test":false},"executable":"/app/build-script"}
            {"reason":"compiler-artifact","target":{"kind":["lib"]},"executable":null}
            {"reason":"build-finished","success":true}
            Not JSON progress
            """;
        var artifact = Assert.Single(CargoBuildOutput.Executables(output));
        Assert.Equal("/custom/target/triple/debug/hello", artifact.Path);
        Assert.Equal("/app/src/main.rs", artifact.Source);
        Assert.Empty(CargoBuildOutput.Executables("not json\n{broken"));
    }

    [Fact]
    public void A_workspace_members_error_is_found_where_rustc_names_it_from_the_workspace_root()
    {
        // Cargo builds with --message-format=json, which writes each error as a compiler-message line; the
        // json-render-diagnostics format the plan used writes none, so a failing build left the Error List empty.
        _folder.Write("Cargo.toml", "[workspace]\nmembers = [\"crates/app\"]\n");
        _folder.Write("crates/app/Cargo.toml", "[package]\nname = \"app\"\n");
        var source = _folder.Write("crates/app/src/main.rs", "fn main() {\n    let x: i32 = \"a\";\n}\n");
        var manifest = _folder.Resolve("crates/app/Cargo.toml").Replace("\\", "\\\\", StringComparison.Ordinal);
        var output = "{\"reason\":\"compiler-message\",\"package_id\":\"path+file:///ws/crates/app#0.1.0\",\"manifest_path\":\"" + manifest
            + "\",\"target\":{\"kind\":[\"bin\"]},\"message\":{\"rendered\":\"error[E0308]: mismatched types\\n\",\"$message_type\":\"diagnostic\",\"level\":\"error\",\"message\":\"mismatched types\","
            + "\"spans\":[{\"file_name\":\"crates/app/src/main.rs\",\"is_primary\":true,\"line_start\":2,\"column_start\":18}]}}\n"
            + "{\"reason\":\"build-finished\",\"success\":false}\n";

        var problem = Assert.Single(new ProblemMatcher(Rust).FindErrors(CargoBuildOutput.Diagnostics(output), _ => true));

        Assert.Equal("mismatched types", problem.Message);
        Assert.Equal((source, 2, 18), (problem.Location!.File, problem.Location.Line, problem.Location.Column));
        Assert.Contains("--message-format=json", RunPlanner.PlanProject(new(Rust, _folder.Resolve("crates/app"), "Cargo.toml"))!.Build!.Arguments);
    }

    [Fact]
    public void Every_primary_Cargo_error_becomes_a_linked_problem()
    {
        const string output = """
            {"reason":"compiler-message","message":{"level":"error","message":"cannot find value nmae","rendered":"error[E0425]: cannot find value nmae\n --> src/main.rs:4:3\n","spans":[{"is_primary":true,"file_name":"src/main.rs","line_start":4,"column_start":3}]}}
            """;
        var diagnostics = CargoBuildOutput.Diagnostics(output);
        var problem = Assert.Single(new ProblemMatcher(Rust).FindErrors(diagnostics, _ => true));
        Assert.Equal("cannot find value nmae", problem.Message);
        Assert.Equal(("src/main.rs", 4, 3), (problem.Location!.File, problem.Location.Line, problem.Location.Column));
        Assert.StartsWith("error[E0425]", CargoBuildOutput.Display(output), StringComparison.Ordinal);
        Assert.Null(CargoBuildOutput.Display("{\"reason\":\"build-finished\",\"success\":true}"));
        Assert.Empty(new ProblemMatcher(Rust).FindErrors(["Compiling hello v0.1.0", "Finished dev profile", "Hello!"], _ => true));
    }

    public void Dispose() => _folder.Dispose();
}
