using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.App.ViewModels;

/// <summary>
/// What the status bar says, and when it stays quiet (docs/design/README.md, "Calm by default"): a language service or
/// server only while it is not ready, the device's temperature only while it slows TabStudio down, TabStudio Link only
/// while this device is linked. The floating touch bar follows the last input: shown for a finger or a pen, faded away
/// while a hardware keyboard types.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// Whether the last input was a finger or a pen rather than a hardware keyboard, which the shell view tracks; the
    /// floating touch bar shows only then. True until the first key press, as a tablet starts out touch-first.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTouchInput { get; set; } = true;

    /// <summary>
    /// Whether the status bar shows the active file's language server: while it starts, restarts or has stopped, or is
    /// not installed; a server that is running and ready says nothing.
    /// </summary>
    public bool ShowsLanguageServerStatus =>
        ShowsLanguageServerText && ActiveDocument?.LanguageServer?.ServerState != LanguageServerState.Running;

    /// <summary>Whether the status bar shows TabStudio Link: while this device is linked to a host, reconnects to one, or hosts a tablet.</summary>
    public bool ShowsLinkStatus => IsLinkAvailable && (IsLinkedToHost || IsLinkReconnecting || _link?.IsHosting == true);

    /// <summary>Follows what the status bar's items depend on. Called once by the constructor.</summary>
    private void InitializeStatusBar() => PropertyChanged += OnPropertyChangedForStatusBar;

    private void OnPropertyChangedForStatusBar(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LanguageServerText) or nameof(ShowsLanguageServerText) or nameof(ServerInstallOffer) or nameof(ActiveDocument):
                OnPropertyChanged(nameof(ShowsLanguageServerStatus));
                break;
            case nameof(LinkStatusText) or nameof(IsLinkedToHost) or nameof(IsLinkReconnecting):
                OnPropertyChanged(nameof(ShowsLinkStatus));
                break;
        }
    }
}
