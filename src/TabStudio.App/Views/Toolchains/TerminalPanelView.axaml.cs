using Avalonia.Controls;

namespace TabStudio.App.Views.Toolchains;

/// <summary>
/// The Terminal panel in the bottom dock: the shell's screen, a row of on-screen keys (Esc, Tab, Ctrl, Alt, the
/// arrows and the characters tablet keyboards hide), and, before the first shell, the Linux environment's setup.
/// </summary>
public sealed partial class TerminalPanelView : UserControl
{
    /// <summary>Creates the view.</summary>
    public TerminalPanelView() => InitializeComponent();
}
