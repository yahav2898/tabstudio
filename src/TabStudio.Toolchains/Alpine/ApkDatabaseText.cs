using System.Globalization;

namespace TabStudio.Toolchains.Alpine;

/// <summary>
/// Reads the text format that Alpine's index (<c>APKINDEX</c>) and installed database (<c>lib/apk/db/installed</c>)
/// share: one package per paragraph, one <c>K:value</c> field per line.
/// </summary>
/// <remarks>
/// The installed database adds lines for the package's folders and files (<c>F:</c>, <c>R:</c>, <c>a:</c>,
/// <c>M:</c>, <c>Z:</c>), which this reader skips.
/// </remarks>
public static class ApkDatabaseText
{
    /// <summary>Reads every package.</summary>
    /// <param name="reader">The text.</param>
    /// <param name="repository">The repository the packages come from, if any.</param>
    public static List<ApkPackage> Read(TextReader reader, string? repository = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var result = new List<ApkPackage>();
        var fields = new Dictionary<char, string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                Flush(fields, repository, result);
                continue;
            }

            if (line.Length >= 2 && line[1] == ':')
            {
                // The file lists use the same keys over and over; only the package's own fields are kept.
                fields.TryAdd(line[0], line[2..]);
            }
        }

        Flush(fields, repository, result);
        return result;
    }

    /// <summary>Reads every package from a string.</summary>
    /// <param name="text">The text.</param>
    /// <param name="repository">The repository the packages come from, if any.</param>
    public static List<ApkPackage> Read(string text, string? repository = null)
    {
        using var reader = new StringReader(text);
        return Read(reader, repository);
    }

    private static void Flush(Dictionary<char, string> fields, string? repository, List<ApkPackage> result)
    {
        if (fields.TryGetValue('P', out var name) && fields.TryGetValue('V', out var version))
        {
            result.Add(new ApkPackage
            {
                Name = name,
                Version = version,
                Architecture = fields.GetValueOrDefault('A'),
                Size = Number(fields.GetValueOrDefault('S')),
                InstalledSize = Number(fields.GetValueOrDefault('I')),
                Description = fields.GetValueOrDefault('T'),
                License = fields.GetValueOrDefault('L'),
                Checksum = fields.GetValueOrDefault('C'),
                Depends = ApkDependency.ParseList(fields.GetValueOrDefault('D')),
                Provides = ApkDependency.ParseList(fields.GetValueOrDefault('p')),
                InstallIf = ApkDependency.ParseList(fields.GetValueOrDefault('i')),
                ProviderPriority = (int)Number(fields.GetValueOrDefault('k')),
                Repository = repository,
            });
        }

        fields.Clear();
    }

    private static long Number(string? text) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
