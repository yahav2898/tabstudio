namespace TabStudio.Toolchains.Projects;

/// <summary>
/// Kotlin run plans on the JVM: Gradle and Maven builds and loose <c>.kt</c> files compiled by <c>kotlinc</c>, all
/// run as <c>java -cp &lt;classpath&gt; &lt;MainClass&gt;</c>, the shape Java's debugger launches; <c>.kts</c> scripts with
/// <c>kotlinc -script</c>.
/// </summary>
public static partial class RunPlanner
{
    private static RunPlan? PlanKotlinProject(ToolchainProject project)
    {
        var directory = project.Directory;
        var variables = Variables(project, null, null);
        if (File.Exists(Path.Combine(directory, "pom.xml")))
        {
            if (KotlinProject.MainClass(directory) is not { } mavenMain)
            {
                return null;
            }

            var classpath = Path.Combine(directory, "target", "classes") + Path.PathSeparator + Path.Combine(directory, "target", "dependency", "*");
            return new RunPlan(
                Spec(project, [.. MavenPackage, "org.apache.maven.plugins:maven-dependency-plugin:3.8.1:copy-dependencies", "-DincludeScope=runtime"], variables),
                Spec(project, ["java", "-cp", classpath, mavenMain], variables), "pom.xml")
            { ProgramName = SimpleName(mavenMain) };
        }

        if (File.Exists(Path.Combine(directory, "build.gradle.kts")) || File.Exists(Path.Combine(directory, "build.gradle")))
        {
            if (KotlinProject.MainClass(directory) is not { } gradleMain)
            {
                return null;
            }

            var support = Path.Combine(directory, ".tabstudio");
            Directory.CreateDirectory(support);
            var init = Path.Combine(support, "java-runtime.init.gradle");
            File.WriteAllText(init, JavaProject.GradleInit);
            // The classpath is the one the init script records; until then, the Kotlin and resource output folders.
            var classpath = Path.Combine(directory, "build", "classes", "kotlin", "main") + Path.PathSeparator + Path.Combine(directory, "build", "resources", "main");
            return new RunPlan(
                Spec(project, ["gradle", "--no-daemon", "--console=plain", "--init-script", init, "tabstudioResolveRuntime"], variables),
                Spec(project, ["java", "-cp", classpath, gradleMain], variables), "build.gradle")
            { ProgramName = SimpleName(gradleMain) };
        }

        return KotlinProject.MainSource(directory) is { } source ? PlanKotlinFile(project, source) : null;
    }

    private static RunPlan? PlanKotlinFile(ToolchainProject project, string file)
    {
        var relative = Path.GetRelativePath(project.Directory, file).Replace('\\', '/');
        var variables = Variables(project, relative, null);
        if (file.EndsWith(".kts", StringComparison.OrdinalIgnoreCase))
        {
            // Gradle's own build scripts are not programs.
            return RunPlan.IsBuildFile(Path.GetFileName(file))
                ? null
                : new RunPlan(null, Spec(project, ["kotlinc", "-script", relative], variables), relative);
        }

        var output = Path.Combine(project.Directory, ".tabstudio", "kotlin");
        Directory.CreateDirectory(output);
        var jar = Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".jar");
        var mainClass = KotlinProject.ClassName(file);
        // Loose files beside each other are one program, as a folder of .java files is to javac.
        var sources = Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.kt")
            .Select(f => Path.GetRelativePath(project.Directory, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        return new RunPlan(
            Spec(project, ["kotlinc", .. sources, "-include-runtime", "-d", jar], variables),
            Spec(project, ["java", "-cp", jar, mainClass], variables), relative);
    }

    private static string SimpleName(string className) => className[(className.LastIndexOf('.') + 1)..];
}
