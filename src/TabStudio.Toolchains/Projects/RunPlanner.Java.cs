namespace TabStudio.Toolchains.Projects;

/// <summary>Maven, Gradle and standalone Java run plans with debug information and a concrete main class.</summary>
public static partial class RunPlanner
{
    private static RunPlan? PlanJavaProject(ToolchainProject project)
    {
        var source = JavaProject.MainSource(project.Directory);
        if (source is null)
        {
            return null;
        }

        var variables = Variables(project, null, null);
        if (File.Exists(Path.Combine(project.Directory, "pom.xml")))
        {
            // The program's runtime classpath, as Gradle's runtimeClasspath: test-scoped dependencies, such as
            // spring-boot-docker-compose in spring-petclinic, would change what the app does when it starts.
            var classpath = Path.Combine(project.Directory, "target", "classes") + Path.PathSeparator + Path.Combine(project.Directory, "target", "dependency", "*");
            string[] build = JavaProject.MayHaveRuntimeDependencies(Path.Combine(project.Directory, "pom.xml"))
                ? [.. MavenPackage, "org.apache.maven.plugins:maven-dependency-plugin:3.8.1:copy-dependencies", "-DincludeScope=runtime"]
                : [.. MavenPackage];
            return new(
                Spec(project, build, variables),
                Spec(project, ["java", "-cp", classpath, JavaProject.ClassName(source)], variables), "pom.xml");
        }

        if (File.Exists(Path.Combine(project.Directory, "build.gradle")) || File.Exists(Path.Combine(project.Directory, "build.gradle.kts")))
        {
            var support = Path.Combine(project.Directory, ".tabstudio");
            Directory.CreateDirectory(support);
            var init = Path.Combine(support, "java-runtime.init.gradle");
            File.WriteAllText(init, JavaProject.GradleInit);
            return new(
                Spec(project, ["gradle", "--no-daemon", "--console=plain", "--init-script", init, "tabstudioResolveRuntime"], variables),
                Spec(project, ["java", "-cp", Path.Combine(project.Directory, "build", "classes", "java", "main"), JavaProject.ClassName(source)], variables), "build.gradle");
        }

        return PlanJavaFile(project, source);
    }

    /// <summary>
    /// Maven's package build. A download Maven Central answers with 429 or 503, as it does to busy networks, is retried
    /// with a back-off five times rather than the resolver's three, before the build gives up.
    /// </summary>
    private static readonly string[] MavenPackage =
        ["mvn", "-B", "-Dstyle.color=never", "-Daether.connector.http.retryHandler.count=5", "-DskipTests", "package"];

    private static RunPlan PlanJavaFile(ToolchainProject project, string file)
    {
        var classes = Path.Combine(project.Directory, ".tabstudio", "java", "classes");
        Directory.CreateDirectory(classes);
        var relative = Path.GetRelativePath(project.Directory, file).Replace('\\', '/');
        var variables = Variables(project, relative, null);
        return new(
            Spec(project, ["javac", "-g", "-encoding", "UTF-8", "-d", classes, relative], variables),
            Spec(project, ["java", "-cp", classes, JavaProject.ClassName(file)], variables), relative);
    }
}
