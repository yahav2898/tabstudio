using Avalonia.Controls;

namespace TabStudio.App.Views.Toolchains;

/// <summary>
/// What toolchains draw over the IDE: the Toolchain Store (Tools &gt; Toolchain Store) with each toolchain's
/// version, size, state and actions, and the offer to install a project's toolchain or packages.
/// </summary>
public sealed partial class ToolchainOverlaysView : UserControl
{
    /// <summary>Below this width the Store puts a toolchain's actions under its text.</summary>
    private const double NarrowStoreWidth = 560;

    /// <summary>Creates the view.</summary>
    public ToolchainOverlaysView()
    {
        InitializeComponent();
        Store.SizeChanged += (_, e) => Store.Classes.Set("narrow", e.NewSize.Width < NarrowStoreWidth);
        TabStudio.App.Controls.DialogDismiss.Attach(this, StoreScrim, () => (DataContext as TabStudio.App.ViewModels.WorkspaceViewModel)?.ToolchainStore.CloseCommand.Execute(null));
    }
}
