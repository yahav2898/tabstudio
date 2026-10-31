using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Testing;

/// <summary>
/// Finds a toolchain project's tests in its source before they run, as Test Explorer lists C# tests: PHPUnit's
/// <c>test*</c> methods and <c>#[Test]</c>, JUnit's <c>@Test</c> functions in Kotlin and Java, and XCTest's
/// <c>test*</c> methods and Swift Testing's <c>@Test</c> functions, pytest's <c>test*</c> functions and methods, Go's
/// <c>Test*</c> functions and Rust's <c>#[test]</c> functions.
/// Their names are those the runners report, so a result replaces the test found in source.
/// </summary>
public static partial class ToolchainTestDiscoverer
{
    private const int MaxFiles = 2000;
    private const long MaxFileSize = 512 * 1024;

    private static readonly HashSet<string> PythonSkippedFolders = new(StringComparer.Ordinal)
    {
        "node_modules", "venv", "env", "site-packages", "__pycache__", "build", "dist",
    };

    /// <summary>The tests in a project's source.</summary>
    /// <param name="directory">The project folder.</param>
    /// <param name="framework">Its runner.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    public static IReadOnlyList<ToolchainTestCase> Discover(string directory, ToolchainTestFramework framework, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var tests = new List<ToolchainTestCase>();
        switch (framework)
        {
            case ToolchainTestFramework.DartTest or ToolchainTestFramework.FlutterTest:
                // A Dart test's name joins its groups' descriptions, which are expressions: its results name them.
                return tests;
            case ToolchainTestFramework.GoTest:
                var module = ToolchainTestOutput.GoModule(directory);
                foreach (var file in FilesUnder(directory, "*_test.go", ["vendor", "testdata", "node_modules"]))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tests.AddRange(Go(directory, module, file, ReadSmall(file)));
                }

                return tests;
            case ToolchainTestFramework.Cargo:
                foreach (var file in FilesUnder(directory, "*.rs", ["target", "vendor", "node_modules", "benches", "examples"]))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tests.AddRange(Rust(directory, file, ReadSmall(file)));
                }

                return tests;
        }

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
                ToolchainTestFramework.Pytest => Python(directory, file, text),
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
        var classes = PhpClass().Matches(text).Select(m => (Name: m.Groups["name"].Value, m.Groups["name"].Index)).ToList();
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
        var classes = JvmClass().Matches(text).Select(m => (Name: m.Groups["name"].Value, m.Groups["name"].Index)).ToList();
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
        var types = SwiftType().Matches(text).Select(m => (Name: m.Groups["name"].Value, m.Groups["name"].Index, IsTestCase: m.Groups["base"].Value.Contains("XCTestCase", StringComparison.Ordinal), End: BlockEnd(text, m.Index))).ToList();
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

    /// <summary>
    /// pytest's tests in a test file: <c>test*</c> functions, and <c>test*</c> methods of <c>Test*</c> classes and of
    /// <c>unittest.TestCase</c> classes, reported in the class named by the file's path with dots, as pytest does.
    /// </summary>
    internal static IEnumerable<ToolchainTestCase> Python(string directory, string file, string text)
    {
        var module = Path.ChangeExtension(Path.GetRelativePath(directory, file), null).Replace(Path.DirectorySeparatorChar, '.');
        (string Name, int Line, bool IsTest)? owner = null;
        int? bodyIndent = null;
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0 || line[0] is '#' or '@' or ')' || char.IsWhiteSpace(line[0]))
            {
                // A method is at the indentation of the class's first statement; a function nested in one is not a test.
                var indent = line.Length - line.TrimStart().Length;
                if (owner is { } type && line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
                {
                    bodyIndent ??= indent;
                    if (type.IsTest && indent == bodyIndent && PythonMethod().Match(line) is { Success: true } method)
                    {
                        yield return new ToolchainTestCase(module + "." + type.Name, method.Groups["name"].Value, file, i + 1, type.Line);
                    }
                }

                continue;
            }

            // A statement at the top of the file ends the class before it.
            owner = null;
            bodyIndent = null;
            if (PythonClass().Match(line) is { Success: true } declared)
            {
                var name = declared.Groups["name"].Value;
                owner = (name, i + 1, name.StartsWith("Test", StringComparison.Ordinal) || declared.Groups["bases"].Value.Contains("TestCase", StringComparison.Ordinal));
            }
            else if (PythonFunction().Match(line) is { Success: true } function)
            {
                yield return new ToolchainTestCase(module, function.Groups["name"].Value, file, i + 1, 1);
            }
        }
    }

    /// <summary>
    /// Go tests in a <c>_test.go</c> file: <c>Test*</c> functions taking a <c>*testing.T</c> and <c>Fuzz*</c> functions
    /// taking a <c>*testing.F</c>, in the class of their package (see <see cref="ToolchainTestOutput.GoPackageClass"/>).
    /// </summary>
    internal static IEnumerable<ToolchainTestCase> Go(string directory, string? module, string file, string text)
    {
        var relative = Path.GetRelativePath(directory, Path.GetDirectoryName(file)!).Replace(Path.DirectorySeparatorChar, '/');
        var package = module is null ? relative : relative == "." ? module : module + "/" + relative;
        var className = ToolchainTestOutput.GoPackageClass(package, module);
        var lines = LineStarts(text);
        foreach (Match function in GoTestFunction().Matches(text))
        {
            yield return new ToolchainTestCase(className, function.Groups["name"].Value, file, LineOf(lines, function.Groups["name"].Index), 1);
        }
    }

    /// <summary>
    /// Rust tests in a file: functions marked <c>#[test]</c> (or a test attribute such as <c>#[tokio::test]</c>), in
    /// the class <c>cargo test</c> reports them in: the target (the crate for <c>src</c>, the file for
    /// <c>tests/*.rs</c>) and the module path, which follows the file's path and the <c>mod</c> blocks around the test.
    /// </summary>
    internal static IEnumerable<ToolchainTestCase> Rust(string directory, string file, string text)
    {
        var package = RustPackageDirectory(directory, file);
        var relative = Path.GetRelativePath(package, file).Replace(Path.DirectorySeparatorChar, '/');
        string target;
        List<string> modules;
        if (relative.StartsWith("tests/", StringComparison.Ordinal))
        {
            var parts = relative["tests/".Length..].Split('/');
            target = Path.GetFileNameWithoutExtension(parts[0]);
            modules = parts.Length > 1 && parts[^1] is not "main.rs" ? [.. parts[1..^1], .. RustModule(parts[^1])] : [];
        }
        else if (relative.StartsWith("src/", StringComparison.Ordinal))
        {
            target = RustCrateName(package, relative);
            var parts = relative["src/".Length..].Split('/');
            modules = parts[0] == "bin" || (parts.Length == 1 && parts[0] is "lib.rs" or "main.rs") ? [] : [.. parts[..^1], .. RustModule(parts[^1])];
        }
        else
        {
            yield break;
        }

        var lines = text.Split('\n');
        var scopes = new Stack<(string Name, int Depth)>();
        var depth = 0;
        var marked = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (RustTestAttribute().IsMatch(line))
            {
                marked = true;
            }
            else if (marked && RustFunction().Match(line) is { Success: true } function)
            {
                var className = string.Join('.', new[] { target }.Concat(modules).Concat(scopes.Reverse().Select(s => s.Name)));
                yield return new ToolchainTestCase(className, function.Groups["name"].Value, file, i + 1, 1);
                marked = false;
            }
            else if (marked && line.Length > 0 && !line.StartsWith("#[", StringComparison.Ordinal))
            {
                marked = false;
            }

            if (RustModuleBlock().Match(line) is { Success: true } block)
            {
                scopes.Push((block.Groups["name"].Value, depth));
            }

            foreach (var c in line)
            {
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    while (scopes.Count > 0 && scopes.Peek().Depth >= depth)
                    {
                        scopes.Pop();
                    }
                }
            }
        }
    }

    private static string[] RustModule(string file) =>
        file is "mod.rs" ? [] : [Path.GetFileNameWithoutExtension(file)];

    /// <summary>The folder of the Cargo package a file is in: the closest one above it with a Cargo.toml.</summary>
    private static string RustPackageDirectory(string directory, string file)
    {
        for (var folder = Path.GetDirectoryName(file); folder is not null && folder.Length >= directory.Length; folder = Path.GetDirectoryName(folder))
        {
            if (File.Exists(Path.Combine(folder, "Cargo.toml")))
            {
                return folder;
            }
        }

        return directory;
    }

    /// <summary>The crate a file in <c>src</c> belongs to, as its test binary is named: the package's name with underscores.</summary>
    private static string RustCrateName(string package, string relative)
    {
        var manifest = ReadSmall(Path.Combine(package, "Cargo.toml"));
        var section = relative.StartsWith("src/bin/", StringComparison.Ordinal) ? null : RustLibName().Match(manifest) is { Success: true } lib ? lib.Groups["name"].Value : null;
        var name = relative.StartsWith("src/bin/", StringComparison.Ordinal)
            ? Path.GetFileNameWithoutExtension(relative["src/bin/".Length..].Split('/')[0])
            : section ?? (RustPackageName().Match(manifest) is { Success: true } package2 ? package2.Groups["name"].Value : Path.GetFileName(package));
        return name.Replace('-', '_');
    }

    private static List<string> FilesUnder(string directory, string pattern, string[] skipped)
    {
        var found = new List<string>();
        var pending = new Stack<(string Folder, int Depth)>([(directory, 0)]);
        while (pending.TryPop(out var next) && found.Count < MaxFiles)
        {
            try
            {
                found.AddRange(Directory.EnumerateFiles(next.Folder, pattern));
                if (next.Depth < 10)
                {
                    foreach (var child in Directory.EnumerateDirectories(next.Folder))
                    {
                        var name = Path.GetFileName(child);
                        if (!name.StartsWith('.') && !skipped.Contains(name, StringComparer.Ordinal))
                        {
                            pending.Push((child, next.Depth + 1));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder removed or unreadable while the tests are looked for.
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private static string ReadSmall(string file)
    {
        try
        {
            return new FileInfo(file).Length <= MaxFileSize ? File.ReadAllText(file) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> SourceFiles(string directory, ToolchainTestFramework framework)
    {
        if (framework == ToolchainTestFramework.Pytest)
        {
            foreach (var file in PythonTestFiles(directory))
            {
                yield return file;
            }

            yield break;
        }

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

    /// <summary>pytest's test files: <c>test_*.py</c> and <c>*_test.py</c>, outside virtual environments and packages.</summary>
    private static List<string> PythonTestFiles(string directory)
    {
        var found = new List<string>();
        var pending = new Stack<(string Folder, int Depth)>([(directory, 0)]);
        while (pending.TryPop(out var next) && found.Count < MaxFiles)
        {
            try
            {
                found.AddRange(Directory.EnumerateFiles(next.Folder, "*.py")
                    .Where(f => Path.GetFileName(f) is var name && (name.StartsWith("test_", StringComparison.Ordinal) || name.EndsWith("_test.py", StringComparison.Ordinal))));
                if (next.Depth < 8)
                {
                    foreach (var child in Directory.EnumerateDirectories(next.Folder).Order(StringComparer.Ordinal).Reverse())
                    {
                        var name = Path.GetFileName(child);
                        if (!name.StartsWith('.') && !PythonSkippedFolders.Contains(name) && !File.Exists(Path.Combine(child, "pyvenv.cfg")))
                        {
                            pending.Push((child, next.Depth + 1));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder removed or unreadable while the tests are looked for.
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private static string[] JvmTestRoots(string directory)
    {
        var roots = new List<string> { Path.Combine("src", "test") };
        try
        {
            roots.AddRange(Directory.EnumerateDirectories(directory)
                .Select(Path.GetFileName).OfType<string>()
                .Where(d => !d.StartsWith('.') && d is not ("build" or "target" or "node_modules" or "src"))
                .Order(StringComparer.Ordinal)
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

    [GeneratedRegex(@"^func\s+(?<name>(?:Test|Fuzz)(?:[A-Z0-9_]\w*)?)\s*\(\s*\w+\s+\*testing\.[TF]\s*\)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GoTestFunction();

    [GeneratedRegex(@"^#\[(?:test|(?:\w+::)+test(?:\(.*\))?|rstest|test_case(?:\(.*\))?)\]", RegexOptions.CultureInvariant)]
    private static partial Regex RustTestAttribute();

    [GeneratedRegex(@"^(?:pub(?:\([^)]*\))?\s+)?(?:async\s+)?(?:unsafe\s+)?fn\s+(?<name>\w+)", RegexOptions.CultureInvariant)]
    private static partial Regex RustFunction();

    [GeneratedRegex(@"^(?:pub(?:\([^)]*\))?\s+)?mod\s+(?<name>\w+)\s*\{", RegexOptions.CultureInvariant)]
    private static partial Regex RustModuleBlock();

    [GeneratedRegex(@"\[lib\][^\[]*?^\s*name\s*=\s*""(?<name>[^""]+)""", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex RustLibName();

    [GeneratedRegex(@"\[package\][^\[]*?^\s*name\s*=\s*""(?<name>[^""]+)""", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex RustPackageName();

    [GeneratedRegex(@"^[ \t]*namespace\s+(?<name>[\w\\]+)\s*[;{]", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PhpNamespace();

    [GeneratedRegex(@"^[ \t]*(?:(?:abstract|final|readonly)\s+)*class\s+(?<name>\w+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PhpClass();

    [GeneratedRegex(@"(?<attributes>(?:#\[[^\]]*\]\s*|/\*\*(?:(?!\*/)[\s\S])*\*/\s*)*)public\s+(?:static\s+)?function\s+(?<name>\w+)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex PhpMethod();

    [GeneratedRegex(@"^class\s+(?<name>\w+)\s*(?:\((?<bases>[^)]*)\))?\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex PythonClass();

    [GeneratedRegex(@"^(?:async\s+)?def\s+(?<name>test\w*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex PythonFunction();

    [GeneratedRegex(@"^[ \t]+(?:async\s+)?def\s+(?<name>test\w*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex PythonMethod();

    [GeneratedRegex(@"^[ \t]*package\s+(?<name>[\w.]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex JvmPackage();

    [GeneratedRegex(@"^[ \t]*(?:(?:public|internal|private|protected|open|abstract|final|static|inner|data)\s+)*class\s+(?<name>\w+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex JvmClass();

    [GeneratedRegex(@"@(?:Test|ParameterizedTest|RepeatedTest|TestFactory|TestTemplate)\b(?:\([^)]*\))?(?:\s*@[\w.]+(?:\([^)]*\))?)*\s*(?:(?:public|internal|private|protected|open|override|suspend|final|static)\s+)*(?:fun\s+(?:`(?<kotlin>[^`]+)`|(?<kotlin>\w+))|(?:void|\w+(?:<[^>]*>)?)\s+(?<java>\w+))\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex JvmTest();

    [GeneratedRegex(@"^[ \t]*(?:@\w+(?:\([^)]*\))?\s*)*(?:(?:public|internal|private|fileprivate|open|final)\s+)*(?:class|struct|actor|extension|enum)\s+(?<name>\w+)\s*(?::\s*(?<base>[^{]*))?\{", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SwiftType();

    [GeneratedRegex(@"(?<attribute>@Test\b(?:\((?:[^()]|\([^()]*\))*\))?\s*)?(?:(?:public|internal|private|fileprivate|open|final|override|static|@MainActor)\s+)*func\s+(?<name>\w+)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex SwiftFunction();
}
