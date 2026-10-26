using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;

namespace TabStudio.App.ViewModels;

/// <summary>A local server found in the running program, with explicit preview and browser actions.</summary>
public sealed record ServerNoticeViewModel(int Port, IRelayCommand OpenPreview, IAsyncRelayCommand OpenBrowser, IRelayCommand Dismiss)
{
    /// <summary>The notice's title.</summary>
    public string Title => $"App listening on port {Port}";
}

public sealed partial class WorkspaceViewModel
{
    private IStoragePicker _serverBrowser = null!;
    private readonly HashSet<int> _offeredPorts = [];

    /// <summary>The latest listener detected in the current run.</summary>
    [ObservableProperty]
    public partial ServerNoticeViewModel? ServerNotice { get; private set; }

    private void OfferServer(int port) => _ui.Post(() =>
    {
        if (_disposed || !_offeredPorts.Add(port))
        {
            return;
        }

        var address = new Uri($"http://127.0.0.1:{port}/");
        ServerNotice = new ServerNoticeViewModel(port,
            new RelayCommand(() =>
            {
                WebPreview.Show(StartupProject?.Name ?? "Local app", address, null);
                SelectSideBarPanel(SideBarPanel.WebPreview);
                ServerNotice = null;
            }),
            new AsyncRelayCommand(() => _serverBrowser.OpenUriAsync(address)),
            new RelayCommand(() => ServerNotice = null));
    });

    private void BeginServerRun()
    {
        _offeredPorts.Clear();
        ServerNotice = null;
    }

    private void EndServerRun()
    {
        ServerNotice = null;
        if (WebPreview.AppAddress is { } address && _offeredPorts.Contains(address.Port))
        {
            WebPreview.ShowStopped();
        }
    }
}
