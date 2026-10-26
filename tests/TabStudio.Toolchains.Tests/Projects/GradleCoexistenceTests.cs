using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

public sealed class GradleCoexistenceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Android_SDK_and_Java_keep_separate_Gradle_commands_in_either_catalog_order(bool androidFirst)
    {
        var original = ToolchainCatalog.Default;
        var android = original.Find("android")!;
        var java = original.Find("java")!;
        var catalog = original with { Toolchains = androidFirst ? [android, java] : [java, android] };
        var sdk = catalog.Find("android")!;
        Assert.DoesNotContain(sdk.Setup, step => step.File?.Path == "/usr/local/bin/gradle");
        var launcher = Assert.Single(sdk.Setup, step => step.File?.Path == GradleAndroidProject.LinuxGradleCommand).File!;
        Assert.Contains("JAVA_HOME=/usr/lib/jvm/java-17-openjdk", launcher.Content, StringComparison.Ordinal);
        Assert.Contains("/opt/tabstudio/android/gradle/gradle-8.9/bin/gradle", launcher.Content, StringComparison.Ordinal);
        Assert.Equal(["gradle"], sdk.Host!.Commands);
        Assert.All(sdk.Run!.Entries, entry => Assert.Equal(GradleAndroidProject.LinuxGradleCommand, entry.Command[0]));

        using var temp = new TempDirectory();
        temp.Write("build.gradle", "plugins { id 'java' }");
        temp.Write("src/main/java/App.java", "public class App { public static void main(String[] args) { System.out.println(\"JVM\"); } }");
        var jvm = RunPlanner.PlanProject(new ToolchainProject(catalog.Find("java")!, temp.Path, "build.gradle"))!;
        Assert.Equal("gradle", jvm.Build!.Program);
        Assert.Contains("openjdk21-jdk", java.Packages);
        Assert.DoesNotContain(jvm.Build.Environment, entry => entry.Key == "JAVA_HOME" && entry.Value == "/usr/lib/jvm/java-17-openjdk");
        Assert.DoesNotContain(java.Setup, step => step.File?.Path == GradleAndroidProject.LinuxGradleCommand);
    }

    [Fact]
    public void Android_uses_its_pinned_Gradle_on_the_tablet_and_the_hosts_Gradle_on_a_computer()
    {
        using var temp = new TempDirectory();
        var tablet = GradleAndroidProject.Build(temp.Path, linuxEnvironment: true);
        Assert.Equal(GradleAndroidProject.LinuxGradleCommand, tablet.Program);
        Assert.Equal("/usr/lib/jvm/java-17-openjdk", tablet.Environment["JAVA_HOME"]);
        Assert.Equal("gradle", GradleAndroidProject.Build(temp.Path, linuxEnvironment: false).Program);
        temp.Write("gradlew", "#!/bin/sh\n");
        var wrapper = GradleAndroidProject.Build(temp.Path, linuxEnvironment: true);
        Assert.Equal("sh", wrapper.Program);
        Assert.Equal("gradlew", wrapper.Arguments[0]);
        Assert.Equal("/usr/lib/jvm/java-17-openjdk", wrapper.Environment["JAVA_HOME"]);
    }
}
