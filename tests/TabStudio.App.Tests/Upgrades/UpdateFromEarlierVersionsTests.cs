using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Accessibility;
using TabStudio.App.Services;
using TabStudio.App.Services.Agent;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.DesktopMode;
using TabStudio.Core.Previews;
using TabStudio.Testing;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;

namespace TabStudio.App.Tests.Upgrades;

/// <summary>
/// Installing this APK over 0.19.0 to 0.21.1 keeps what the earlier version left in the app's storage: its settings, its
/// session (open documents with their unsaved text, breakpoints, watches, the startup project) and the toolchains the
/// Toolchain Store installed. Each test lays the storage out as that version wrote it (see
/// <see cref="EarlierVersionData"/>), starts this version's services on it, and checks that everything is read and that
/// nothing was deleted or reset.
/// </summary>
public sealed class UpdateFromEarlierVersionsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    /// <summary>Every release from 0.19.0 to 0.21.1; 0.20.5 was never released.</summary>
    public static TheoryData<string> EarlierVersions { get; } = new()
    {
        "0.19.0",
        "0.19.1",
        "0.19.2",
        "0.20.0",
        "0.20.1",
        "0.20.2",
        "0.20.3",
        "0.20.4",
        "0.20.6",
        "0.21.0",
        "0.21.1",
    };

    public void Dispose() => _temp.Dispose();

    [AvaloniaTheory]
    [MemberData(nameof(EarlierVersions))]
    public async Task The_first_start_after_the_update_reopens_the_session_the_earlier_version_saved(string version)
    {
        var data = new EarlierVersionData(_temp.Resolve("TabStudio"), version);
        var paths = new AppPaths(data.Root);

        // An update is not a crash: the earlier version left no note that this version would read as one.
        Assert.Null(new CrashLog(paths).TakeLastExit());

        using var services = AppServices.Create(paths);
        var main = services.GetRequiredService<MainViewModel>();
        await main.StartAsync();
        var workspace = main.Workspace;
        Assert.NotNull(workspace);
        try
        {
            Assert.True(string.IsNullOrEmpty(main.Start.ErrorText), main.Start.ErrorText);

            // The open documents, in tab order, with the unsaved text and the carets.
            Assert.Equal(new[] { data.Program, data.Greeter }, workspace.Documents.Select(d => d.FilePath));
            var program = workspace.Documents[0];
            Assert.True(program.IsDirty);
            Assert.Equal(EarlierVersionData.ProgramUnsaved, program.Document.Text);
            Assert.Equal(EarlierVersionData.ProgramCaret, program.CaretOffset);
            var greeter = workspace.Documents[1];
            Assert.False(greeter.IsDirty);
            Assert.Equal(EarlierVersionData.GreeterCaret, greeter.CaretOffset);
            Assert.Same(greeter, workspace.ActiveDocument);

            // Opening the session saved nothing over the files.
            Assert.Equal(EarlierVersionData.ProgramOnDisk, await File.ReadAllTextAsync(data.Program));

            // Breakpoints with their conditions, hit counts and log messages; the watches and the rest of the shell.
            var breakpoints = workspace.Breakpoints.All.OrderBy(b => b.FilePath, StringComparer.Ordinal).ToList();
            Assert.Equal(2, breakpoints.Count);
            var inGreeter = breakpoints.Single(b => b.FilePath == data.Greeter);
            Assert.Equal(6, inGreeter.Line);
            Assert.Null(inGreeter.Condition);
            Assert.Equal("2", inGreeter.HitCount);
            Assert.Equal("Greeting {name}", inGreeter.LogMessage);
            var inProgram = breakpoints.Single(b => b.FilePath == data.Program);
            Assert.Equal(3, inProgram.Line);
            Assert.Equal("name == \"tablet\"", inProgram.Condition);
            Assert.Null(inProgram.HitCount);
            Assert.Null(inProgram.LogMessage);
            Assert.Equal(new[] { "name", "args.Length" }, workspace.Watch.Expressions);
            Assert.Equal(128, workspace.RecordingBufferMegabytes);
            Assert.True(workspace.Properties.IsExpanded);

            // The startup project the earlier version saved. Sessions before 0.21.2 did not say whether the person chose
            // it, so it is taken as TabStudio's own pick, which this solution's only program is either way.
            Assert.Equal(data.AppProject, workspace.StartupProject?.FilePath);

            // The app's setting, read by the IDE: off, so the workspace opened without installing anything.
            Assert.False(workspace.SetUpProjectsWhenTheyOpen);

            // The recent list keeps the earlier version's entries.
            var recent = services.GetRequiredService<RecentWorkspaces>().GetAll();
            Assert.Equal(data.Solution, recent[0].Path);
            Assert.Contains(recent, r => r.Path == data.OlderFolder && r.Name == "Older" && r.Description == "Folder");

            // The session this version saves keeps all of it, in its own format: nothing was reset by the update.
            main.SaveSession();
            var saved = new SessionStore(paths).Load();
            Assert.NotNull(saved);
            Assert.Equal(data.Solution, saved.WorkspacePath);
            Assert.Equal(EarlierVersionData.ProgramUnsaved, saved.Documents.Single(d => d.Path == data.Program).UnsavedText);
            Assert.Null(saved.Documents.Single(d => d.Path == data.Greeter).UnsavedText);
            Assert.Equal(data.Greeter, saved.ActiveDocument);
            Assert.Equal(data.AppProject, saved.StartupProject);
            Assert.NotNull(saved.Breakpoints);
            Assert.Equal(2, saved.Breakpoints.Count);
            Assert.Contains(saved.Breakpoints, b => b.Path == data.Program && b.Line == 3 && b.Condition == "name == \"tablet\"");
            Assert.Contains(saved.Breakpoints, b => b.Path == data.Greeter && b.Line == 6 && b.HitCount == "2" && b.LogMessage == "Greeting {name}");
            Assert.Equal(new[] { "name", "args.Length" }, saved.WatchExpressions);
            Assert.Equal(128, saved.RecordingBufferMegabytes);
            Assert.True(saved.IsPropertiesExpanded);

            // Nothing had to be rescued from a session this version could not read.
            Assert.False(Directory.Exists(Path.Combine(paths.State, "Recovered")));
            Assert.Empty(Directory.GetFiles(paths.State, "session.unreadable-*"));
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(EarlierVersions))]
    public void The_settings_the_earlier_version_saved_are_read_and_left_as_they_were(string version)
    {
        var data = new EarlierVersionData(_temp.Resolve("TabStudio"), version);
        var paths = new AppPaths(data.Root);
        using var services = AppServices.Create(paths);

        // Tools > Theme and the text size; the text size is Android's until 0.20.4, which added the setting.
        var appearance = services.GetRequiredService<AppearanceService>();
        Assert.Equal(IdeTheme.Light, appearance.Theme);
        Assert.Equal(data.SavesTextSize ? TextSize.Larger : TextSize.System, appearance.TextSize);

        var desktop = services.GetRequiredService<DesktopModeViewModel>();
        Assert.True(desktop.UseDisplayAutomatically);
        Assert.Equal(DesktopScale.Percent150, desktop.Scale);

        var github = services.GetRequiredService<GitHubAccount>();
        Assert.True(github.IsSignedIn);
        Assert.Equal("tablet-dev", github.User?.Login);
        Assert.Equal("Tablet Dev", github.User?.Name);

        var agent = new AgentSettingsStore(paths).Load();
        Assert.Equal("sk-ant-api03-earlier-version", agent.ApiKey);
        Assert.Equal("claude-sonnet-5-5", agent.Model);
        Assert.Equal("Prefer file-scoped namespaces.", agent.Instructions);
        var server = Assert.Single(agent.McpServers);
        Assert.Equal(new McpServerSetting("docs", "https://mcp.example.org/docs", "mcp-token"), server);
        Assert.False(agent.LocalMode);

        var setup = new ProjectSetupStore(paths).Load();
        Assert.False(setup.SetUpProjectsWhenTheyOpen);
        Assert.NotNull(setup.Projects);
        Assert.Equal("csproj:4f1c2e", Assert.Contains(data.AppProject, setup.Projects));

        var liveTesting = new LiveTestingSettingsStore(paths).Load(data.Solution);
        Assert.NotNull(liveTesting);
        Assert.Equal(LiveTestingTrigger.OnSave, liveTesting.Trigger);
        var rule = Assert.Single(liveTesting.Rules);
        Assert.Equal(data.LibraryProject, rule.ProjectFilePath);
        Assert.Equal(string.Empty, rule.Name);
        Assert.False(rule.Include);

        Assert.Equal(PreviewZoomMode.Fill, new PreviewZoomStore(paths).Load(data.AppProject));

        var recent = services.GetRequiredService<RecentWorkspaces>().GetAll();
        Assert.Equal(new[] { data.Solution, data.OlderFolder }, recent.Select(r => r.Path));
        Assert.Equal(new[] { "Hello", "Older" }, recent.Select(r => r.Name));

        // Reading them changed none of them: a setting the update does not know is not dropped by a rewrite.
        foreach (var (file, content) in data.SettingsFiles)
        {
            Assert.Equal(content, File.ReadAllText(data.Resolve(file)));
        }
    }

    [Theory]
    [MemberData(nameof(EarlierVersions))]
    public async Task The_toolchains_the_earlier_version_installed_are_still_installed(string version)
    {
        var data = new EarlierVersionData(_temp.Resolve("TabStudio"), version);
        var native = Directory.CreateDirectory(_temp.Resolve("lib")).FullName;
        File.WriteAllText(Path.Combine(native, "libproot.so"), string.Empty);
        File.WriteAllText(Path.Combine(native, "libproot-loader.so"), string.Empty);
        var platform = new ToolchainPlatform
        {
            NativeLibraryDirectory = native,
            AndroidApiLevel = 35,
            Architecture = "x86_64",
            FreeSpace = _ => 50_000_000_000,
        };

        using var services = AppServices.Create(new AppPaths(data.Root), s => s.AddSingleton(platform));
        var toolchains = services.GetRequiredService<ToolchainServices>();
        Assert.True(toolchains.Platform.UsesLinuxEnvironment);
        await toolchains.EnsureStatusAsync();
        // Read again where an error would surface: EnsureStatusAsync keeps the catalog's estimates when reading fails.
        await toolchains.Manager.RefreshAsync();
        var manager = toolchains.Manager;

        Assert.Null(manager.Problem);
        Assert.True(manager.IsRuntimeReady);
        Assert.Equal(ToolchainState.Installed, manager.Status("cpp").State);
        Assert.Equal("22.1.4", manager.Status("cpp").Version);
        Assert.Equal(ToolchainState.Installed, manager.Status("go").State);
        Assert.Equal("1.25.4", manager.Status("go").Version);
        // Python ran setup steps; it is installed as long as they are the catalog's, and offers an update once they change.
        Assert.True(manager.Status("python").IsReady);
        Assert.Equal("3.14.0", manager.Status("python").Version);
        // What the earlier version did not install is still not installed.
        Assert.Equal(ToolchainState.NotInstalled, manager.Status("rust").State);
        Assert.Equal(ToolchainState.NotInstalled, manager.Status("java").State);

        // The Store's records, apk's database and what the setup steps unpacked are all still there, unchanged.
        foreach (var (file, content) in data.ToolchainFiles)
        {
            Assert.Equal(content, File.ReadAllText(data.Resolve(file)));
        }
    }
}
