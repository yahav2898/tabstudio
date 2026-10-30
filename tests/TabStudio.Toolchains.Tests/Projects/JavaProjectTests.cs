using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>Maven, Gradle and standalone Java projects, build plans and compiler diagnostics.</summary>
public sealed class JavaProjectTests : IDisposable
{
    private readonly TempDirectory _folder = new();
    private static ToolchainDefinition Java => ToolchainCatalog.Default.Find("java")!;
    private const string Main = "package com.example;\npublic class App { public static void main(String[] args) {} }\n";

    [Theory]
    [InlineData("pom.xml")]
    [InlineData("build.gradle")]
    [InlineData("build.gradle.kts")]
    [InlineData("settings.gradle.kts")]
    [InlineData("App.java")]
    public void Detects_Java_project_layouts(string marker)
    {
        _folder.Write(marker);
        _folder.Write("target/generated/App.java");
        _folder.Write(".gradle/cache/App.java");
        Assert.Equal("java", Assert.Single(ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default)).Toolchain.Id);
    }

    [Fact]
    public void Maven_builds_and_runs_the_qualified_main_with_runtime_dependencies()
    {
        _folder.Write("pom.xml");
        _folder.Write("src/main/java/com/example/App.java", Main);
        var plan = RunPlanner.PlanProject(new(Java, _folder.Path, "pom.xml"))!;
        Assert.Equal("mvn", plan.Build!.Program);
        Assert.Contains("package", plan.Build.Arguments);
        Assert.Contains("org.apache.maven.plugins:maven-dependency-plugin:3.8.1:copy-dependencies", plan.Build.Arguments);

        // Only the runtime classpath: a test-scoped dependency such as spring-boot-docker-compose must not run.
        Assert.Contains("-DincludeScope=runtime", plan.Build.Arguments);
        Assert.Equal("java", plan.Run.Program);
        Assert.False(plan.Run.Environment.ContainsKey("JAVA_HOME"));
        Assert.Equal("com.example.App", plan.Run.Arguments[^1]);
        Assert.Contains("target/dependency/*", plan.Run.Arguments[1], StringComparison.Ordinal);
        Assert.Equal(["openjdk21-jdk", "jdtls"], Java.Packages);
        Assert.Contains(Java.ProjectTools, t => t.Manifest == "pom.xml" && t.Packages.SequenceEqual(["maven"]));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("<dependencies><dependency><groupId>org.junit.jupiter</groupId><artifactId>junit-jupiter</artifactId><version>5.11.0</version><scope>test</scope></dependency></dependencies>", false)]
    [InlineData("<dependencies><dependency><groupId>com.google.code.gson</groupId><artifactId>gson</artifactId><version>2.11.0</version></dependency></dependencies>", true)]
    [InlineData("<dependencies><dependency><groupId>org.postgresql</groupId><artifactId>postgresql</artifactId><version>42.7.4</version><scope> runtime </scope></dependency></dependencies>", true)]
    [InlineData("<parent><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-parent</artifactId><version>3.4.0</version></parent>", true)]
    public void Maven_copies_runtime_libraries_only_when_the_project_may_have_some(string content, bool copies)
    {
        // The dependency plugin is dozens of downloads before a first build, and one more request a busy network
        // can refuse (Maven Central's 429), for a project that has nothing to copy.
        _folder.Write("pom.xml", $"<project xmlns=\"http://maven.apache.org/POM/4.0.0\"><modelVersion>4.0.0</modelVersion><groupId>com.example</groupId><artifactId>app</artifactId><version>1.0.0</version>{content}</project>");
        _folder.Write("src/main/java/com/example/App.java", Main);

        var plan = RunPlanner.PlanProject(new(Java, _folder.Path, "pom.xml"))!;

        Assert.Equal(copies, plan.Build!.Arguments.Any(a => a.EndsWith(":copy-dependencies", StringComparison.Ordinal)));
        Assert.Equal(copies, plan.Build.Arguments.Contains("-DincludeScope=runtime"));
        Assert.Contains("package", plan.Build.Arguments);
        Assert.Contains("-Daether.connector.http.retryHandler.count=5", plan.Build.Arguments);
        Assert.Contains("target/dependency/*", plan.Run.Arguments[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build.gradle")]
    [InlineData("build.gradle.kts")]
    public void Gradle_resolves_the_runtime_classpath_without_requiring_an_application_plugin(string marker)
    {
        _folder.Write(marker);
        _folder.Write("src/main/java/com/example/App.java", Main);
        var plan = RunPlanner.PlanProject(new(Java, _folder.Path, marker))!;
        Assert.Equal("gradle", plan.Build!.Program);
        Assert.Contains("tabstudioResolveRuntime", plan.Build.Arguments);
        Assert.True(File.Exists(_folder.Resolve(".tabstudio/java-runtime.init.gradle")));
        Assert.Equal("com.example.App", plan.Run.Arguments[^1]);
    }

    [Fact]
    public void A_standalone_file_is_compiled_with_locals_and_runs_in_its_package()
    {
        var file = _folder.Write("App.java", Main);
        var plan = RunPlanner.PlanFile(new(Java, _folder.Path, null), file);
        Assert.Equal("javac", plan.Build!.Program);
        Assert.Contains("-g", plan.Build.Arguments);
        Assert.Equal("com.example.App", plan.Run.Arguments[^1]);
        Assert.Equal(_folder.Resolve(".tabstudio/java/classes"), plan.Run.Arguments[1]);
    }

    [Fact]
    public void Ambiguous_main_classes_and_non_Java_files_are_not_automatically_selected()
    {
        _folder.Write("App.java", Main);
        _folder.Write("Second.java", Main);
        Assert.Null(JavaProject.MainSource(_folder.Path));
        Assert.Null(RunPlanner.PlanProject(new(Java, _folder.Path, null)));
        Assert.Throws<ArgumentException>(() => RunPlanner.PlanFile(new(Java, _folder.Path, null), _folder.Resolve("main.py")));
    }

    [Theory]
    [InlineData("App.java:5: error: cannot find symbol", "App.java", 5, null)]
    [InlineData("[ERROR] /work/my app/App.java:[5,13] cannot find symbol", "/work/my app/App.java", 5, 13)]
    [InlineData(@"[ERROR] C:\my app\App.java:[5,13] cannot find symbol", @"C:\my app\App.java", 5, 13)]
    [InlineData("[ERROR] C:/my app/App.java:[5,13] cannot find symbol", "C:/my app/App.java", 5, 13)]
    [InlineData("[ERROR] src/main/java/App.java:[5,13] cannot find symbol", "src/main/java/App.java", 5, 13)]
    public void Javac_and_Maven_errors_keep_their_file_and_line(string output, string file, int line, int? column)
    {
        var error = Assert.Single(new ProblemMatcher(Java).FindErrors([output], _ => true));
        Assert.Contains("cannot find symbol", error.Message, StringComparison.Ordinal);
        Assert.Equal((file, line, column), (error.Location!.File, error.Location.Line, error.Location.Column));
    }

    [Theory]
    [InlineData("[INFO] BUILD SUCCESS")]
    [InlineData("App.java:5: warning: unchecked conversion")]
    [InlineData("[WARNING] /app/App.java:[5,3] unchecked conversion")]
    [InlineData(@"[WARNING] C:\app\App.java:[5,3] unchecked conversion")]
    public void Warnings_and_progress_are_not_compiler_errors(string output) =>
        Assert.Empty(new ProblemMatcher(Java).FindErrors([output], _ => true));

    public void Dispose() => _folder.Dispose();
}
