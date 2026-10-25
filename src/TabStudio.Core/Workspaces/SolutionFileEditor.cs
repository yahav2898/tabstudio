using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Adds projects to and removes them from <c>.slnx</c> and <c>.sln</c> files, keeping everything else in the
/// file as it was.
/// </summary>
public static class SolutionFileEditor
{
    /// <summary>The project type of SDK-style C# projects in <c>.sln</c> files.</summary>
    private const string CSharpProjectTypeId = "9A19103F-16F7-4668-BE54-9A1E7A4F7556";

    /// <summary>Adds a project to a solution, unless it is already in it.</summary>
    /// <param name="solutionPath">The <c>.slnx</c> or <c>.sln</c> file.</param>
    /// <param name="projectPath">The project file to add.</param>
    /// <param name="solutionFolder">The solution folder to put it in, such as <c>src</c>, or <see langword="null"/> for the root.</param>
    /// <returns>Whether the file changed.</returns>
    public static bool AddProject(string solutionPath, string projectPath, string? solutionFolder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        if (SolutionParser.Load(solutionPath).Projects.Any(p => SamePath(p.AbsolutePath, projectPath)))
        {
            return false;
        }

        var relative = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(solutionPath))!, Path.GetFullPath(projectPath));
        if (Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            AddToSlnx(solutionPath, relative.Replace('\\', '/'), solutionFolder?.Trim('/'));
        }
        else
        {
            AddToSln(solutionPath, relative.Replace('/', '\\'), Path.GetFileNameWithoutExtension(projectPath));
        }

        return true;
    }

    /// <summary>Removes a project from a solution. The project's files stay on disk.</summary>
    /// <param name="solutionPath">The <c>.slnx</c> or <c>.sln</c> file.</param>
    /// <param name="projectPath">The project file to remove.</param>
    /// <returns>Whether the file changed.</returns>
    public static bool RemoveProject(string solutionPath, string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        var entry = SolutionParser.Load(solutionPath).Projects.FirstOrDefault(p => SamePath(p.AbsolutePath, projectPath));
        if (entry is null)
        {
            return false;
        }

        if (Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            RemoveFromSlnx(solutionPath, projectPath);
        }
        else
        {
            RemoveFromSln(solutionPath, entry.RelativePath.Replace('/', '\\'));
        }

        return true;
    }

    private static void AddToSlnx(string solutionPath, string relativePath, string? folder)
    {
        var file = XmlFile.Load(solutionPath);
        var root = file.Root;
        const string newline = "\n";
        var parent = root;
        var depth = 1;
        if (!string.IsNullOrEmpty(folder))
        {
            var folderName = $"/{folder}/";
            parent = root.Elements("Folder").FirstOrDefault(f => string.Equals((string?)f.Attribute("Name"), folderName, StringComparison.OrdinalIgnoreCase))
                ?? AppendElement(root, new XElement("Folder", new XAttribute("Name", folderName)), 1, newline);
            depth = 2;
        }

        AppendElement(parent, new XElement("Project", new XAttribute("Path", relativePath)), depth, newline);
        file.Save();
    }

    private static void RemoveFromSlnx(string solutionPath, string projectPath)
    {
        var file = XmlFile.Load(solutionPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        foreach (var project in file.Document.Descendants("Project").ToList())
        {
            var path = (string?)project.Attribute("Path");
            if (path is not null && SamePath(Path.Combine(directory, path.Replace('\\', Path.DirectorySeparatorChar)), projectPath))
            {
                if (project.PreviousNode is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value))
                {
                    whitespace.Remove();
                }

                project.Remove();
            }
        }

        file.Save();
    }

    /// <summary>Appends a child on its own line at the given depth, before the parent's closing tag.</summary>
    private static XElement AppendElement(XElement parent, XElement child, int depth, string newline)
    {
        const string Indent = "  ";
        if (parent.LastNode is XText closing && string.IsNullOrWhiteSpace(closing.Value))
        {
            closing.AddBeforeSelf(new XText(newline + string.Concat(Enumerable.Repeat(Indent, depth))), child);
        }
        else
        {
            parent.Add(new XText(newline + string.Concat(Enumerable.Repeat(Indent, depth))), child, new XText(newline + string.Concat(Enumerable.Repeat(Indent, depth - 1))));
        }

        return child;
    }

    private static void AddToSln(string solutionPath, string relativePath, string name)
    {
        var text = File.ReadAllText(solutionPath);
        var newline = Newline(text);
        var id = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var project = $"Project(\"{{{CSharpProjectTypeId}}}\") = \"{name}\", \"{relativePath}\", \"{id}\"{newline}EndProject{newline}";

        var global = text.IndexOf("Global" + newline, StringComparison.Ordinal);
        text = global >= 0 ? text.Insert(global, project) : text + project;

        // Build the project in every solution configuration, mapped to the project configuration of the same
        // name ("Debug|Any CPU"), as Visual Studio does for a new SDK-style project.
        var configurations = ReadSection(text, "SolutionConfigurationPlatforms", newline)
            .Select(line => line.Split('=')[0].Trim())
            .Where(c => c.Length > 0)
            .ToList();
        var mapping = new StringBuilder();
        foreach (var configuration in configurations)
        {
            mapping.Append(CultureInfo.InvariantCulture, $"\t\t{id}.{configuration}.ActiveCfg = {configuration}{newline}");
            mapping.Append(CultureInfo.InvariantCulture, $"\t\t{id}.{configuration}.Build.0 = {configuration}{newline}");
        }

        const string Header = "GlobalSection(ProjectConfigurationPlatforms) = postSolution";
        var section = text.IndexOf(Header, StringComparison.Ordinal);
        if (section >= 0 && mapping.Length > 0)
        {
            var end = text.IndexOf("\tEndGlobalSection", section, StringComparison.Ordinal);
            if (end >= 0)
            {
                text = text.Insert(end, mapping.ToString());
            }
        }
        else if (mapping.Length > 0)
        {
            var endGlobal = text.LastIndexOf("EndGlobal", StringComparison.Ordinal);
            if (endGlobal >= 0)
            {
                text = text.Insert(endGlobal, $"\t{Header}{newline}{mapping}\tEndGlobalSection{newline}");
            }
        }

        WriteText(solutionPath, text);
    }

    private static void RemoveFromSln(string solutionPath, string relativePath)
    {
        var text = File.ReadAllText(solutionPath);
        var newline = Newline(text);
        var lines = text.Split(newline).ToList();
        var start = lines.FindIndex(l => l.StartsWith("Project(", StringComparison.Ordinal) && l.Contains($"\"{relativePath}\"", StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return;
        }

        var id = lines[start].Split('"')[^2];
        var end = lines.FindIndex(start, l => l.StartsWith("EndProject", StringComparison.Ordinal));
        lines.RemoveRange(start, end - start + 1);
        lines.RemoveAll(l => l.TrimStart().StartsWith(id, StringComparison.OrdinalIgnoreCase));
        WriteText(solutionPath, string.Join(newline, lines));
    }

    private static IEnumerable<string> ReadSection(string text, string name, string newline)
    {
        var start = text.IndexOf($"GlobalSection({name})", StringComparison.Ordinal);
        if (start < 0)
        {
            yield break;
        }

        var end = text.IndexOf("EndGlobalSection", start, StringComparison.Ordinal);
        foreach (var line in text[start..end].Split(newline).Skip(1))
        {
            yield return line.Trim();
        }
    }

    /// <summary>Writes a .sln with the byte order mark Visual Studio writes.</summary>
    private static void WriteText(string path, string text) =>
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    private static string Newline(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
