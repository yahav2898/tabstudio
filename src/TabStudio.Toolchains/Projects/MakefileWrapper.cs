using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// Recognizes a Makefile that only wraps another build tool, such as fd's <c>cargo build --release</c> target or a
/// <c>make test</c> that runs <c>npm test</c>. Such a Makefile is a shortcut for the repository's real project, not
/// a C or C++ program, so it never becomes a C/C++ startup project.
/// </summary>
public static partial class MakefileWrapper
{
    /// <summary>Build tools a Makefile wraps, by the command its recipes start.</summary>
    private static readonly string[] WrappedTools =
    [
        "cargo", "rustup", "npm", "npx", "yarn", "pnpm", "bun", "node", "go", "dotnet", "mvn", "mvnw", "gradle", "gradlew",
        "python", "python3", "pip", "pip3", "uv", "poetry", "flutter", "dart", "deno",
        // Containers and API test runners, as gothinkster/aspnetcore-realworld-example-app's Makefile drives them.
        "docker", "docker-compose", "podman", "hurl",
        // Documentation and site generators: Sphinx's generated Makefile runs $(SPHINXBUILD), which is sphinx-build.
        "sphinx-build", "sphinx-autobuild", "mkdocs", "jekyll", "hugo", "doxygen", "asciidoctor", "pandoc",
    ];

    /// <summary>What a recipe or variable uses to compile or link C and C++.</summary>
    private static readonly string[] NativeTools = ["cc", "gcc", "g++", "c++", "clang", "clang++", "cmake", "ld", "ar", "nasm", "yasm", "meson", "ninja", "xc8-cc", "xc32-gcc"];

    /// <summary>
    /// The build tool a Makefile wraps, such as <c>cargo</c>, or <see langword="null"/> when it builds C or C++
    /// itself, or when it cannot be read.
    /// </summary>
    /// <param name="makefile">The Makefile.</param>
    public static string? WrappedTool(string makefile)
    {
        ArgumentNullException.ThrowIfNull(makefile);
        string text;
        try
        {
            var info = new FileInfo(makefile);
            if (!info.Exists || info.Length > 512 * 1024)
            {
                return null;
            }

            text = File.ReadAllText(makefile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return WrappedToolOf(text);
    }

    /// <summary>The build tool a Makefile's text wraps, as <see cref="WrappedTool"/>.</summary>
    /// <param name="text">The Makefile's text.</param>
    public static string? WrappedToolOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string? wrapped = null;
        var lines = JoinContinuations(text).ToList();
        var variables = Variables(lines);
        foreach (var raw in lines)
        {
            var line = raw.Split('#', 2)[0];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Compiling objects, or sources named as prerequisites, is a native build whatever else it runs.
            if (ObjectRule().IsMatch(line) || NativeSource().IsMatch(line) || NativeVariable().IsMatch(line))
            {
                return null;
            }

            var recipe = line.StartsWith('\t');
            foreach (var command in Commands(recipe ? Expand(line, variables) : AssignedValue(line)))
            {
                if (NativeTools.Contains(command, StringComparer.Ordinal))
                {
                    return null;
                }

                if (recipe && wrapped is null && WrappedTools.Contains(command, StringComparer.Ordinal))
                {
                    wrapped = command is "mvnw" ? "mvn" : command is "gradlew" ? "gradle" : command;
                }
            }
        }

        return wrapped;
    }

    /// <summary>Lines with their backslash continuations joined.</summary>
    private static IEnumerable<string> JoinContinuations(string text)
    {
        var current = string.Empty;
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.EndsWith('\\'))
            {
                current += line[..^1] + " ";
                continue;
            }

            yield return current + line;
            current = string.Empty;
        }

        if (current.Length > 0)
        {
            yield return current;
        }
    }

    /// <summary>
    /// The Makefile's variables whose value starts a command, such as <c>SPHINXBUILD ?= sphinx-build</c>. A value
    /// that assigns environment variables (<c>WITH_JWT = JWT=x</c>) is left alone: it is a prefix, not the command.
    /// </summary>
    private static Dictionary<string, string> Variables(IEnumerable<string> lines)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (!line.StartsWith('\t') && Assignment().Match(line.Split('#', 2)[0]) is { Success: true } match)
            {
                var value = match.Groups["value"].Value.Trim();
                if (value.Length > 0 && !value.Contains('=', StringComparison.Ordinal) && !value.Contains('$', StringComparison.Ordinal))
                {
                    variables.TryAdd(match.Groups["name"].Value, value);
                }
            }
        }

        return variables;
    }

    /// <summary>A recipe line with the whole-word variables it starts commands with, <c>$(SPHINXBUILD)</c>, replaced by their values.</summary>
    private static string Expand(string line, Dictionary<string, string> variables) =>
        variables.Count == 0 ? line : MacroWord().Replace(line, m => variables.TryGetValue(m.Groups["name"].Value, out var value) ? value : m.Value);

    /// <summary>The value of a variable assignment, such as <c>BUILD = cargo build</c>; empty for other lines.</summary>
    private static string AssignedValue(string line) =>
        Assignment().Match(line) is { Success: true } match ? match.Groups["value"].Value : string.Empty;

    /// <summary>The commands a shell line starts: its first word and the first word after each separator.</summary>
    private static IEnumerable<string> Commands(string line)
    {
        foreach (var part in Separators().Split(line))
        {
            var words = part.Trim().TrimStart('@', '-', '+').Trim().Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            var index = 0;
            // Environment assignments, wrappers and make macros before the command: VAR=x env time $(SETUP) cargo build.
            while (index < words.Length && (words[index].Contains('=', StringComparison.Ordinal) || words[index] is "env" or "time" or "exec" or "command" or "sudo" || IsMacro(words[index])))
            {
                index++;
            }

            if (index < words.Length)
            {
                yield return Path.GetFileName(words[index].Trim('(', ')', '"', '\''));
            }
        }
    }

    /// <summary>A whole word that expands a make variable, such as <c>$(WITH_LOCAL_JWT)</c>, which runs before the command.</summary>
    private static bool IsMacro(string word) =>
        (word.StartsWith("$(", StringComparison.Ordinal) && word.EndsWith(')')) || (word.StartsWith("${", StringComparison.Ordinal) && word.EndsWith('}'));

    [GeneratedRegex(@"&&|\|\||;|\||\$\(shell\s", RegexOptions.CultureInvariant)]
    private static partial Regex Separators();

    [GeneratedRegex(@"^(?<name>[A-Za-z_][A-Za-z0-9_.-]*)\s*(\?|:|::|\+|!)?=\s*(?<value>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Assignment();

    /// <summary>A make variable expanded as a whole word: <c>$(NAME)</c> or <c>${NAME}</c>.</summary>
    [GeneratedRegex(@"(?<=^|[\s@+\-;&|])\$[({](?<name>[A-Za-z_][A-Za-z0-9_.-]*)[)}](?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex MacroWord();

    /// <summary>A rule that makes object files, such as <c>%.o: %.c</c>, <c>.c.o:</c> or <c>main.o: main.c</c>.</summary>
    [GeneratedRegex(@"^[^\t=]*\.o\s*:|^\.(c|cc|cpp|cxx|s|S)\.o\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex ObjectRule();

    /// <summary>A C or C++ source or header named outside a recipe, as a prerequisite or in a variable.</summary>
    [GeneratedRegex(@"^[^\t].*[\w*%)](\.c|\.cc|\.cpp|\.cxx|\.h|\.hpp)(\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex NativeSource();

    /// <summary>The variables a C or C++ build sets or uses: <c>CC</c>, <c>CXX</c>, <c>CFLAGS</c>, <c>LDFLAGS</c>.</summary>
    [GeneratedRegex(@"(^|[^\w])(\$[({])?(CC|CXX|CPP|CFLAGS|CXXFLAGS|CPPFLAGS|LDFLAGS|LDLIBS)([)}]|\s*[?:+]?=)", RegexOptions.CultureInvariant)]
    private static partial Regex NativeVariable();
}
