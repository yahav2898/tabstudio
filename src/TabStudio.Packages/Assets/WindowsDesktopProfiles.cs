using System.Xml.Linq;

namespace TabStudio.Packages.Assets;

/// <summary>
/// The Windows Desktop shared framework and its profiles, as the Windows Desktop SDK references them: a project with
/// <c>UseWPF</c> references <c>Microsoft.WindowsDesktop.App.WPF</c>, one with <c>UseWindowsForms</c>
/// <c>Microsoft.WindowsDesktop.App.WindowsForms</c>, and one with both the whole framework. Each profile is the
/// targeting pack's assemblies its <c>data/FrameworkList.xml</c> marks for it, and those common to both.
/// </summary>
internal static class WindowsDesktopProfiles
{
    /// <summary>The whole framework.</summary>
    public const string Framework = "Microsoft.WindowsDesktop.App";

    /// <summary>The WPF profile.</summary>
    public const string WpfFramework = "Microsoft.WindowsDesktop.App.WPF";

    /// <summary>The Windows Forms profile.</summary>
    public const string WindowsFormsFramework = "Microsoft.WindowsDesktop.App.WindowsForms";

    /// <summary>Whether a framework reference names the Windows Desktop framework or one of its profiles.</summary>
    /// <param name="name">The framework reference.</param>
    public static bool IsWindowsDesktop(string name) =>
        string.Equals(name, Framework, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, WpfFramework, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, WindowsFormsFramework, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The analyzers of a profile: the targeting pack's <c>data/FrameworkList.xml</c> marks the Windows Forms source
    /// generator and analyzers for Windows Forms, so a WPF project does not get them.
    /// </summary>
    /// <param name="packDirectory">The extracted targeting pack.</param>
    /// <param name="name">The framework reference, which names the profile.</param>
    /// <param name="analyzers">The pack's analyzers for the compiler, as full paths.</param>
    public static List<string> Analyzers(string packDirectory, string name, IReadOnlyList<string> analyzers)
    {
        var profile = ProfileOf(name);
        var profiles = Profiles(packDirectory, "Analyzer");
        return [.. analyzers.Where(path => profile is null || InProfile(profiles, Path.GetRelativePath(packDirectory, path).Replace('\\', '/'), profile))];
    }

    /// <summary>The reference assemblies of a profile, from the targeting pack.</summary>
    /// <param name="packDirectory">The extracted <c>Microsoft.WindowsDesktop.App.Ref</c> package.</param>
    /// <param name="targetFramework">The target framework, such as <c>net10.0</c>.</param>
    /// <param name="name">The framework reference, which names the profile.</param>
    public static List<string> Assemblies(string packDirectory, string targetFramework, string name)
    {
        var folder = Path.Combine(packDirectory, "ref", targetFramework);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var all = Directory.EnumerateFiles(folder, "*.dll").Order(StringComparer.Ordinal).ToList();
        var profile = ProfileOf(name);
        if (profile is null)
        {
            return all;
        }

        var profiles = Profiles(packDirectory, "Managed");
        return [.. all.Where(path => InProfile(profiles, Path.GetRelativePath(packDirectory, path).Replace('\\', '/'), profile))];
    }

    /// <summary>The profile a framework reference names, or <see langword="null"/> for the whole framework.</summary>
    private static string? ProfileOf(string name) =>
        string.Equals(name, WpfFramework, StringComparison.OrdinalIgnoreCase) ? "WPF"
        : string.Equals(name, WindowsFormsFramework, StringComparison.OrdinalIgnoreCase) ? "WindowsForms"
        : null;

    /// <summary>The profiles <c>data/FrameworkList.xml</c> gives the pack's files of a type, by their paths in the pack.</summary>
    private static Dictionary<string, string?> Profiles(string packDirectory, string type)
    {
        var list = Path.Combine(packDirectory, "data", "FrameworkList.xml");
        if (!File.Exists(list))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        return XDocument.Load(list).Descendants("File")
            .Where(file => string.Equals((string?)file.Attribute("Type"), type, StringComparison.OrdinalIgnoreCase))
            .Select(file => (Path: ((string?)file.Attribute("Path") ?? string.Empty).Replace('\\', '/'), Profile: (string?)file.Attribute("Profile")))
            .Where(file => file.Path.Length > 0)
            .GroupBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Profile, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether a file is in a profile: files the list does not mark belong to every profile.</summary>
    private static bool InProfile(Dictionary<string, string?> profiles, string path, string profile) =>
        !profiles.TryGetValue(path, out var marked) || string.IsNullOrEmpty(marked)
        || marked.Split(';', StringSplitOptions.TrimEntries).Contains(profile, StringComparer.OrdinalIgnoreCase);
}
