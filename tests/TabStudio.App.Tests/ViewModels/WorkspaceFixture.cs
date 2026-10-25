using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.ViewModels;
using TabStudio.Link.Networking;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>A workspace view model over a fresh copy of the sample, with all data in a temporary folder.</summary>
internal sealed class WorkspaceFixture : IDisposable
{
    private readonly TempDirectory _temp = new();

    /// <param name="device">The device's memory and cores.</param>
    /// <param name="agentHttp">The agent's HTTP client, for a fake Claude.</param>
    /// <param name="link">Whether the workspace follows TabStudio Link, as in the app, from a tablet named <c>Test tablet</c>.</param>
    /// <param name="webViews">The embedded browsers of the web preview, for a fake one; none by default, as on a desktop.</param>
    public WorkspaceFixture(DeviceInfo? device = null, HttpClient? agentHttp = null, bool link = false, IWebViewFactory? webViews = null)
    {
        Paths = new AppPaths(_temp.Path);
        DeviceStatus = new DefaultDeviceStatus(device ?? new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        Profile = DeviceProfile.For(DeviceStatus.Device);
        Compiler = new CompilerServices(Paths);
        Sessions = new SessionStore(Paths);
        if (link)
        {
            Link = new LinkService(Paths, Compiler, DeviceStatus, Profile, new TestDeviceIdentity("Test tablet", LinkDeviceKinds.Tablet), new AvaloniaUiDispatcher());
            LinkDialog = new LinkViewModel(Link, new NoQrScanner());
        }

        Workspace = agentHttp is null
            ? new WorkspaceViewModel(Compiler, DeviceStatus, Profile, new AvaloniaUiDispatcher(), Sessions, Paths, webViews: webViews, link: Link, linkDialog: LinkDialog)
            : new WorkspaceViewModel(Compiler, DeviceStatus, Profile, new AvaloniaUiDispatcher(), Sessions, Paths, webViews: webViews, link: Link, linkDialog: LinkDialog) { AgentHttp = agentHttp };
    }

    public AppPaths Paths { get; }

    public DefaultDeviceStatus DeviceStatus { get; }

    public DeviceProfile Profile { get; }

    public CompilerServices Compiler { get; }

    public SessionStore Sessions { get; }

    public WorkspaceViewModel Workspace { get; }

    /// <summary>The app's TabStudio Link, when the fixture was made with it.</summary>
    public LinkService? Link { get; }

    /// <summary>The TabStudio Link dialog, when the fixture was made with Link.</summary>
    public LinkViewModel? LinkDialog { get; }

    /// <summary>Types a line into the program whenever it waits for input.</summary>
    public void AnswerInput(string line)
    {
        var output = Workspace.Output;
        output.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && output.IsWaitingForInput)
            {
                output.InputText = line;
                output.SubmitInputCommand.Execute(null);
            }
        };
    }

    public async Task<string> OpenSampleAsync()
    {
        var solution = await new SampleLibrary(Paths).EnsureExtractedAsync();
        await Workspace.LoadAsync(solution);
        return solution;
    }

    public void Dispose()
    {
        Workspace.Dispose();
        LinkDialog?.Dispose();
        Link?.Dispose();
        _temp.Dispose();
    }
}
