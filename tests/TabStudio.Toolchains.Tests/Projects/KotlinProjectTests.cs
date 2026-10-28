using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>Kotlin on the JVM: which builds are Kotlin, how F5 runs them, and the classes Java's debugger needs.</summary>
public sealed class KotlinProjectTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    private static ToolchainDefinition Kotlin => ToolchainCatalog.Default.Find("kotlin")!;

    [Fact]
    public void A_Gradle_build_with_Kotlin_sources_is_a_Kotlin_project_and_a_Java_one_stays_Java()
    {
        _folder.Write("build.gradle.kts", "plugins { kotlin(\"jvm\") version \"2.4.21\"\n application }\n");
        _folder.Write("src/main/kotlin/Main.kt", "fun main() {}\n");
        Assert.Equal("kotlin", Assert.Single(ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default)).Toolchain.Id);

        using var java = new TempDirectory();
        java.Write("build.gradle.kts", "plugins { java }\n");
        java.Write("src/main/java/App.java", "class App { public static void main(String[] a) {} }\n");
        Assert.Equal("java", Assert.Single(ToolchainProjectDetector.Detect(java.Path, ToolchainCatalog.Default)).Toolchain.Id);
    }

    [Fact]
    public void Gradle_runs_the_application_main_class_with_the_Java_debuggers_launch_shape()
    {
        _folder.Write("build.gradle.kts", "plugins { kotlin(\"jvm\") version \"2.4.21\"\n application }\napplication {\n    mainClass.set(\"demo.AppKt\")\n}\n");
        _folder.Write("src/main/kotlin/demo/App.kt", "package demo\n\nfun main() {}\n");

        var plan = RunPlanner.PlanProject(new(Kotlin, _folder.Path, "build.gradle.kts"))!;

        Assert.Equal("gradle", plan.Build!.Program);
        Assert.Equal("java", plan.Run.Program);
        Assert.Equal("-cp", plan.Run.Arguments[0]);
        Assert.Equal("demo.AppKt", plan.Run.Arguments[^1]);
        Assert.Equal("AppKt", plan.ProgramName);
    }

    [Fact]
    public void Loose_files_compile_together_with_kotlinc_and_scripts_run_with_kotlinc_script()
    {
        var main = _folder.Write("Main.kt", "fun main() { println(greet()) }\n");
        _folder.Write("Greet.kt", "fun greet() = \"hi\"\n");
        var script = _folder.Write("tool.main.kts", "println(1)\n");
        var project = new ToolchainProject(Kotlin, _folder.Path, null);

        var plan = RunPlanner.PlanFile(project, main);
        Assert.Equal("kotlinc", plan.Build!.Program);
        Assert.Contains("Greet.kt", plan.Build.Arguments);
        Assert.Contains("Main.kt", plan.Build.Arguments);
        Assert.Equal("MainKt", plan.Run.Arguments[^1]);

        var run = RunPlanner.PlanFile(project, script);
        Assert.Null(run.Build);
        Assert.Equal(["kotlinc", "-script", "tool.main.kts"], [run.Run.Program, .. run.Run.Arguments]);
    }

    [Fact]
    public void Maps_each_line_to_the_JVM_class_it_compiles_into()
    {
        const string Text = """
            package demo

            fun main() {
                println(Greeter().greet())
            }

            class Greeter {
                fun greet(): String {
                    val brace = "}"
                    return "hi"
                }

                companion object {
                    fun create() = Greeter()
                }
            }
            """;
        var classes = KotlinProject.LineClasses("/src/App.kt", Text);

        Assert.Equal("demo.AppKt", classes[4]);
        Assert.Equal("demo.Greeter", classes[9]);
        Assert.Equal("demo.Greeter", classes[10]);
        Assert.Equal("demo.Greeter$Companion", classes[14]);
    }

    [Fact]
    public void Breakpoint_stubs_keep_the_Kotlin_line_numbers_in_one_Java_file_per_class()
    {
        const string Text = "package demo\n\nfun main() {\n    val answer = 42\n}\n\nclass Greeter {\n    fun greet() = \"hi\"\n}\n";
        var stubs = _folder.Resolve("stubs");

        var sources = KotlinProject.WriteBreakpointStubs("/src/App.kt", Text, [4, 8], stubs);

        Assert.Equal(2, sources.Count);
        var app = sources.Single(s => s.StubFile.EndsWith("AppKt.java", StringComparison.Ordinal));
        Assert.Equal([4], app.Lines);
        var lines = File.ReadAllLines(app.StubFile);
        Assert.StartsWith("package demo; class AppKt {", lines[0], StringComparison.Ordinal);
        Assert.Equal("int l4 = 0;", lines[3]);
        Assert.True(File.Exists(Path.Combine(stubs, ".project")));
        Assert.Contains(sources, s => s.StubFile.EndsWith(Path.Combine("demo", "Greeter.java"), StringComparison.Ordinal) && s.Lines.SequenceEqual([8]));
    }

    [Fact]
    public void Kotlin_compiler_errors_reach_the_error_list()
    {
        var errors = new ProblemMatcher(Kotlin).FindErrors(
            ["e: file:///work/app/src/main/kotlin/Main.kt:3:5 Unresolved reference 'printn'.", "w: file:///work/app/src/main/kotlin/Main.kt:1:1 Unused"], _ => true);

        var error = Assert.Single(errors);
        Assert.Equal(("/work/app/src/main/kotlin/Main.kt", 3), (error.Location!.File, error.Location.Line));
    }

    public void Dispose() => _folder.Dispose();
}
