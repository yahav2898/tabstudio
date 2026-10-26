using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Projects;

/// <summary>Java main classes and build metadata shared by the planner and the start screen.</summary>
public static partial class JavaProject
{
    /// <summary>The sole main source in a Java project, or no automatic selection when there are several.</summary>
    /// <param name="directory">The project root.</param>
    public static string? MainSource(string directory)
    {
        var sources = Path.Combine(directory, "src", "main", "java");
        var files = Directory.Exists(sources) ? Directory.EnumerateFiles(sources, "*.java", SearchOption.AllDirectories) : Directory.EnumerateFiles(directory, "*.java");
        var mains = files.Where(f => MainPattern().IsMatch(File.ReadAllText(f))).Take(2).ToList();
        return mains.Count == 1 ? mains[0] : null;
    }

    /// <summary>A source file's fully qualified class name, including its package.</summary>
    /// <param name="file">The Java source.</param>
    public static string ClassName(string file)
    {
        var text = File.ReadAllText(file);
        var package = PackagePattern().Match(text);
        return (package.Success ? package.Groups[1].Value + "." : string.Empty) + Path.GetFileNameWithoutExtension(file);
    }

    /// <summary>The Gradle init script that records the application's complete runtime classpath.</summary>
    public const string GradleInit = """
        allprojects { p ->
            p.tasks.register('tabstudioResolveRuntime') {
                dependsOn p.tasks.matching { it.name == 'classes' }
                doLast {
                    def sets = p.extensions.findByName('sourceSets')
                    if (sets != null && sets.findByName('main') != null) {
                        def file = new File(p.projectDir, '.tabstudio/java-classpath.txt')
                        file.parentFile.mkdirs()
                        file.text = sets.main.runtimeClasspath.asPath
                    }
                }
            }
        }
        """;

    [GeneratedRegex(@"\bstatic\s+void\s+main\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex MainPattern();

    [GeneratedRegex(@"^\s*package\s+([\w.]+)\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PackagePattern();
}
