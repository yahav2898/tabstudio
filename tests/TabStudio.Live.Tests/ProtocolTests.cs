using System.Text.Json;
using TabStudio.Link;
using TabStudio.Link.Security;
using TabStudio.Live.Protocol;
using TabStudio.Live.Text;

namespace TabStudio.Live.Tests;

/// <summary>The session's invitation and messages, as they travel.</summary>
public sealed class ProtocolTests
{
    [Fact]
    public void A_session_invitation_is_a_live_link_URI_that_round_trips()
    {
        var invitation = new LinkInvitation("Yahav's tablet", ["192.168.1.23"], LiveProtocol.DefaultPort, LinkIdentity.Create().Fingerprint, LinkPairingCode.Create())
        {
            Role = LinkRole.Live,
        };

        var uri = invitation.ToUri();

        Assert.StartsWith("tabstudio-link://live?", uri, StringComparison.Ordinal);
        Assert.True(LinkInvitation.TryParse(uri, out var scanned));
        Assert.Equal(LinkRole.Live, scanned.Role);
        Assert.Equal(invitation.Code, scanned.Code);
        Assert.Equal(invitation.Fingerprint, scanned.Fingerprint);
        Assert.Equal("Yahav's tablet", scanned.DeviceName);
    }

    [Fact]
    public void Edits_presence_and_participants_survive_the_wire()
    {
        var text = new SharedText(2);
        var insert = text.Insert(0, "hello")!;
        var delete = text.Delete(1, 2)!;
        var edit = new LiveEditNotice("src/Program.cs", [insert, delete]);

        var read = RoundTrip(edit, LiveJsonContext.Default.LiveEditNotice);
        Assert.Equal(edit.Path, read.Path);
        Assert.Equal(insert, read.Operations[0]);
        Assert.Equal(delete.Deleted, read.Operations[1].Deleted);
        Assert.Equal(delete with { Deleted = read.Operations[1].Deleted }, read.Operations[1]);

        var presence = new LivePresence(2, "src/Program.cs", text.PositionAt(1), text.PositionAt(3), 12);
        Assert.Equal(presence, RoundTrip(presence, LiveJsonContext.Default.LivePresence));

        var participants = new LiveParticipantsNotice(
            [new LiveParticipant(1, "Teacher", "tablet", 0, LiveRole.Host), new LiveParticipant(2, "Dana", "phone", 3, LiveRole.Viewer) { HandRaised = true, IsConnected = false }],
            Classroom: true,
            Presenter: 1);
        var back = RoundTrip(participants, LiveJsonContext.Default.LiveParticipantsNotice);
        Assert.Equal(participants.Participants, back.Participants);
        Assert.True(back.Classroom);

        // Short names keep a typing session's messages small.
        var json = JsonSerializer.Serialize(insert, LiveJsonContext.Default.TextOperation);
        Assert.Contains("\"t\":\"hello\"", json, StringComparison.Ordinal);
    }

    private static T RoundTrip<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) =>
        JsonSerializer.Deserialize(JsonSerializer.Serialize(value, type), type)!;
}
