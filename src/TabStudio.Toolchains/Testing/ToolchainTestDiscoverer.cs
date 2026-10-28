using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Testing;

/// <summary>
/// Finds a toolchain project's tests in its source before they run, as Test Explorer lists C# tests: PHPUnit's
/// <c>test*</c> methods and <c>#[Test]</c>, JUnit's <c>@Test</c> functions in Kotlin and Java, and XCTest's
/// <c>test*</c> methods and Swift Testing's <c>@Test</c> functions. Their names are those the runners report, so a
/// result replaces the test found in source.
/// </summary>
public static partial class ToolchainTestDiscoverer
{
    private const int MaxFiles = 2000;
    private const long MaxFileSize = 512 * 1024;

    /// <summary>The tests in a project's source.</summary>
    /// <param name="directory">The project folder.</param>
    /// <param name="framework">Its runner.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    public static IReadOnlyList<ToolchainTestCase> Discover(string directory, ToolchainTestFramework framework, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var tests = new List<ToolchainTestCase>();
        foreach (var file in SourceFiles(directory, framework))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text;
            try
            {
                if (new FileInfo(file).Length > MaxFileSize)
                {
                    continue;
                }

                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            tests.AddRange(framework switch
            {
                ToolchainTestFramework.PhpUnit or ToolchainTestFramework.Pest => Php(file, text),
                ToolchainTestFramework.SwiftPM => Swift(directory, file, text),
                _ => Jvm(file, text),
            });
        }

        return tests;
    }

    /// <summary>PHPUnit tests in a PHP file: public <c>test*</c> methods, and methods marked <c>#[Test]</c> or <c>@test</c>.</summary>
    internal static IEnumerable<ToolchainTestCase> Php(string file, string text)
    {
        var lines = LineStarts(text);
        var ns = PhpNamespace().Match(text) is { Success: true } n ? n.Groups["name"].Value.Replace('\\', '.') + "." : string.Empty;
        var classes = PhpClass().Matches(text).Select(m => (Name: m.Groups["name"].Value, m.Index)).ToList();
        foreach (Match method in PhpMethod().Matches(text))
        {
            var name = method.Groups["name"].Value;
            var marked = method.Groups["attributes"].Value.Contains("Test", StringComparison.Ordinal) || method.Groups["attributes"].Value.Contains("@test", StringComparison.Ordinal);
            if (!(name.StartsWith("test", StringComparison.Ordinal) || marked) || classes.LastOrDefault(c => c.Index < method.Index) is not { Name: not null } owner)
            {
                continue;
            }

            yield return new ToolchainTestCase(ns + owner.Name, name, file, LineOf(lines, method.Groups["name"].Index), LineOf(lines, owner.Index));
        }
    }

    /// <summary>JUnit tests in a Kotlin or Java file: functions marked <c>@Test</c>, <c>@ParameterizedTest</c> and the like.</summary>
    internal static IEnumerable<ToolchainTestCase> Jvm(string file, string text)
    {
        var lines = LineStarts(text);
        var package = JvmPackage().Match(text) is { Success: true } p ? p.Groups["name"].Value + "." : string.Empty;
        var classes = JvmClass().Matches(text).Select(m => (Name: m.Groups["name"].Value, m.Index)).ToList();
        foreach (Match test in JvmTest().Matches(text))
        {
            var name = test.Groups["kotlin"].Success ? test.Groups["kotlin"].Value : test.Groups["java"].Value;
            if (name.Length == 0 || classes.LastOrDefault(c => c.Index < test.Index) is not { Name: not null } owner)
            {
                continue;
            }

            var at = test.Groups["kotlin"].Success ? test.Groups["kotlin"].Index : test.Groups["java"].Index;
            yield return new ToolchainTestCase(package + owner.Name, name, file, LineOf(lines, at), LineOf(lines, owner.Index));
        }
    }

    /// <summary>
    /// Swift tests in a file of a test target (<c>Tests/&lt;Target&gt;</c>): XCTest's <c>test*</c> methods of an
    /// <c>XCTestCase</c>, reported as <c>Target.Class</c>, and Swift Testing's <c>@Test</c> functions, in their suite
    /// type or, at the top of the file, in the target.
    /// </summary>
    internal static IEnumerable<ToolchainTestCase> Swift(string directory, string file, string text)
    {
        var relative = Path.GetRelativePath(Path.Combine(directory, "Tests"), file).Split(Path.DirectorySeparatorChar);
        var module = relative.Length > 1 ? relative[0] : Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar));
        var lines = LineStarts(text);
        var types = SwiftType().Matches(text).Select(m => (Name: m.Groups["name"].Value, m.Index, IsTestCase: m.Groups["base"].Value.Contains("XCTestCase", StringComparison.Ordinal), End: BlockEnd(text, m.Index))).ToList();
        foreach (Match function in SwiftFunction().Matches(text))
        {
            var name = function.Groups["name"].Value;
            var owner = types.LastOrDefault(t => t.Index < function.Index && function.Index < t.End);
            var isTest = function.Groups["attribute"].Success || (owner.IsTestCase && name.StartsWith("test", StringComparison.Ordinal));
            if (!isTest)
            {
                continue;
            }

            var className = owner.Name is null ? module : module + "." + owner.Name;
            yield return new ToolchainTestCase(className, name, file, LineOf(lines, function.Groups["name"].Index), owner.Name is null ? LineOf(lines, function.Index) : LineOf(lines, owner.Index));
        }
    }

    private static IEnumerable<string> SourceFiles(string directory, ToolchainTestFramework framework)
    {
        (string[] Roots, string Pattern) sources = framework switch
        {
            ToolchainTestFramework.PhpUnit or ToolchainTestFramework.Pest => (["tests", "test", "Tests"], "*.php"),
            ToolchainTestFramework.SwiftPM => (["Tests"], "*.swift"),
            _ => (JvmTestRoots(directory), "*.*"),
        };
        var count = 0;
        foreach (var root in sources.Roots.Select(r => Path.Combine(directory, r)).Where(Directory.Exists).Distinct(StringComparer.Ordinal))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, sources.Pattern, new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 12 })
                    .Where(f => framework is not (ToolchainTestFramework.Gradle or ToolchainTestFramework.Maven) || f.EndsWith(".kt", StringComparison.Ordinal) || f.EndsWith(".java", StringComparison.Ordinal))
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}vendor{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    .Order(StringComparer.Ordinal).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (++count > MaxFiles)
                {
                    yield break;
                }

                yield return file;
            }
        }
    }

    private static string[] JvmTestRoots(string directory)
    {
        var roots = new List<string> { Path.Combine("src", "test") };
        try
        {
            roots.AddRange(Directory.EnumerateDirectories(directory)
                .Select(Path.GetFileName).OfType<string>()
                .Where(d => !d.StartsWith('.') && d is not ("build" or "target" or "node_modules" or "src"))
                .Take(64)
                .Select(d => Path.Combine(d, "src", "test")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only the project's own tests.
        }

        return [.. roots];
    }

    private static int BlockEnd(string text, int start)
    {
        var open = text.IndexOf('{', start);
        if (open < 0)
        {
            return text.Length;
        }

        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}' && --depth == 0)
            {
                return i;
            }
        }

        return text.Length;
    }

    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    private static int LineOf(List<int> starts, int index)
    {
        var found = starts.BinarySearch(index);
        return (found >= 0 ? found : ~found - 1) + 1;
    }

    [GeneratedRegex(@"^\s*namespace\s+(?<name>[\w\\]+)\s*[;{]", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PhpNamespace();

    [GeneratedRegex(@"^\s*(?:(?:abstract|final|readonly)\s+)*class\s+(?<name>\w+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PhpClass();

    [GeneratedRegex(@"(?<attributes>(?:#\[[^\]]*\]\s*|/\*\*(?:(?!\*/)[\s\S])*\*/\s*)*)public\s+(?:static\s+)?function\s+(?<name>\w+)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex PhpMethod();

    [GeneratedRegex(@"^\s*package\s+(?<name>[\w.]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex JvmPackage();

    [GeneratedRegex(@"^\s*(?:(?:public|internal|private|protected|open|abstract|final|static|inner|data)\s+)*class\s+(?<name>\w+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex JvmClass();

    [GeneratedRegex(@"@(?:Test|ParameterizedTest|RepeatedTest|TestFactory|TestTemplate)\b(?:\([^)]*\))?(?:\s*@[\w.]+(?:\([^)]*\))?)*\s*(?:(?:public|internal|private|protected|open|override|suspend|final|static)\s+)*(?:fun\s+(?:`(?<kotlin>[^`]+)`|(?<kotlin>\w+))|(?:void|\w+(?:<[^>]*>)?)\s+(?<java>\w+))\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex JvmTest();

    [GeneratedRegex(@"^\s*(?:@\w+(?:\([^)]*\))?\s*)*(?:(?:public|internal|private|fileprivate|open|final)\s+)*(?:class|struct|actor|extension|enum)\s+(?<name>\w+)\s*(?::\s*(?<base>[^{]*))?\{", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SwiftType();

    [GeneratedRegex(@"(?<attribute>@Test\b(?:\((?:[^()]|\([^()]*\))*\))?\s*)?(?:(?:public|internal|private|fileprivate|open|final|override|static|@MainActor)\s+)*func\s+(?<name>\w+)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex SwiftFunction();
}
