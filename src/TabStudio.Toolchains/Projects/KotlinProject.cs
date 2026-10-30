using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// Kotlin on the JVM: which Gradle and Maven builds compile Kotlin, the main class a Kotlin program starts with, and
/// the JVM class each line of a Kotlin file compiles into, which Java's debugger needs to set a breakpoint.
/// </summary>
public static partial class KotlinProject
{
    private static readonly string[] SourceLanguages = ["kotlin", "java"];

    /// <summary>
    /// Whether a Gradle or Maven build compiles Kotlin: its build file applies Kotlin's plugin, or it has
    /// <c>src/main/kotlin</c> or <c>src/test/kotlin</c>, here or in a module folder one level down.
    /// </summary>
    /// <param name="directory">The build's folder.</param>
    public static bool IsKotlinBuild(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (HasKotlinSources(directory) || BuildFileUsesKotlin(directory))
        {
            return true;
        }

        try
        {
            return Directory.EnumerateDirectories(directory)
                .Where(d => !Path.GetFileName(d).StartsWith('.') && Path.GetFileName(d) is not ("build" or "target" or "node_modules"))
                .Take(64)
                .Any(d => HasKotlinSources(d) || BuildFileUsesKotlin(d));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasKotlinSources(string directory) =>
        Directory.Exists(Path.Combine(directory, "src", "main", "kotlin")) || Directory.Exists(Path.Combine(directory, "src", "test", "kotlin"));

    private static bool BuildFileUsesKotlin(string directory)
    {
        foreach (var name in new[] { "build.gradle.kts", "build.gradle", "pom.xml" })
        {
            var file = Path.Combine(directory, name);
            try
            {
                if (File.Exists(file) && KotlinPlugin().IsMatch(File.ReadAllText(file)))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An unreadable build file says nothing about the language.
            }
        }

        return false;
    }

    /// <summary>
    /// The Kotlin sources of a project: <c>src/main/kotlin</c> when it has one, otherwise the <c>.kt</c> files at its
    /// top.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    public static IEnumerable<string> MainSources(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var sources = Path.Combine(directory, "src", "main", "kotlin");
        return Directory.Exists(sources)
            ? Directory.EnumerateFiles(sources, "*.kt", SearchOption.AllDirectories)
            : Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.kt") : [];
    }

    /// <summary>
    /// The main class a project runs: the application's <c>mainClass</c> in its Gradle build, else the class of its
    /// only top-level <c>fun main</c>; <see langword="null"/> when there is none or several.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    public static string? MainClass(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        foreach (var name in new[] { "build.gradle.kts", "build.gradle" })
        {
            var file = Path.Combine(directory, name);
            if (File.Exists(file) && ApplicationMainClass().Match(File.ReadAllText(file)) is { Success: true } match)
            {
                return match.Groups["class"].Value;
            }
        }

        var mains = MainSources(directory).Where(f => TopLevelMain().IsMatch(File.ReadAllText(f))).Take(2).ToList();
        return mains.Count == 1 ? ClassName(mains[0]) : null;
    }

    /// <summary>The file with a project's <c>fun main</c>, to show first and to debug.</summary>
    /// <param name="directory">The project folder.</param>
    public static string? MainSource(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var mains = MainSources(directory).Where(f => TopLevelMain().IsMatch(File.ReadAllText(f))).Take(2).ToList();
        return mains.Count == 1 ? mains[0] : null;
    }

    /// <summary>
    /// The JVM class of a Kotlin file's top-level functions: its <c>@file:JvmName</c>, or its file name with
    /// <c>Kt</c>, in its package, as <c>demo.MainKt</c> for <c>Main.kt</c>.
    /// </summary>
    /// <param name="file">The Kotlin file.</param>
    public static string ClassName(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var text = File.ReadAllText(file);
        var package = PackageOf(text);
        var name = JvmName().Match(text) is { Success: true } jvmName ? jvmName.Groups["name"].Value : FileClass(file);
        return package.Length > 0 ? package + "." + name : name;
    }

    /// <summary>The package a Kotlin file declares, or an empty string.</summary>
    /// <param name="text">The file's text.</param>
    public static string PackageOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Package().Match(text) is { Success: true } match ? match.Groups["name"].Value.Replace("`", string.Empty, StringComparison.Ordinal) : string.Empty;
    }

    private static string FileClass(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return char.ToUpperInvariant(name[0]) + name[1..].Replace('.', '_').Replace('-', '_') + "Kt";
    }

    /// <summary>
    /// The JVM class each line of a Kotlin file compiles into, for the lines with code: top-level code in the file's
    /// class, code in a class, object or interface in that type (nested ones as <c>Outer$Inner</c>, companion objects
    /// as <c>Outer$Companion</c>). Read from the file's braces, not compiled: code in a lambda or local function stays
    /// with the class around it, as Kotlin compiles them since 2.0, except suspend lambdas and inline functions from
    /// other files, whose code can be elsewhere.
    /// </summary>
    /// <param name="file">The Kotlin file.</param>
    /// <param name="text">Its text.</param>
    /// <returns>A map from each 1-based line to the binary name of its class, such as <c>demo.Greeter</c>.</returns>
    public static IReadOnlyDictionary<int, string> LineClasses(string file, string text)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(text);
        var package = PackageOf(text);
        var fileClass = JvmName().Match(text) is { Success: true } jvmName ? jvmName.Groups["name"].Value : FileClass(file);
        string Qualified(string name) => package.Length > 0 ? package + "." + name : name;

        var result = new Dictionary<int, string>();
        var stack = new List<(string? Type, int Depth)>();
        var depth = 0;
        string? pendingType = null;
        var lines = text.Split('\n');
        var inBlockComment = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = StripStringsAndComments(lines[index], ref inBlockComment);
            if (TypeDeclaration().Match(line) is { Success: true } declaration)
            {
                var name = declaration.Groups["companion"].Success
                    ? (declaration.Groups["name"].Success ? declaration.Groups["name"].Value : "Companion")
                    : declaration.Groups["name"].Value;
                var outer = stack.LastOrDefault(s => s.Type is not null).Type;
                pendingType = outer is null ? name : outer + "$" + name;
            }

            var current = stack.LastOrDefault(s => s.Type is not null).Type;
            if (line.Trim().Length > 0)
            {
                result[index + 1] = Qualified(current ?? (pendingType is not null && line.Contains('{', StringComparison.Ordinal) ? pendingType : fileClass));
            }

            foreach (var character in line)
            {
                if (character == '{')
                {
                    depth++;
                    stack.Add((pendingType, depth));
                    pendingType = null;
                }
                else if (character == '}')
                {
                    if (stack.Count > 0 && stack[^1].Depth == depth)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    depth = Math.Max(0, depth - 1);
                }
            }

            // A type declared without a body on its line, such as "data class Point(val x: Int)", never opens one.
            if (pendingType is not null && !line.Contains('{', StringComparison.Ordinal) && LineEndsDeclaration().IsMatch(line))
            {
                pendingType = null;
            }
        }

        return result;
    }

    /// <summary>Blanks out a line's string literals and comments, so their braces do not count.</summary>
    private static string StripStringsAndComments(string line, ref bool inBlockComment)
    {
        var builder = new System.Text.StringBuilder(line.Length);
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            var next = i + 1 < line.Length ? line[i + 1] : '\0';
            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                builder.Append(' ');
                continue;
            }

            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                    builder.Append(' ');
                }
                else if (c == '"')
                {
                    inString = false;
                    builder.Append('"');
                }
                else
                {
                    builder.Append(' ');
                }

                continue;
            }

            if (c == '/' && next == '/')
            {
                break;
            }

            if (c == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (c == '\'' && i + 2 < line.Length)
            {
                // A character literal, such as '{' or '\n', is not a brace of the code.
                var end = line.IndexOf('\'', next == '\\' ? i + 3 : i + 2);
                if (end > i)
                {
                    builder.Append(' ', end - i + 1);
                    i = end;
                    continue;
                }
            }

            if (c == '"')
            {
                inString = true;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// The Java source files that tell Java's debugger which class a Kotlin breakpoint's line is in: one per JVM class,
    /// with every line of the Kotlin file inside a method of that class, so the line number is the same. The debugger
    /// reads the class from the file and sets the breakpoint in the Kotlin class with that name.
    /// </summary>
    /// <param name="kotlinFile">The Kotlin file.</param>
    /// <param name="text">Its text.</param>
    /// <param name="lines">The 1-based lines with breakpoints.</param>
    /// <param name="stubFolder">Where to write the files, such as the project's <c>.tabstudio/kotlin-debug</c>.</param>
    /// <returns>Each class's file and the lines of the breakpoints in it.</returns>
    public static IReadOnlyList<(string StubFile, IReadOnlyList<int> Lines)> WriteBreakpointStubs(string kotlinFile, string text, IReadOnlyCollection<int> lines, string stubFolder)
    {
        ArgumentNullException.ThrowIfNull(kotlinFile);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(stubFolder);
        var classes = LineClasses(kotlinFile, text);
        var lineCount = text.Split('\n').Length;
        var result = new List<(string, IReadOnlyList<int>)>();
        foreach (var group in lines.GroupBy(line => classes.TryGetValue(line, out var name) ? name : ClassName(kotlinFile)))
        {
            var binaryName = group.Key;
            var dot = binaryName.LastIndexOf('.');
            var package = dot < 0 ? string.Empty : binaryName[..dot];
            var nested = (dot < 0 ? binaryName : binaryName[(dot + 1)..]).Split('$');
            var folder = Path.Combine([stubFolder, .. package.Split('.', StringSplitOptions.RemoveEmptyEntries)]);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, string.Join('_', nested) + ".java");
            File.WriteAllText(file, StubSource(package, nested, Math.Max(lineCount, group.Max()) + 2));
            result.Add((file, [.. group.Order()]));
        }

        // A folder JDT opens as a plain Eclipse project, rather than importing the Kotlin build around it.
        File.WriteAllText(Path.Combine(stubFolder, ".project"), "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<projectDescription><name>tabstudio-kotlin-debug</name></projectDescription>\n");
        return result;
    }

    /// <summary>
    /// A Java source whose lines 2 and on are statements of a method of the class, nested as the binary name says;
    /// line 1 has the package and the class headers, where a Kotlin file has its package.
    /// </summary>
    private static string StubSource(string package, string[] nested, int lineCount)
    {
        var builder = new System.Text.StringBuilder();
        if (package.Length > 0)
        {
            builder.Append("package ").Append(package).Append("; ");
        }

        for (var i = 0; i < nested.Length; i++)
        {
            builder.Append(i == 0 ? "class " : " static class ").Append(nested[i]).Append(" {");
        }

        builder.Append(" void tabstudioLines() {\n");
        for (var line = 2; line <= lineCount; line++)
        {
            builder.Append("int l").Append(line.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(" = 0;\n");
        }

        builder.Append('}').Append(new string('}', nested.Length)).Append('\n');
        return builder.ToString();
    }

    /// <summary>
    /// The source folders Java's debugger looks up a Kotlin class's file in: <c>src/main/kotlin</c>,
    /// <c>src/test/kotlin</c> and the Java ones of the project and its modules, or the project folder for loose files.
    /// </summary>
    /// <param name="directory">The project folder.</param>
    public static IReadOnlyList<string> SourcePaths(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var roots = new List<string> { directory };
        try
        {
            roots.AddRange(Directory.EnumerateDirectories(directory).Where(d => !Path.GetFileName(d).StartsWith('.')).Take(64));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Without module folders, the project's own source folders are looked in.
        }

        var result = roots.SelectMany(root => SourceLanguages.SelectMany(language => new[]
            {
                Path.Combine(root, "src", "main", language),
                Path.Combine(root, "src", "test", language),
            }))
            .Where(Directory.Exists)
            .ToList();
        return result.Count > 0 ? result : [directory];
    }

    [GeneratedRegex(@"kotlin\s*\(\s*""jvm""\s*\)|org\.jetbrains\.kotlin|kotlin-maven-plugin|id\s*\(?\s*['""]org\.jetbrains\.kotlin", RegexOptions.CultureInvariant)]
    private static partial Regex KotlinPlugin();

    [GeneratedRegex(@"mainClass(?:Name)?\s*(?:\.set\s*\(|=)\s*['""](?<class>[\w.$]+)['""]", RegexOptions.CultureInvariant)]
    private static partial Regex ApplicationMainClass();

    [GeneratedRegex(@"^\s*(?:suspend\s+)?fun\s+main\s*\(", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TopLevelMain();

    [GeneratedRegex(@"^\s*package\s+(?<name>[\w.`]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex Package();

    [GeneratedRegex(@"^\s*@file\s*:\s*JvmName\s*\(\s*""(?<name>[\w$]+)""\s*\)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex JvmName();

    [GeneratedRegex(@"^\s*(?:(?:public|private|internal|protected|open|abstract|sealed|data|inner|enum|annotation|value|final|expect|actual|fun)\s+)*(?:(?<companion>companion\s+object)(?:\s+(?<name>\w+))?|(?:class|object|interface)\s+(?<name>\w+))", RegexOptions.CultureInvariant)]
    private static partial Regex TypeDeclaration();

    [GeneratedRegex(@"\)\s*(?::\s*[\w<>.,\s()]+)?\s*$|^\s*(?:\w+\s+)*(?:class|object|interface)\s+\w+\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex LineEndsDeclaration();
}
