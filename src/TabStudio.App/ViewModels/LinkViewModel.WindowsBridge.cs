using TabStudio.App.ViewModels.WindowsBridge;

namespace TabStudio.App.ViewModels;

/// <summary>The TabStudio Link dialog's Windows Bridge tab: a Windows PC that builds and runs Windows-only projects.</summary>
public sealed partial class LinkViewModel
{
    /// <summary>The Windows Bridge tab, or <see langword="null"/> where the app has none, as in tests.</summary>
    public WindowsBridgeViewModel? Bridge { get; }

    /// <summary>Whether the dialog has the Windows Bridge tab.</summary>
    public bool HasBridge => Bridge is not null;

    /// <summary>Whether the Windows Bridge tab is shown.</summary>
    public bool IsBridgeTab => SelectedTab == LinkTab.Bridge;

    partial void OnSelectedTabChanged(LinkTab value) => OnPropertyChanged(nameof(IsBridgeTab));
}
