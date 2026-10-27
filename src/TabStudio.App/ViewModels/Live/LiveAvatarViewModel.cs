using Avalonia.Media;
using TabStudio.App.Theming;
using TabStudio.Live;

namespace TabStudio.App.ViewModels.Live;

/// <summary>A participant's avatar: initials on the participant's color, as tabs, carets and the participants list show it.</summary>
/// <param name="Id">The participant's number in the session.</param>
/// <param name="Name">The participant's name.</param>
/// <param name="Initials">Its initials.</param>
/// <param name="Color">Its color, 0 to 7.</param>
public sealed record LiveAvatarViewModel(int Id, string Name, string Initials, int Color)
{
    /// <summary>The avatar's background, one of the fixed avatar tokens, dark enough for white initials in both themes.</summary>
    public IBrush Background => BrushFor(Color);

    /// <summary>The avatar of a participant.</summary>
    /// <param name="participant">The participant.</param>
    public static LiveAvatarViewModel Of(LiveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        return new LiveAvatarViewModel(participant.Id, participant.Name, participant.Initials, participant.Color);
    }

    /// <summary>The brush of a participant's color.</summary>
    /// <param name="color">The color, 0 to 7.</param>
    public static IBrush BrushFor(int color) =>
        ThemeBrushes.Get(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Brush.Avatar.{(((color % LiveProtocol.Colors) + LiveProtocol.Colors) % LiveProtocol.Colors) + 1}"));

    /// <summary>A participant's color itself, for carets and selections drawn in the editor.</summary>
    /// <param name="color">The color, 0 to 7.</param>
    public static Color ColorFor(int color) => BrushFor(color) is ISolidColorBrush solid ? solid.Color : Colors.SteelBlue;
}
