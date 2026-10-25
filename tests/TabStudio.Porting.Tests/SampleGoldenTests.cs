using System.Globalization;
using System.Text;

namespace TabStudio.Porting.Tests;

/// <summary>
/// The samples ported end to end: every file of the new project, its port notes and its summary match the reviewed
/// output in Samples/&lt;sample&gt;/Expected.
/// </summary>
public sealed class SampleGoldenTests
{
    [Theory]
    [MemberData(nameof(SampleProjects.All), MemberType = typeof(SampleProjects))]
    public void The_sample_ports_to_the_expected_Avalonia_project(string sample)
    {
        var result = SampleProjects.Port(sample);
        var actual = result.Files.Where(f => f.Text is not null).ToDictionary(f => f.Path, f => f.Text!, StringComparer.Ordinal);
        actual["NOTES.txt"] = SampleProjects.NotesText(result);
        actual["SUMMARY.txt"] = SampleProjects.SummaryText(result);
        var expected = SampleProjects.Expected(sample);

        var differences = new StringBuilder();
        foreach (var (path, text) in actual.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            if (!expected.TryGetValue(path, out var wanted))
            {
                differences.Append(CultureInfo.InvariantCulture, $"New file: {path}\n");
            }
            else if (wanted.Replace("\r\n", "\n", StringComparison.Ordinal) != text.Replace("\r\n", "\n", StringComparison.Ordinal))
            {
                differences.Append(CultureInfo.InvariantCulture, $"Changed: {path}\n");
            }
        }

        foreach (var path in expected.Keys.Where(p => !actual.ContainsKey(p)))
        {
            differences.Append(CultureInfo.InvariantCulture, $"Missing: {path}\n");
        }

        if (differences.Length > 0)
        {
            var folder = SampleProjects.CiOut("porting-actual", sample);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            foreach (var (path, text) in actual)
            {
                var file = Path.Combine(folder, path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, text);
            }
        }

        Assert.True(differences.Length == 0, $"The port of {sample} differs from Samples/{sample}/Expected (the output is in ci-out/porting-actual/{sample}):\n{differences}");
    }
}
