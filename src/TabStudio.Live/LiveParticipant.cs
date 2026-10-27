using System.Text.Json.Serialization;

namespace TabStudio.Live;

/// <summary>A device in a session, as every participant sees it.</summary>
/// <param name="Id">Its number in the session, which its edits carry; the host is 1.</param>
/// <param name="Name">Its name, such as <c>Dana's tablet</c>.</param>
/// <param name="Kind">What it is: <c>phone</c>, <c>tablet</c> or <c>computer</c>.</param>
/// <param name="Color">Its color, 0 to <see cref="LiveProtocol.Colors"/> − 1: its caret, its selection and its avatar.</param>
/// <param name="Role">What it may do.</param>
public sealed record LiveParticipant(int Id, string Name, string Kind, int Color, LiveRole Role)
{
    /// <summary>Whether the host lets it continue and step the debugged program.</summary>
    public bool CanStep { get; init; }

    /// <summary>Whether the host lets it type in the host's Terminal.</summary>
    public bool CanUseTerminal { get; init; }

    /// <summary>Whether it presents, in classroom mode: everyone else follows it.</summary>
    public bool IsPresenter { get; init; }

    /// <summary>Whether it raised its hand, in classroom mode.</summary>
    public bool HandRaised { get; init; }

    /// <summary>Whether it is connected now; a guest whose Wi-Fi dropped stays in the list while it reconnects.</summary>
    public bool IsConnected { get; init; } = true;

    /// <summary>Its initials, for its avatar: <c>DT</c> for Dana's tablet.</summary>
    [JsonIgnore]
    public string Initials => LiveNames.Initials(Name);
}
