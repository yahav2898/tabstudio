namespace TabStudio.Forms;

/// <summary>
/// The fonts the Forms layer draws with where an app asks for a Windows font the device does not have. Windows
/// Forms apps name fonts such as Segoe UI and Consolas; on a tablet those are drawn with the platform's default
/// sans-serif font and with <see cref="MonospaceFamily"/>, so text keeps its kind and roughly its width.
/// </summary>
public static class FormsFonts
{
    /// <summary>The family Windows' monospace fonts, such as Consolas and Courier New, are drawn with when missing.</summary>
    /// <remarks>An Avalonia font family name or address, such as <c>avares://App/Assets/Fonts#JetBrains Mono</c>.</remarks>
    public static string MonospaceFamily { get; set; } = "monospace";

    /// <summary>The family Windows' serif fonts, such as Times New Roman and Georgia, are drawn with when missing.</summary>
    public static string SerifFamily { get; set; } = "serif";
}
