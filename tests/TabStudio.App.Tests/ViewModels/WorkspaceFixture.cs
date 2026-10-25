using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>A workspace view model over a fresh copy of the sample, with all data in a temporary folder.</summary>
internal sealed class WorkspaceFixture : IDisposable
{
    private readonly TempDirectory _temp = new();

    public WorkspaceFixture(DeviceInfo? device = null)
    {
        Paths = new AppPaths(_temp.Path);
        DeviceStatus = new DefaultDeviceStatus(device ?? new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        Profile = DeviceProfile.For(DeviceStatus.Device);
        Compiler = new CompilerServices(Paths);
        Sessions = new SessionStore(Paths);
        Workspace = new WorkspaceViewModel(Compiler, DeviceStatus, Profile, new AvaloniaUiDispatcher(), Sessions, Paths);
    }

    public AppPaths Paths { get; }

    public DefaultDeviceStatus DeviceStatus { get; }

    public DeviceProfile Profile { get; }

    public CompilerServices Compiler { get; }

    public SessionStore Sessions { get; }

    public WorkspaceViewModel Workspace { get; }

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
        _temp.Dispose();
    }
}
