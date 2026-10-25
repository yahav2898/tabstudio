using Avalonia.Controls;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Services.Extensions;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Extensions.Installation;
using TabStudio.Testing;
using TabStudio.Testing.Extensions;

namespace TabStudio.App.Tests.Extensions;

/// <summary>
/// Extensions change what every editor does, so their tests run one at a time, after the tests that run in parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExtensionsTestGroup
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Extensions";
}

/// <summary>
/// A workspace with VS Code extensions: the extensions service over a temporary folder and the fake Open VSX registry,
/// and a folder with a Lumen program to open. Disposing it withdraws what its extensions contributed.
/// </summary>
internal sealed class ExtensionsFixture : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http;

    public ExtensionsFixture()
    {
        Paths = new AppPaths(Path.Combine(_temp.Path, "app"));
        Registry = new FakeOpenVsx();
        _http = Registry.CreateClient();
        Appearance = new AppearanceService(new AppearanceSettingsStore(Paths));
        Service = new ExtensionsService(Paths, _http, Appearance);
        Service.SetRegistry(FakeOpenVsx.Address);
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        Workspace = new WorkspaceViewModel(
            new CompilerServices(Paths),
            device,
            DeviceProfile.For(device.Device),
            new AvaloniaUiDispatcher(),
            new SessionStore(Paths),
            Paths,
            extensions: Service)
        {
            VsixPicker = Picker,
        };
        Folder = Path.Combine(_temp.Path, "LumenDemo");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "hello.lumen"), Program);
        File.WriteAllText(Path.Combine(Folder, "notes.txt"), "Notes about the Lumen demo.\n");
    }

    /// <summary>A Lumen program with a region, for folding, and a comment, for comment continuation.</summary>
    public const string Program = """
        # Greets everyone. TODO: localize
        fn greet(name) {
            let count = 42
            if name {
                print("Hello, " + name)
            }
            return json`{"ok": true}`
        }

        # region Helpers
        fn twice(x) {
            return x + x
        }
        # endregion

        """;

    public AppPaths Paths { get; }

    public FakeOpenVsx Registry { get; }

    public ExtensionsService Service { get; }

    /// <summary>The IDE's theme, which a light color theme makes light; the fixture sets it back to dark when disposed.</summary>
    public AppearanceService Appearance { get; }

    public WorkspaceViewModel Workspace { get; }

    public FakeVsixPicker Picker { get; } = new();

    public string Folder { get; }

    /// <summary>The key of the Lumen Night theme, once Lumen is installed.</summary>
    public string NightThemeKey => Service.Contributions.Themes.Single(t => t.Label == LumenExtension.NightTheme).Key;

    /// <summary>Installs the Lumen fixture from a package file, as Install from VSIX does.</summary>
    /// <param name="package">The package, or <see langword="null"/> for the standard one.</param>
    public async Task<InstalledExtension> InstallLumenAsync(byte[]? package = null)
    {
        using var stream = new MemoryStream(package ?? LumenExtension.Build());
        return await Service.InstallFileAsync(stream, "lumen-1.0.0.vsix");
    }

    /// <summary>Opens the demo folder as the workspace.</summary>
    public Task OpenFolderAsync() => Workspace.LoadAsync(Folder);

    /// <summary>Opens a file of the demo folder.</summary>
    public async Task<DocumentViewModel> OpenAsync(string name) =>
        await Workspace.OpenDocumentAsync(Path.Combine(Folder, name)) ?? throw new InvalidOperationException($"{name} did not open.");

    /// <summary>Shows the workspace in a window.</summary>
    /// <param name="width">The window's width.</param>
    /// <param name="height">The window's height.</param>
    /// <param name="bottomPanel">Whether the bottom panel (Error List, Output) is open, as it is at first.</param>
    public Window Show(int width = 1280, int height = 800, bool bottomPanel = true)
    {
        Workspace.IsBottomPanelVisible = bottomPanel;
        var window = new Window { Width = width, Height = height, Content = new WorkspaceView { DataContext = Workspace } };
        window.Show();
        return window;
    }

    public void Dispose()
    {
        Workspace.Dispose();
        AppearanceService.Preview(AppTheme.Dark);
        Service.Dispose();
        _http.Dispose();
        Registry.Dispose();
        _temp.Dispose();
    }
}

/// <summary>Picks a .vsix file the test chose, as the system's file picker would.</summary>
internal sealed class FakeVsixPicker : IVsixPicker
{
    /// <summary>The package to hand out next, or <see langword="null"/> to act as if the user cancelled.</summary>
    public byte[]? Next { get; set; }

    /// <summary>The name of the file handed out.</summary>
    public string Name { get; set; } = "lumen-1.0.0.vsix";

    public Task<PickedVsix?> PickAsync() =>
        Task.FromResult(Next is { } bytes ? new PickedVsix(Name, new MemoryStream(bytes)) : null);
}
