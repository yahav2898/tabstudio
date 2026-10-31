using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// Windows' help viewer, which shows <c>.chm</c> help files and help popups. It does not exist here: the calls are
/// accepted and show nothing, so an app that offers help still runs.
/// </summary>
public static class Help
{
    /// <summary>Shows a help file.</summary>
    /// <param name="parent">The control the help belongs to.</param>
    /// <param name="url">The help file.</param>
    public static void ShowHelp(Control? parent, string? url)
    {
    }

    /// <summary>Shows a help file at a navigator's place.</summary>
    /// <param name="parent">The control the help belongs to.</param>
    /// <param name="url">The help file.</param>
    /// <param name="navigator">Where in it.</param>
    public static void ShowHelp(Control? parent, string? url, HelpNavigator navigator)
    {
    }

    /// <summary>Shows a help file at a keyword.</summary>
    /// <param name="parent">The control the help belongs to.</param>
    /// <param name="url">The help file.</param>
    /// <param name="keyword">The keyword.</param>
    public static void ShowHelp(Control? parent, string? url, string? keyword)
    {
    }

    /// <summary>Shows a help file at a navigator's place for a parameter.</summary>
    /// <param name="parent">The control the help belongs to.</param>
    /// <param name="url">The help file.</param>
    /// <param name="command">Where in it.</param>
    /// <param name="parameter">The keyword or topic.</param>
    public static void ShowHelp(Control? parent, string? url, HelpNavigator command, object? parameter)
    {
    }

    /// <summary>Shows a help file's index.</summary>
    /// <param name="parent">The control the help belongs to.</param>
    /// <param name="url">The help file.</param>
    public static void ShowHelpIndex(Control? parent, string? url)
    {
    }

    /// <summary>Shows a help popup.</summary>
    /// <param name="parent">The control the help belongs to.</param>
    /// <param name="caption">The text.</param>
    /// <param name="location">Where, on the screen.</param>
    public static void ShowPopup(Control? parent, string? caption, Point location)
    {
    }
}
