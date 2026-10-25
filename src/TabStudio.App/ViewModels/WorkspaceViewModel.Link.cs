using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>
/// TabStudio Link in the IDE: the language service follows the app's link, so a linked host compiles while it is there
/// and this device does when it is not; the status bar says which.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private readonly LinkService? _link;
    private readonly LinkViewModel? _linkDialog;
    private bool _wasLinked;

    /// <summary>Whether TabStudio Link is available, for its status bar item and menu entry.</summary>
    public bool IsLinkAvailable => _linkDialog is not null;

    /// <summary>The Link status bar item: <c>Local</c>, <c>Pixel 9 · 12 ms</c>, <c>Reconnecting to Pixel 9</c>.</summary>
    [ObservableProperty]
    public partial string LinkStatusText { get; private set; } = "Local";

    /// <summary>What the Link status bar item means, for its tooltip.</summary>
    [ObservableProperty]
    public partial string LinkStatusTip { get; private set; } = "TabStudio Link: this device compiles on its own.";

    /// <summary>Whether a host compiles for this workspace right now.</summary>
    [ObservableProperty]
    public partial bool IsLinkedToHost { get; private set; }

    /// <summary>Whether the link was lost and this device compiles while it reconnects.</summary>
    [ObservableProperty]
    public partial bool IsLinkReconnecting { get; private set; }

    /// <summary>Opens the TabStudio Link dialog.</summary>
    [RelayCommand(CanExecute = nameof(IsLinkAvailable))]
    private void OpenLink() => _linkDialog?.Open();

    private void InitializeLink()
    {
        if (_link is not null)
        {
            _link.Changed += OnLinkChanged;
        }

        UpdateLinkStatus();
    }

    private void DisposeLink()
    {
        if (_link is not null)
        {
            _link.Changed -= OnLinkChanged;
        }
    }

    /// <summary>
    /// The workspace's language service: with TabStudio Link, one that follows the link between the host and this
    /// device; without it, this device's own.
    /// </summary>
    private async Task<ILanguageService> CreateLanguageServiceAsync(Workspace workspace)
    {
        var skeletons = Path.Combine(_paths.Cache, "Skeletons");
        if (_link is null)
        {
            var host = await _compiler.CreateHostAsync();
            var local = new LocalLanguageService(host, _compiler.Packages);
            if (_profile.UseSkeletonReferences)
            {
                local.UseSkeletonReferences(skeletons);
            }

            await Task.Run(() => host.Load(workspace, FileSystemTextProvider.Instance));
            local.SetConcurrentBuild(_device.Thermal == ThermalState.Normal);
            return local;
        }

        var linked = new LinkedLanguageService(_compiler, _link);
        if (_profile.UseSkeletonReferences)
        {
            linked.UseSkeletonReferences(skeletons);
        }

        linked.SetConcurrentBuild(_device.Thermal == ThermalState.Normal);
        linked.ModeChanged += OnLanguageModeChanged;
        await Task.Run(() => linked.Reload(workspace, FileSystemTextProvider.Instance));
        _wasLinked = linked.IsLinked;
        UpdateLinkStatus();
        return linked;
    }

    private void OnLinkChanged(object? sender, EventArgs e) => UpdateLinkStatus();

    /// <summary>The language service moved between the host and this device: say so, and analyze the open files again there.</summary>
    private void OnLanguageModeChanged(object? sender, EventArgs e) => _ui.Post(() =>
    {
        if (_disposed || _language is not LinkedLanguageService linked)
        {
            return;
        }

        var linkedNow = linked.IsLinked;
        if (_wasLinked && !linkedNow && State != IdeState.Running)
        {
            StatusText = $"Lost {_link?.LinkedHost?.Name ?? "the host"} · compiling on this device";
        }
        else if (!_wasLinked && linkedNow && State is IdeState.Ready or IdeState.Failed)
        {
            StatusText = $"Linked to {linked.HostName}";
        }

        _wasLinked = linkedNow;
        UpdateLinkStatus();
        foreach (var document in Documents)
        {
            _ = document.AnalyzeNowAsync();
        }
    });

    private void UpdateLinkStatus()
    {
        var linked = _language as LinkedLanguageService;
        var hostName = linked?.HostName ?? _link?.LinkedHost?.Name;
        IsLinkedToHost = linked?.IsLinked == true;
        IsLinkReconnecting = !IsLinkedToHost && _link?.ClientState is LinkClientState.Reconnecting or LinkClientState.Connecting;
        if (IsLinkedToHost)
        {
            var latency = LinkDisplay.Latency(_link?.Latency);
            LinkStatusText = latency is null ? hostName! : $"{hostName} · {latency}";
            LinkStatusTip = $"TabStudio Link: {hostName} compiles this workspace. Tap for details.";
        }
        else if (IsLinkReconnecting)
        {
            LinkStatusText = $"Reconnecting to {hostName}";
            LinkStatusTip = $"TabStudio Link: {hostName} is not answering, so this device compiles until it is back. Nothing is lost.";
        }
        else if (_link?.IsHosting == true)
        {
            LinkStatusText = _link.ServedTablet is { } tablet ? $"Hosting {tablet.Name}" : "Hosting";
            LinkStatusTip = "TabStudio Link: this device is a host for a tablet. Tap for the pairing code.";
        }
        else
        {
            LinkStatusText = "Local";
            LinkStatusTip = "TabStudio Link: this device compiles on its own. Tap to link a phone or PC that compiles for it.";
        }
    }
}
