using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.Compilation.Running;
using TabStudio.Toolchains.Networking;

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
    private int _serverGeneration;

    /// <summary>The latest listener detected in the current run.</summary>
    [ObservableProperty]
    public partial ServerNoticeViewModel? ServerNotice { get; private set; }

    private void OfferServer(int port, int generation) => _ui.Post(() =>
    {
        if (_disposed || generation != _serverGeneration || !_offeredPorts.Add(port))
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

    private Action<int> BeginServerRun()
    {
        var generation = ++_serverGeneration;
        _offeredPorts.Clear();
        ServerNotice = null;
        return port => OfferServer(port, generation);
    }

    private void EndServerRun()
    {
        ++_serverGeneration;
        ServerNotice = null;
        if (WebPreview.AppAddress is { } address && _offeredPorts.Contains(address.Port))
        {
            WebPreview.ShowStopped();
        }
    }

    /// <summary>Also recognizes server output from an in-process .NET console application.</summary>
    private sealed class ServerConsoleSink(IConsoleSink target, Action<int> listening) : IConsoleSink
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<bool, string> _tails = [];
        private readonly HashSet<int> _seen = [];

        public void WriteOutput(string text)
        {
            target.WriteOutput(text);
            Feed(text, false);
        }

        public void WriteError(string text)
        {
            target.WriteError(text);
            Feed(text, true);
        }

        private void Feed(string text, bool error)
        {
            lock (_gate)
            {
                var combined = _tails.GetValueOrDefault(error, string.Empty) + text;
                var end = combined.LastIndexOf('\n');
                if (end >= 0)
                {
                    foreach (var port in ListeningPorts.ParseOutput(combined[..(end + 1)]))
                    {
                        if (_seen.Add(port)) listening(port);
                    }
                }

                var tail = combined[(end + 1)..];
                _tails[error] = tail.Length > 4096 ? tail[^4096..] : tail;
            }
        }
    }
}
