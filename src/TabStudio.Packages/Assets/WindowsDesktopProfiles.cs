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
        var profile = string.Equals(name, WpfFramework, StringComparison.OrdinalIgnoreCase) ? "WPF"
            : string.Equals(name, WindowsFormsFramework, StringComparison.OrdinalIgnoreCase) ? "WindowsForms"
            : null;
        var list = Path.Combine(packDirectory, "data", "FrameworkList.xml");
        if (profile is null || !File.Exists(list))
        {
            return all;
        }

        // Files without a Profile belong to every profile; the others list theirs, separated by semicolons.
        var profiles = XDocument.Load(list).Descendants("File")
            .Select(file => (Name: Path.GetFileName((string?)file.Attribute("Path") ?? string.Empty), Profile: (string?)file.Attribute("Profile")))
            .Where(file => file.Name.Length > 0)
            .GroupBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Profile, StringComparer.OrdinalIgnoreCase);
        return all.Where(path => !profiles.TryGetValue(Path.GetFileName(path), out var marked) || string.IsNullOrEmpty(marked)
            || marked.Split(';', StringSplitOptions.TrimEntries).Contains(profile, StringComparer.OrdinalIgnoreCase)).ToList();
    }
}
