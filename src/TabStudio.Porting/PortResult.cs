using System.Globalization;
using System.Text;

namespace TabStudio.Porting;

/// <summary>
/// A ported project, in memory until <see cref="Write"/>: its files, what the converter changed in each, and the
/// port notes for what it could not port exactly.
/// </summary>
public sealed class PortResult
{
    internal PortResult(PortSource source, PortOptions options, IReadOnlyList<PortedFile> files, IReadOnlyList<PortNote> notes)
    {
        SourceName = source.Name;
        SourceKind = source.Kind;
        ProjectName = options.ProjectName;
        ProjectDirectory = options.ProjectDirectory;
        Files = files;
        Notes = notes;
    }

    /// <summary>The original project's name.</summary>
    public string SourceName { get; }

    /// <summary>The original project's UI framework.</summary>
    public PortSourceKind SourceKind { get; }

    /// <summary>The new project's name.</summary>
    public string ProjectName { get; }

    /// <summary>The new project's folder.</summary>
    public string ProjectDirectory { get; }

    /// <summary>The new project file.</summary>
    public string ProjectFilePath => Path.Combine(ProjectDirectory, ProjectName + ".csproj");

    /// <summary>The new project's files.</summary>
    public IReadOnlyList<PortedFile> Files { get; }

    /// <summary>The spots for a person or Claude to finish, in file order.</summary>
    public IReadOnlyList<PortNote> Notes { get; }

    /// <summary>How many changes the converter made.</summary>
    public int ConversionCount => Files.Sum(f => f.ConversionCount);

    /// <summary>The name of the original UI framework, as people write it.</summary>
    public string SourceFramework => SourceKind == PortSourceKind.Wpf ? "WPF" : "Windows Forms";

    /// <summary>Writes the new project to its folder.</summary>
    /// <exception cref="IOException">The folder exists and is not empty.</exception>
    public void Write()
    {
        if (Directory.Exists(ProjectDirectory) && Directory.EnumerateFileSystemEntries(ProjectDirectory).Any())
        {
            throw new IOException($"{ProjectDirectory} already exists and is not empty.");
        }

        var root = Path.GetFullPath(ProjectDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var file in Files)
        {
            var path = Path.GetFullPath(Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (Path.IsPathRooted(file.Path) || !path.StartsWith(root, StringComparison.Ordinal))
            {
                throw new IOException($"The generated path escapes the new project: {file.Path}");
            }
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(ProjectDirectory))!;
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".tabstudio-port-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            foreach (var file in Files)
            {
                var path = Path.Combine(staging, file.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (file.Text is { } text)
                {
                    File.WriteAllText(path, text, encoding);
                }
                else if (file.CopyFrom is { } from)
                {
                    File.Copy(from, path);
                }
            }

            if (Directory.Exists(ProjectDirectory))
            {
                Directory.Delete(ProjectDirectory, recursive: false);
            }

            Directory.Move(staging, ProjectDirectory);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    /// <summary>
    /// The task for Claude: finish porting the spots in the notes, in the new project only, and build it. The paths
    /// are relative to the workspace's folder, as the agent's tools take them.
    /// </summary>
    /// <param name="projectFolder">The new project's folder relative to the workspace, with <c>/</c>, such as <c>WpfApp1.Avalonia</c>.</param>
    public string DescribeForAgent(string projectFolder)
    {
        ArgumentNullException.ThrowIfNull(projectFolder);
        var folder = projectFolder.TrimEnd('/');
        var prefix = folder.Length == 0 ? string.Empty : folder + "/";
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"TabStudio ported the {SourceFramework} project {SourceName} to Avalonia, as the new project {ProjectName} in {(folder.Length == 0 ? "the workspace folder" : folder + "/")}. ");
        text.Append("Its converter rewrote everything that maps exactly; finish porting these spots. Each one is marked in the code with a TODO(port) comment, often above the original code commented out. ");
        text.Append("Make each spot work on Avalonia the way it worked before, remove its TODO(port) comment, and keep everything else as it is. ");
        text.Append(CultureInfo.InvariantCulture, $"Change only files under {prefix}; the original project {SourceName} stays as it is. Build the project when you are done.\n\n");
        text.Append("Port notes (file:line: what. why):\n");
        foreach (var note in Notes)
        {
            text.Append(CultureInfo.InvariantCulture, $"- {prefix}{note.File}:{note.Line}: {note.What.TrimEnd('.')}. {note.Why}\n");
        }

        text.Append("\nThe app's windows derive from WindowView (Porting/WindowView.cs), which opens them as windows on the desktop and as the screen on phones and tablets; message boxes and dialogs are awaited.");
        return text.ToString();
    }
}
