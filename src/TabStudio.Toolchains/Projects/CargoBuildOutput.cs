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

            foreach (var span in spans.EnumerateArray().Where(s => s.GetProperty("is_primary").GetBoolean()))
            {
                result.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{span.GetProperty("file_name").GetString()}:{span.GetProperty("line_start").GetInt32()}:{span.GetProperty("column_start").GetInt32()}: {message.GetProperty("level").GetString()}: {message.GetProperty("message").GetString()}"));
            }
        }

        return result;
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
