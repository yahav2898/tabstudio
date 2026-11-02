using Avalonia.Platform.Storage;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// A workspace whose GitHub is <see cref="FakeGitHub"/>: the sample solution pushed to a fake repository, then
/// downloaded and opened the way the start screen does it, so the workspace is linked.
/// </summary>
internal sealed class GitWorkspaceFixture : IDisposable
{
    /// <summary>The fake repository holding the sample.</summary>
    public const string Repository = "hello";

    private readonly TempDirectory _temp = new();

    /// <param name="notifications">The system's notifications, for tests of CI notifications.</param>
    /// <param name="appActivity">Whether the app is in the background, for tests of CI polling.</param>
    public GitWorkspaceFixture(INotifications? notifications = null, IAppActivity? appActivity = null)
    {
        Paths = new AppPaths(_temp.Path);
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        Account = GitHub.CreateAccount(Paths);
        AccountView = new GitHubAccountViewModel(Account, Browser);
        Workspace = new WorkspaceViewModel(
            new CompilerServices(Paths), device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(Paths), Paths, AccountView, Browser,
            notifications: notifications, appActivity: appActivity);
    }

    /// <summary>A fixture whose workspace sends its requests to Claude through the test's own client.</summary>
    /// <param name="agentHttp">Where the workspace sends requests to Claude, such as <see cref="FakeClaude"/>.</param>
    public GitWorkspaceFixture(HttpClient agentHttp)
        : this()
    {
        Workspace.Dispose();
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        Workspace = new WorkspaceViewModel(
            new CompilerServices(Paths), device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(Paths), Paths, AccountView, Browser) { AgentHttp = agentHttp };
    }

    public FakeGitHub GitHub { get; } = new();

    public FakeBrowser Browser { get; } = new();

    public AppPaths Paths { get; }

    public GitHubAccount Account { get; }

    public GitHubAccountViewModel AccountView { get; }

    public WorkspaceViewModel Workspace { get; }

    public GitChangesViewModel Git => Workspace.GitChanges;

    /// <summary>The linked folder, once opened.</summary>
    public string Folder { get; private set; } = string.Empty;

    /// <summary>Puts the sample on the fake GitHub, downloads it like the start screen and opens it.</summary>
    /// <param name="signIn">Whether to sign in to the fake first.</param>
    /// <param name="atCommit">Whether to download the commit rather than the branch, as a link to a commit does.</param>
    public async Task OpenLinkedSampleAsync(bool signIn = true, bool atCommit = false)
    {
        if (signIn)
        {
            await Account.SignInAsync(FakeGitHub.Token);
        }

        GitHub.AddRepository(Repository, await SampleFilesAsync(Paths));
        var reference = atCommit ? GitHub.Head(Repository, "main")[..7] : null;
        Folder = await new GitHubImporter(Account, Paths).ImportAsync(new GitHubRepository(FakeGitHub.Login, Repository, reference));
        await Workspace.LoadAsync(Path.Combine(Folder, "HelloTablet.slnx"));
        await UiTestHelpers.WaitUntilAsync(() => Git.IsLinked && Git.BranchName.Length > 0, because: "the Git Changes window to find the link");
    }

    /// <summary>Opens the sample where it is extracted, a folder that is not on GitHub.</summary>
    public async Task OpenUnlinkedSampleAsync()
    {
        var solution = await new SampleLibrary(Paths).EnsureExtractedAsync();
        Folder = Path.GetDirectoryName(solution)!;
        await Workspace.LoadAsync(solution);
        await UiTestHelpers.WaitUntilAsync(() => Git.HasWorkspace, because: "the Git Changes window to see the workspace");
    }

    /// <summary>
    /// Puts the sample on the fake GitHub and opens it where it is extracted, with a <c>.git</c> folder as
    /// <c>git clone</c> of that repository leaves it, as a folder cloned on a computer is.
    /// </summary>
    public async Task OpenGitCloneOfSampleAsync()
    {
        GitHub.AddRepository(Repository, await SampleFilesAsync(Paths));
        var solution = await new SampleLibrary(Paths).EnsureExtractedAsync();
        Folder = Path.GetDirectoryName(solution)!;
        var git = Path.Combine(Folder, ".git");
        Directory.CreateDirectory(Path.Combine(git, "refs", "heads"));
        await File.WriteAllTextAsync(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");
        await File.WriteAllTextAsync(Path.Combine(git, "config"), $"[remote \"origin\"]\n\turl = https://github.com/{FakeGitHub.Login}/{Repository}.git\n[branch \"main\"]\n\tremote = origin\n");
        await File.WriteAllTextAsync(Path.Combine(git, "refs", "heads", "main"), GitHub.Head(Repository, "main") + "\n");
        await Workspace.LoadAsync(solution);
        await UiTestHelpers.WaitUntilAsync(() => Git.HasWorkspace, because: "the Git Changes window to see the workspace");
    }

    /// <summary>The full path of a file in the workspace folder.</summary>
    public string PathOf(string relativePath) => Path.Combine(Folder, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Writes a file as another editor would, then refreshes the Git Changes window.</summary>
    public async Task WriteAsync(string relativePath, string content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        await Git.RefreshAsync();
    }

    /// <summary>The bundled sample's files, by path, extracted into the app's projects folder.</summary>
    public static async Task<Dictionary<string, string>> SampleFilesAsync(AppPaths paths)
    {
        var folder = Path.GetDirectoryName(await new SampleLibrary(paths).EnsureExtractedAsync())!;
        return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).StartsWith(".tabstudio", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(folder, f).Replace('\\', '/'), f => File.ReadAllText(f), StringComparer.Ordinal);
    }

    public void Dispose()
    {
        Workspace.Dispose();
        AccountView.Dispose();
        GitHub.Dispose();
        _temp.Dispose();
    }
}

/// <summary>A browser that remembers the pages it was asked to open.</summary>
internal sealed class FakeBrowser : IStoragePicker
{
    public List<Uri> Opened { get; } = [];

    public Task<IStorageFolder?> PickFolderAsync(string title) => Task.FromResult<IStorageFolder?>(null);

    public Task OpenUriAsync(Uri uri)
    {
        Opened.Add(uri);
        return Task.CompletedTask;
    }
}
