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
        var project = Assert.Single(ToolchainProjectDetector.Detect(temp.Path, ToolchainCatalog.Default));
        Assert.Equal(temp.Resolve("mobile"), project.Directory);
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
        Assert.True(File.Exists(temp.Resolve("app/src/main/java/com/example/tabstudioapp/MainActivity.kt")));
        using var relay = new AndroidLogcatRelay(_ => { });
        var init = relay.PrepareBuild(temp.Path);
        Assert.Contains("getByName('debug')", File.ReadAllText(init), StringComparison.Ordinal);
        Assert.DoesNotContain("LogcatProvider", File.ReadAllText(temp.Resolve("app/src/main/AndroidManifest.xml")), StringComparison.Ordinal);
    }
}
