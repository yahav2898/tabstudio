namespace TabStudio.Toolchains.Projects;

/// <summary>
/// Finds the toolchain projects that open a desktop window through a framework that needs a computer's window system
/// (X11, Wayland, Windows or macOS), which an Android tablet does not have: Electron apps, Rust apps on eframe, winit,
/// iced, Tauri, GTK, Slint or Bevy, and Kotlin apps on Compose for Desktop. TabStudio builds them and runs their tests,
/// but cannot show their window, and says so in one line instead of starting a program that fails.
/// </summary>
public static class DesktopGui
{
    private static readonly (string Crate, string Name)[] RustFrameworks =
    [
        ("eframe", "eframe (egui)"), ("winit", "winit"), ("iced", "iced"), ("tauri", "Tauri"), ("gtk", "GTK"), ("gtk4", "GTK"),
        ("slint", "Slint"), ("bevy", "Bevy"), ("druid", "Druid"), ("fltk", "FLTK"),
    ];

    /// <summary>
    /// The desktop window framework a project's program opens its window with, such as <c>Electron</c>; <see langword="null"/>
    /// for a project that needs none.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string? Framework(ToolchainProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        switch (project.Toolchain.Id)
        {
            case "node":
                var package = Read(Path.Combine(project.Directory, "package.json"));
                return package.Contains("\"electron\":", StringComparison.Ordinal) && RunPlanner.ReadJson(Path.Combine(project.Directory, "package.json"), "main") is not null
                    ? "Electron" : null;
            case "rust":
                var cargo = Read(Path.Combine(project.Directory, "Cargo.toml"));
                var at = cargo.IndexOf("[dependencies]", StringComparison.Ordinal);
                var dependencies = at >= 0 ? cargo[at..] : string.Empty;
                var program = cargo.Contains("[[bin]]", StringComparison.Ordinal) || File.Exists(Path.Combine(project.Directory, "src", "main.rs"));
                return dependencies.Length > 0 && program ? RustFrameworks.FirstOrDefault(f => Depends(dependencies, f.Crate)).Name : null;
            case "kotlin":
                var gradle = Read(Path.Combine(project.Directory, "build.gradle.kts")) + Read(Path.Combine(project.Directory, "desktopApp", "build.gradle.kts"));
                return gradle.Contains("compose.desktop", StringComparison.Ordinal) ? "Compose for Desktop" : null;
            default:
                return null;
        }
    }

    /// <summary>The sentence that says why such a project's window cannot open here.</summary>
    /// <param name="name">The project's name.</param>
    /// <param name="framework">Its framework, from <see cref="Framework"/>.</param>
    public static string Explanation(string name, string framework) =>
        $"{name} opens a desktop window with {framework}, which needs a computer's window system: TabStudio builds it and runs its tests here, but its window opens only on a computer.";

    private static bool Depends(string dependencies, string crate) =>
        dependencies.Contains("\n" + crate + " =", StringComparison.Ordinal) || dependencies.Contains("\n" + crate + "=", StringComparison.Ordinal)
        || dependencies.Contains("\n" + crate + ".workspace", StringComparison.Ordinal);

    private static string Read(string file)
    {
        try
        {
            return File.Exists(file) && new FileInfo(file).Length <= 512 * 1024 ? File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
