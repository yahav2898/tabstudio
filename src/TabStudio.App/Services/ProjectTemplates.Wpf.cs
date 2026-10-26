using TabStudio.Wpf.Templates;

namespace TabStudio.App.Services;

/// <summary>
/// The WPF Application template: Visual Studio's own, file for file (see <see cref="WpfTemplates"/>), so the project
/// also opens and runs in Visual Studio on a PC. In TabStudio it builds on OpenSilver and runs in the web preview.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A WPF app with a window, run on OpenSilver in the web preview.</summary>
    public const string WpfId = WpfTemplates.WpfApplicationId;

    /// <summary>The start screen's entry for the WPF Application template.</summary>
    private static ProjectTemplate WpfTemplate => new(
        WpfId,
        "WPF Application",
        "A Windows desktop app with a window and XAML, built on OpenSilver and run in the web preview beside the code.",
        ["C#", "Windows", "Desktop", "WPF"]);

    private static string WriteWpf(string root, string name) => WpfTemplates.Create(root, name);
}
