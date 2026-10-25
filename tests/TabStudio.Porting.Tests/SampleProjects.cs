using System.Globalization;
using System.Text;
using TabStudio.Porting;

namespace TabStudio.Porting.Tests;

/// <summary>
/// The sample projects under Samples/: each has the original project in Input/ and the Avalonia project the converter
/// makes of it in Expected/, with its port notes in NOTES.txt. When the output differs, the test writes what the
/// converter made to ci-out/porting-actual/, to review and take as the new expected output.
/// </summary>
internal static class SampleProjects
{
    /// <summary>The samples, as test data.</summary>
    public static TheoryData<string> All => new() { "WpfTemplate", "WinFormsTemplate", "WpfContacts", "WinFormsCustomers" };

    /// <summary>The WPF samples.</summary>
    public static TheoryData<string> Wpf => new() { "WpfTemplate", "WpfContacts" };

    /// <summary>The version of Avalonia the samples are ported to.</summary>
    public const string AvaloniaVersion = "11.3.22";

    /// <summary>The folder of a sample's original project.</summary>
    public static string InputDirectory(string sample)
    {
        var input = Path.Combine(AppContext.BaseDirectory, "Samples", sample, "Input");
        return Directory.GetDirectories(input).Single();
    }

    /// <summary>Reads a sample's original project.</summary>
    public static PortSource Load(string sample)
    {
        var directory = InputDirectory(sample);
        // The sample on its own, without the repository's Directory.Build.props above it.
        var path = Directory.GetFiles(directory, "*.csproj").Single();
        var project = TabStudio.Core.Workspaces.ProjectParser.Parse(path, File.ReadAllText(path));
        return PortSource.Load(project);
    }

    /// <summary>Ports a sample in memory, to a folder that is never written.</summary>
    public static PortResult Port(string sample)
    {
        var source = Load(sample);
        var name = source.Name + ".Avalonia";
        return AvaloniaPorter.Port(source, new PortOptions(name, Path.Combine(Path.GetTempPath(), "tabstudio-port", name), AvaloniaVersion));
    }

    /// <summary>The notes of a port as NOTES.txt lists them.</summary>
    public static string NotesText(PortResult result)
    {
        var text = new StringBuilder();
        foreach (var note in result.Notes)
        {
            text.Append(note).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>What the converter changed, per file, as SUMMARY.txt lists it.</summary>
    public static string SummaryText(PortResult result)
    {
        var text = new StringBuilder();
        foreach (var file in result.Files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            text.Append(file.Path);
            if (file.SourcePath is { } source && source != file.Path)
            {
                text.Append(" ← ").Append(source);
            }

            text.Append(CultureInfo.InvariantCulture, $" ({file.Kind})\n");
            foreach (var conversion in file.Conversions)
            {
                text.Append(CultureInfo.InvariantCulture, $"  {conversion.Description} · {conversion.Count}\n");
            }
        }

        return text.ToString();
    }

    /// <summary>The expected output of a sample, by file, or an empty dictionary when there is none yet.</summary>
    public static Dictionary<string, string> Expected(string sample)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Samples", sample, "Expected");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                files[Path.GetRelativePath(folder, file).Replace('\\', '/')] = File.ReadAllText(file);
            }
        }

        return files;
    }

    /// <summary>The ci-out folder of the repository, where CI publishes what tests write.</summary>
    public static string CiOut(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TabStudio.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? Path.GetTempPath();
        return Path.Combine([root, "ci-out", .. parts]);
    }
}
