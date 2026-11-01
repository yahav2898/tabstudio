using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

public sealed class MobileProjectTests
{
    [Theory]
    [InlineData("settings.gradle", "plugins { id 'com.android.application' version '8.7.3' apply false }")]
    [InlineData("settings.gradle.kts", "plugins { id(\"com.android.application\") }")]
    public void Android_plugin_in_an_application_module_identifies_the_Gradle_root(string settings, string plugin)
    {
        using var temp = new TempDirectory();
        temp.Write(settings, "include(\":app\")");
        temp.Write("app/build.gradle.kts", plugin);
        Assert.True(GradleAndroidProject.IsProject(temp.Path));
        var project = Assert.Single(ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default));
        Assert.Equal("android", project.Toolchain.Id);
        Assert.Equal(temp.Path, project.Directory);
    }

    [Theory]
    [InlineData("// id(\"com.android.application\")\nplugins { java }")]
    [InlineData("/* id(\"com.android.application\") */\nplugins { id(\"com.android.library\") }")]
    [InlineData("plugins { id(\"org.jetbrains.kotlin.jvm\") }")]
    public void A_comment_or_JVM_or_library_project_is_not_an_Android_app(string plugin)
    {
        using var temp = new TempDirectory();
        temp.Write("settings.gradle.kts", "include(\":app\")");
        temp.Write("app/build.gradle.kts", plugin);
        Assert.False(GradleAndroidProject.IsProject(temp.Path));
    }

    [Fact]
    public void Version_catalog_aliases_and_nested_roots_are_supported()
    {
        using var temp = new TempDirectory();
        temp.Write("mobile/settings.gradle.kts", "include(\":app\")");
        temp.Write("mobile/gradle/libs.versions.toml", "[plugins]\nandroid-application = { id = \"com.android.application\", version = \"8.7.3\" }");
        temp.Write("mobile/app/build.gradle.kts", "plugins { alias(libs.plugins.android.application) }");
        temp.Write("server/settings.gradle.kts", "rootProject.name=\"server\"");
        var projects = ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default);
        var project = Assert.Single(projects, p => p.Toolchain.Id == "android");
        Assert.Equal(temp.Resolve("mobile"), project.Directory);
        Assert.Equal(temp.Resolve("server"), Assert.Single(projects, p => p.Toolchain.Id == "java").Directory);
        Assert.Equal(2, projects.Count);
    }

    [Theory]
    [InlineData("settings.gradle", "build.gradle")]
    [InlineData("settings.gradle.kts", "build.gradle.kts")]
    public void Android_owns_its_Gradle_modules_and_Java_source_folders(string settings, string build)
    {
        using var temp = new TempDirectory();
        temp.Write(settings, "include(\":app\")");
        temp.Write("app/" + build, "plugins { id(\"com.android.application\") }");
        temp.Write("app/src/Main.java", "class Main {}\n");

        var project = Assert.Single(ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default));

        Assert.Equal("android", project.Toolchain.Id);
        Assert.Equal(temp.Path, project.Directory);
    }

    [Fact]
    public void Independent_Maven_and_Gradle_JVM_roots_inside_Android_remain_discoverable()
    {
        using var temp = new TempDirectory();
        temp.Write("settings.gradle.kts", "include(\":app\")");
        temp.Write("app/build.gradle.kts", "plugins { id(\"com.android.application\") }");
        temp.Write("app/src/Main.java", "class Main {}\n");
        temp.Write("tools/maven/pom.xml", "<project><artifactId>tool</artifactId></project>");
        temp.Write("tools/maven/src/Main.java", "class Main {}\n");
        temp.Write("tools/jvm/settings.gradle.kts", "rootProject.name=\"jvm-tool\"");
        temp.Write("tools/jvm/build.gradle.kts", "plugins { application }");
        temp.Write("tools/jvm/src/Main.java", "class Main {}\n");

        var projects = ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default);

        Assert.Equal(
            [("android", temp.Path), ("java", temp.Resolve("tools/jvm")), ("java", temp.Resolve("tools/maven"))],
            projects.Select(p => (p.Toolchain.Id, p.Directory)).OrderBy(p => p.Directory, StringComparer.Ordinal));
    }

    [Fact]
    public void Capacitor_source_folders_do_not_create_incidental_Java_projects()
    {
        using var temp = new TempDirectory();
        temp.Write("package.json", """{ "name": "web-app" }""");
        temp.Write("capacitor.config.json", """{ "appId": "example.app" }""");
        temp.Write("src/Main.java", "class Main {}\n");
        temp.Write("tools/pom.xml", "<project><artifactId>tool</artifactId></project>");

        var projects = ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default);

        Assert.Equal(
            [("node", temp.Path), ("java", temp.Resolve("tools"))],
            projects.Select(p => (p.Toolchain.Id, p.Directory)).OrderBy(p => p.Directory, StringComparer.Ordinal));
    }

    [Fact]
    public void Uses_the_wrapper_on_Android_with_a_native_AAPT_override()
    {
        using var temp = new TempDirectory();
        temp.Write("gradlew", "#!/bin/sh\n");
        var plan = GradleAndroidProject.Build(temp.Path, linuxEnvironment: true);
        Assert.Equal("sh", plan.Program);
        Assert.Equal("gradlew", plan.Arguments[0]);
        Assert.Contains("-Pandroid.aapt2FromMavenOverride=/opt/tabstudio/android/sdk/build-tools/35.0.0/aapt2", plan.Arguments);
        Assert.Equal("/usr/lib/jvm/java-17-openjdk", plan.Environment["JAVA_HOME"]);
    }

    [Fact]
    public void The_native_template_is_the_same_project_the_detector_and_CI_build()
    {
        using var temp = new TempDirectory();
        MobileTemplates.CreateAndroid(temp.Path, "MyApp");
        Assert.True(GradleAndroidProject.IsProject(temp.Path));
        Assert.Contains("com.android.application", File.ReadAllText(temp.Resolve("build.gradle.kts")), StringComparison.Ordinal);
        Assert.True(File.Exists(temp.Resolve("app/src/main/java/com/example/appmyapp/MainActivity.kt")));
        using var relay = new AndroidLogcatRelay(_ => { });
        var init = relay.PrepareBuild(temp.Path);
        Assert.Contains("getByName('debug')", File.ReadAllText(init), StringComparison.Ordinal);
        Assert.DoesNotContain("LogcatProvider", File.ReadAllText(temp.Resolve("app/src/main/AndroidManifest.xml")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Class", "com.example.appclass")]
    [InlineData("My.App_2", "com.example.appmyapp2")]
    public void Template_identifiers_work_in_Java_Kotlin_and_Apple_bundle_IDs(string name, string expected)
    {
        using var temp = new TempDirectory();
        MobileTemplates.CreateCapacitor(temp.Path, name);
        using var config = System.Text.Json.JsonDocument.Parse(File.ReadAllText(temp.Resolve("capacitor.config.json")));
        Assert.Equal(expected, config.RootElement.GetProperty("appId").GetString());
        MobileTemplates.CreateAndroid(temp.Resolve("native"), name);
        Assert.Contains("applicationId = \"" + expected + "\"", File.ReadAllText(temp.Resolve("native/app/build.gradle.kts")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_Capacitor_template_locks_every_package_it_installs()
    {
        // Without a lock file each new project took the newest rollup, and one published minutes earlier answered 404.
        using var temp = new TempDirectory();
        MobileTemplates.CreateCapacitor(temp.Path, "My.App");
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(temp.Resolve("package.json")));
        using var lockFile = System.Text.Json.JsonDocument.Parse(File.ReadAllText(temp.Resolve("package-lock.json")));
        var root = lockFile.RootElement.GetProperty("packages").GetProperty("");
        Assert.Equal("my-app", lockFile.RootElement.GetProperty("name").GetString());
        Assert.Equal("my-app", root.GetProperty("name").GetString());
        foreach (var section in new[] { "dependencies", "devDependencies" })
        {
            var wanted = manifest.RootElement.GetProperty(section).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
            var locked = root.GetProperty(section).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
            Assert.Equal(wanted, locked);
        }

        var packages = lockFile.RootElement.GetProperty("packages").EnumerateObject().Where(p => p.Name.Length > 0).ToList();
        Assert.Contains(packages, p => p.Name == "node_modules/rollup");
        Assert.All(packages, p =>
        {
            Assert.StartsWith("https://registry.npmjs.org/", p.Value.GetProperty("resolved").GetString(), StringComparison.Ordinal);
            Assert.StartsWith("sha512-", p.Value.GetProperty("integrity").GetString(), StringComparison.Ordinal);
        });
        Assert.Equal("npm ci", RunPlanner.DependenciesOf(new ToolchainProject(ToolchainCatalog.Default.Find("node")!, temp.Path, null))!.Name);
    }

    [Fact]
    public async Task Logcat_accepts_only_this_builds_authenticated_loopback_stream()
    {
        using var temp = new TempDirectory();
        var received = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var relay = new AndroidLogcatRelay(line => { received.Enqueue(line); arrived.TrySetResult(); });
        relay.PrepareBuild(temp.Path);
        var source = File.ReadAllText(temp.Resolve("build/tabstudio-logcat/java/dev/tabstudio/LogcatProvider.java"));
        var portMatch = System.Text.RegularExpressions.Regex.Match(source, "InetSocketAddress\\(\"127.0.0.1\", (\\d+)\\)");
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(source, "out.println\\(\"([A-F0-9]{64})\"\\)");
        Assert.True(portMatch.Success && tokenMatch.Success);
        var port = int.Parse(portMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        using (var wrong = new System.Net.Sockets.TcpClient())
        {
            await wrong.ConnectAsync(System.Net.IPAddress.Loopback, port);
            using var writer = new StreamWriter(wrong.GetStream()) { AutoFlush = true };
            await writer.WriteLineAsync("not-the-build-token");
            await writer.WriteLineAsync("unrelated app's log");
        }

        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(System.Net.IPAddress.Loopback, port);
        using var authenticated = new StreamWriter(client.GetStream()) { AutoFlush = true };
        await authenticated.WriteLineAsync(tokenMatch.Groups[1].Value);
        await authenticated.WriteLineAsync("the built app's log");
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["the built app's log"], received);
        Assert.Contains("--pid=", source, StringComparison.Ordinal);
    }
}
