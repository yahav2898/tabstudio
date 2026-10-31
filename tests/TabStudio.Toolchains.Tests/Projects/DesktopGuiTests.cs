using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>
/// electron-quick-start and eframe_template open a desktop window, which needs a computer's window system. Before 0.20.5
/// the Doctor said only that they need Node.js and Rust, and F5 started a program that could not show anything.
/// </summary>
public sealed class DesktopGuiTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    [Fact]
    public void An_Electron_app_and_a_Rust_eframe_app_open_desktop_windows()
    {
        _folder.Write("electron/package.json", """{ "name": "electron-quick-start", "main": "main.js", "devDependencies": { "electron": "^39.0.0" } }""");
        _folder.Write("eframe/Cargo.toml", "[package]\nname = \"eframe_template\"\n\n[dependencies]\negui = \"0.33\"\neframe = { version = \"0.33\", default-features = false }\n");
        _folder.Write("eframe/src/main.rs", "fn main() {}\n");

        Assert.Equal("Electron", DesktopGui.Framework(Project("node", "electron")));
        Assert.Equal("eframe (egui)", DesktopGui.Framework(Project("rust", "eframe")));
        Assert.StartsWith("eframe_template opens a desktop window with eframe (egui), which needs a computer's window system", DesktopGui.Explanation("eframe_template", "eframe (egui)"), StringComparison.Ordinal);
    }

    [Fact]
    public void Servers_libraries_and_command_line_programs_open_no_window()
    {
        _folder.Write("express/package.json", """{ "name": "express", "main": "index.js", "dependencies": { "debug": "^4" } }""");
        _folder.Write("lib/Cargo.toml", "[package]\nname = \"egui_extras\"\n\n[dependencies]\neframe = \"0.33\"\n");
        _folder.Write("lib/src/lib.rs", "");
        _folder.Write("cli/Cargo.toml", "[package]\nname = \"bat\"\n\n[dependencies]\nclap = \"4\"\n");
        _folder.Write("cli/src/main.rs", "fn main() {}\n");

        Assert.Null(DesktopGui.Framework(Project("node", "express")));
        Assert.Null(DesktopGui.Framework(Project("rust", "lib")));
        Assert.Null(DesktopGui.Framework(Project("rust", "cli")));
    }

    private ToolchainProject Project(string toolchain, string folder) =>
        new(ToolchainCatalog.Default.Find(toolchain)!, _folder.Resolve(folder), null);

    public void Dispose() => _folder.Dispose();
}
