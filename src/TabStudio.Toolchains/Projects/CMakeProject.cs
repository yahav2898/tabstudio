using System.Text.Json;

namespace TabStudio.Toolchains.Projects;

/// <summary>An executable target reported by CMake's file API.</summary>
/// <param name="Path">The built binary's absolute path.</param>
/// <param name="Sources">Its source files.</param>
/// <param name="Name">The target's name, such as <c>cJSON_test</c>.</param>
/// <param name="SourceDirectory">
/// The absolute source directory whose <c>CMakeLists.txt</c> defines it, or <see langword="null"/> when CMake did not
/// say.
/// </param>
public sealed record CMakeExecutable(string Path, IReadOnlyList<string> Sources, string Name, string? SourceDirectory);

/// <summary>Asks CMake for its actual executable targets and artifact paths, including multi-configuration builds.</summary>
public static class CMakeProject
{
    /// <summary>Creates the file API query before configuration.</summary>
    /// <param name="buildDirectory">The build tree.</param>
    public static void PrepareQuery(string buildDirectory)
    {
        var query = System.IO.Path.Combine(buildDirectory, ".cmake", "api", "v1", "query");
        Directory.CreateDirectory(query);
        File.WriteAllText(System.IO.Path.Combine(query, "codemodel-v2"), string.Empty);
    }

    /// <summary>The executable targets in the most recent codemodel reply, one per target.</summary>
    /// <param name="buildDirectory">The configured build tree.</param>
    /// <param name="sourceDirectory">The source tree.</param>
    public static IReadOnlyList<CMakeExecutable> Executables(string buildDirectory, string sourceDirectory)
    {
        var reply = System.IO.Path.Combine(buildDirectory, ".cmake", "api", "v1", "reply");
        if (!Directory.Exists(reply) || Directory.EnumerateFiles(reply, "index-*.json").Order(StringComparer.Ordinal).LastOrDefault() is not { } index)
        {
            return [];
        }

        using var root = JsonDocument.Parse(File.ReadAllText(index));
        var modelFile = root.RootElement.GetProperty("reply").GetProperty("codemodel-v2").GetProperty("jsonFile").GetString()!;
        using var model = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(reply, modelFile)));
        var configurations = model.RootElement.GetProperty("configurations").EnumerateArray().ToList();
        var configuration = configurations.FirstOrDefault(c => c.GetProperty("name").GetString() == "Debug");
        if (configuration.ValueKind == JsonValueKind.Undefined)
        {
            configuration = configurations[0];
        }

        var directories = configuration.TryGetProperty("directories", out var folders) && folders.ValueKind == JsonValueKind.Array
            ? folders.EnumerateArray().ToList()
            : [];
        var result = new List<CMakeExecutable>();
        foreach (var target in configuration.GetProperty("targets").EnumerateArray())
        {
            using var definition = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(reply, target.GetProperty("jsonFile").GetString()!)));
            var data = definition.RootElement;
            if (data.GetProperty("type").GetString() != "EXECUTABLE" || !data.TryGetProperty("artifacts", out var artifacts) || artifacts.GetArrayLength() == 0)
            {
                continue;
            }

            var sources = data.TryGetProperty("sources", out var files)
                ? files.EnumerateArray().Select(f => System.IO.Path.GetFullPath(System.IO.Path.Combine(sourceDirectory, f.GetProperty("path").GetString()!))).ToList()
                : [];

            // One program per target: its other artifacts are files such as its .pdb.
            var outputs = artifacts.EnumerateArray().Select(a => a.GetProperty("path").GetString()!).ToList();
            var program = outputs.FirstOrDefault(p => !p.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) ?? outputs[0];
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(buildDirectory, program));
            var name = Text(data, "name") ?? Text(target, "name") ?? System.IO.Path.GetFileNameWithoutExtension(path);

            // The folder of the CMakeLists.txt that defines it, relative to the top-level source directory ("." for it).
            var folder = data.TryGetProperty("paths", out var paths) ? Text(paths, "source") : null;
            if (folder is null && target.TryGetProperty("directoryIndex", out var directoryIndex) && directoryIndex.ValueKind == JsonValueKind.Number
                && directoryIndex.TryGetInt32(out var position)
                && position >= 0 && position < directories.Count)
            {
                folder = Text(directories[position], "source");
            }

            result.Add(new(path, sources, name, folder is null ? null : System.IO.Path.GetFullPath(System.IO.Path.Combine(sourceDirectory, folder))));
        }

        return result;
    }

    /// <summary>
    /// The executable F5 runs, chosen as a person would: the only one; else the first that a launch configuration's
    /// <c>program</c> names, by target, file name or path; else the one whose sources include the file being edited;
    /// else the only one the top-level <c>CMakeLists.txt</c> defines, such as cJSON's <c>cJSON_test</c> beside the
    /// tests and fuzzers its subfolders define. <see langword="null"/> when a person would have to choose.
    /// </summary>
    /// <param name="targets">The build's executables, from <see cref="Executables"/>.</param>
    /// <param name="sourceDirectory">The top-level source directory.</param>
    /// <param name="activeFile">The file being edited, if any.</param>
    /// <param name="launchPrograms">The <c>program</c> of each launch configuration, in order, with its variables replaced.</param>
    /// <param name="launchDirectory">The folder a relative <c>program</c> is in: the workspace's.</param>
    public static CMakeExecutable? Choose(IReadOnlyList<CMakeExecutable> targets, string sourceDirectory, string? activeFile, IReadOnlyList<string> launchPrograms, string launchDirectory)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(sourceDirectory);
        ArgumentNullException.ThrowIfNull(launchPrograms);
        ArgumentNullException.ThrowIfNull(launchDirectory);
        if (targets.Count == 1)
        {
            return targets[0];
        }

        foreach (var program in launchPrograms)
        {
            if (targets.FirstOrDefault(t => string.Equals(program, t.Name, StringComparison.Ordinal) || NativePrograms.Names(program, t.Path, launchDirectory)) is { } named)
            {
                return named;
            }
        }

        if (activeFile is not null && OnlyOne(targets.Where(t => t.Sources.Any(s => NativePrograms.SamePath(s, activeFile)))) is { } edited)
        {
            return edited;
        }

        return OnlyOne(targets.Where(t => t.SourceDirectory is { } folder && NativePrograms.SamePath(folder, sourceDirectory)));
    }

    private static CMakeExecutable? OnlyOne(IEnumerable<CMakeExecutable> targets)
    {
        var found = targets.Take(2).ToList();
        return found.Count == 1 ? found[0] : null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
