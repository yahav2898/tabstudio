using Avalonia.Controls;

namespace TabStudio.App.Views.Toolchains;

/// <summary>
/// What toolchains draw over the IDE: the Toolchain Store (Tools &gt; Toolchain Store) with each toolchain's
/// version, size, state and actions, and the offer to install a project's toolchain or packages.
/// </summary>
public sealed partial class ToolchainOverlaysView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ToolchainOverlaysView() => InitializeComponent();
}
