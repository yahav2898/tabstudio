using System.Text.Json;

namespace TabStudio.Toolchains.Projects;

/// <summary>An executable target reported by CMake's file API.</summary>
/// <param name="Path">The built binary's absolute path.</param>
/// <param name="Sources">Its source files.</param>
public sealed record CMakeExecutable(string Path, IReadOnlyList<string> Sources);

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

    /// <summary>The executable targets in the most recent codemodel reply.</summary>
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

        var result = new List<CMakeExecutable>();
        foreach (var target in configuration.GetProperty("targets").EnumerateArray())
        {
            using var definition = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(reply, target.GetProperty("jsonFile").GetString()!)));
            var data = definition.RootElement;
            if (data.GetProperty("type").GetString() != "EXECUTABLE" || !data.TryGetProperty("artifacts", out var artifacts))
            {
                continue;
            }

            var sources = data.TryGetProperty("sources", out var files)
                ? files.EnumerateArray().Select(f => System.IO.Path.GetFullPath(System.IO.Path.Combine(sourceDirectory, f.GetProperty("path").GetString()!))).ToList()
                : [];
            foreach (var artifact in artifacts.EnumerateArray())
            {
                result.Add(new(System.IO.Path.GetFullPath(System.IO.Path.Combine(buildDirectory, artifact.GetProperty("path").GetString()!)), sources));
            }
        }

        return result;
    }
}
