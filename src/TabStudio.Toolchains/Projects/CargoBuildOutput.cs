using System.Globalization;
using System.Text.Json;

namespace TabStudio.Toolchains.Projects;

/// <summary>An executable Cargo actually built, including its real target directory and target triple.</summary>
/// <param name="Path">The executable's absolute path.</param>
/// <param name="Source">Its entry source file.</param>
public sealed record CargoExecutable(string Path, string Source);

/// <summary>Reads Cargo's stable JSON build protocol, rather than guessing executables from manifest text.</summary>
public static class CargoBuildOutput
{
    /// <summary>The binary artifacts, excluding test harnesses, libraries, build scripts and duplicate reports.</summary>
    /// <param name="output">Cargo's JSON output.</param>
    public static IReadOnlyList<CargoExecutable> Executables(string output)
    {
        var result = new List<CargoExecutable>();
        foreach (var line in output.Split('\n'))
        {
            using var json = Read(line);
            if (json?.RootElement is not { } root || !root.TryGetProperty("reason", out var reason) || reason.GetString() != "compiler-artifact"
                || !root.TryGetProperty("executable", out var executable) || executable.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("target", out var target) || !target.TryGetProperty("kind", out var kinds)
                || !kinds.EnumerateArray().Any(k => k.GetString() == "bin")
                || (root.TryGetProperty("profile", out var profile) && profile.TryGetProperty("test", out var test) && test.ValueKind == JsonValueKind.True))
            {
                continue;
            }

            result.Add(new(executable.GetString()!, target.GetProperty("src_path").GetString()!));
        }

        return [.. result.Distinct()];
    }

    /// <summary>A rendered compiler message, ordinary progress text, or nothing for JSON bookkeeping.</summary>
    /// <param name="line">One output line.</param>
    public static string? Display(string line)
    {
        using var json = Read(line);
        if (json is null)
        {
            return line;
        }

        return json.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("rendered", out var rendered) ? rendered.GetString() : null;
    }

    /// <summary>Primary source diagnostics as location lines for the problem matcher, with every error preserved.</summary>
    /// <param name="output">Cargo's JSON output.</param>
    public static IReadOnlyList<string> Diagnostics(string output)
    {
        var result = new List<string>();
        foreach (var line in output.Split('\n'))
        {
            using var json = Read(line);
            if (json?.RootElement is not { } root || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("spans", out var spans))
            {
                continue;
            }

            var manifest = root.TryGetProperty("manifest_path", out var path) && path.ValueKind == JsonValueKind.String ? path.GetString() : null;
            foreach (var span in spans.EnumerateArray().Where(s => s.GetProperty("is_primary").GetBoolean()))
            {
                var file = Resolve(span.GetProperty("file_name").GetString()!, manifest);
                result.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{file}:{span.GetProperty("line_start").GetInt32()}:{span.GetProperty("column_start").GetInt32()}: {message.GetProperty("level").GetString()}: {message.GetProperty("message").GetString()}"));
            }
        }

        return result;
    }

    /// <summary>
    /// A diagnostic's file. rustc names it relative to the workspace's root, which for a member such as crates/app is
    /// a folder above the package's own: the folder above the manifest where the file is.
    /// </summary>
    private static string Resolve(string file, string? manifest)
    {
        if (Path.IsPathRooted(file) || manifest is null)
        {
            return file;
        }

        for (var folder = Path.GetDirectoryName(manifest); !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
        {
            var candidate = Path.Combine(folder, file);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return file;
    }

    private static JsonDocument? Read(string line)
    {
        try
        {
            return line.StartsWith('{') ? JsonDocument.Parse(line) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
