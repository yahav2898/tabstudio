namespace TabStudio.Core.Workspaces;

/// <summary>The Windows Desktop SDK's implicit framework reference, for Windows Forms and WPF projects.</summary>
public static partial class ProjectParser
{
    /// <summary>The Windows Desktop shared framework, which a project using both Windows Forms and WPF references whole.</summary>
    public const string WindowsDesktopFramework = "Microsoft.WindowsDesktop.App";

    /// <summary>The framework's WPF profile, which a project with <c>UseWPF</c> references.</summary>
    public const string WindowsDesktopWpfFramework = "Microsoft.WindowsDesktop.App.WPF";

    /// <summary>The framework's Windows Forms profile, which a project with <c>UseWindowsForms</c> references.</summary>
    public const string WindowsDesktopWindowsFormsFramework = "Microsoft.WindowsDesktop.App.WindowsForms";

    /// <summary>
    /// Adds the framework reference the Windows Desktop SDK adds for <c>UseWPF</c> and <c>UseWindowsForms</c>, as
    /// Microsoft.NET.Sdk.WindowsDesktop.targets does: the profile of the one the project uses, or the whole framework
    /// for both, unless the project references the framework itself.
    /// </summary>
    private static List<string> WithWindowsDesktopFramework(List<string> frameworks, Dictionary<string, string> properties)
    {
        var wpf = IsTrue(properties.GetValueOrDefault("UseWPF"));
        var forms = IsTrue(properties.GetValueOrDefault("UseWindowsForms"));
        if ((!wpf && !forms) || frameworks.Any(f => f.StartsWith(WindowsDesktopFramework, StringComparison.OrdinalIgnoreCase)))
        {
            return frameworks;
        }

        frameworks.Add(wpf && forms ? WindowsDesktopFramework : wpf ? WindowsDesktopWpfFramework : WindowsDesktopWindowsFormsFramework);
        return frameworks;

        static bool IsTrue(string? value) => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }
}
