# Roadmap

TabStudio is built in twelve milestones. Each one ships as a release on the
[releases page](https://github.com/yahav2898/tabstudio/releases) with an installable `TabStudio.apk`, and
each one leaves the app better for real work than the one before.

## 1. The IDE

The Visual Studio layout, sized for touch, with C# that builds and runs on the tablet.

- Menu bar, toolbar, document tabs, Solution Explorer, Properties, Error List, Output and a status bar.
- A code editor with Roslyn semantic highlighting, squiggles for errors and warnings, find and replace,
  toggle comment and Format Document, and highlighting for every common file type.
- Live errors as you type, in the editor and in the Error List.
- **F5**: build the startup project and its project references in memory and run it in-process, with
  console output and input in the Output panel. **Shift+F5** stops it.
- A start screen with recent solutions, the bundled sample, new projects from templates, folders imported
  from the tablet's storage, and repositories downloaded from GitHub.
- Stack detection and the Compatibility Doctor, which shows where each project runs and why.
- Thermal-aware builds and the device status in the status bar.

## 2. Real projects

- NuGet restore on the device, so projects with package references build as they are.
- Add, rename and delete files and folders; add projects and project references.
- Git: clone, branch, commit, pull and push, with GitHub sign-in and pull requests.

## 3. IntelliSense

Completion, signature help, Quick Info, Go to Definition, Find All References, Rename, code fixes and
refactorings (Ctrl+.), all from Roslyn.

## 4. Debugger

Breakpoints, Step Into, Step Over, Step Out, Locals, Watch, Call Stack, DataTips and break on exceptions,
for programs that run in-process.

## 5. Test Explorer

Discover and run xUnit, NUnit and MSTest tests, with results in Test Explorer and Live Unit Testing markers
in the editor margin.

## 6. Web

ASP.NET Core and Blazor apps served on the device and previewed live in a WebView beside the editor, with
Hot Reload.

## 7. Desktop frameworks

- Avalonia apps rendered natively in a pane beside the editor.
- Windows Forms on a cross-platform Forms layer, with a touch drag-and-drop designer.
- WPF through OpenSilver, and WinUI through the Uno Platform.

## 8. Notebooks

C# notebooks (`.dib` and `.ipynb`) whose cells run as you write them, with charts and tables in the output.

## 9. Toolchain Store

Native toolchains for C, C++, Rust, Go, Python, Node and Java, language servers and debug adapters for
their languages, and VS Code extensions from Open VSX. Opening a repository installs what it needs.

## 10. Windows Bridge

Pair a Windows PC you own; Windows-only projects build and run there, streamed to the tablet, while you edit
on the tablet.

## 11. Agent

Claude inside the IDE: an agent that builds, tests and fixes your solution through diffs you approve,
**Fix with Agent** for every Compatibility Doctor finding, sketch to XAML, and MCP server support.

## 12. Smart contracts, dApps and Supra

Solidity and Move, the Supra panel, Supra templates, the Supra SDK for .NET, the Migration Doctor, SupraFX in
the agent panel and an MCP server for building on Supra. The design is in
[design/web3-supra.md](design/web3-supra.md).

## Desktop mode

Across every milestone: when a monitor is connected, the full layout moves to the big screen and the tablet
becomes a touch Toolbox and Properties panel.
