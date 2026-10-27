using System.Diagnostics;
using System.Globalization;
using TabStudio.App.ViewModels;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Live.Sessions;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's TabStudio Live part: the app shares the sample, a second device in the same process joins it as a
/// scan of the QR code does, over the same encrypted, paired connection as on Wi-Fi, and edits go both ways.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The name of the device that joins the self test's session.</summary>
    public const string LiveGuestName = "Self-test guest";

    private static readonly TimeSpan LiveTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Shares the open sample on the loopback address, joins it from a guest with an identity of its own, types on each
    /// side and checks the other sees it, then undoes both edits and ends the session.
    /// </summary>
    /// <returns>Whether the session worked, with a one-line summary.</returns>
    private static async Task<SelfTestResult> LiveSampleAsync(MainViewModel main, WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (main.LiveJoin?.Live is not { } live || workspace.ActiveDocument is not { } document)
        {
            return new SelfTestResult(false, "Live: TabStudio Live is not available, or no document is open.", clock.Elapsed);
        }

        var loopback = live.LoopbackOnly;
        var panel = workspace.SelectedSideBarPanel;
        var original = document.Document.Text;
        var mirror = Path.Combine(live.CopiesDirectory, "self-test");
        live.LoopbackOnly = true;
        LiveGuest? guest = null;
        try
        {
            await workspace.ShareLiveSessionAsync();
            if (live.Host is not { Invitation: { } invitation } host)
            {
                return new SelfTestResult(false, $"Live: the session did not start: {workspace.LivePanel?.Error}", clock.Elapsed);
            }

            guest = new LiveGuest(
                new LiveGuestOptions { Identity = LinkIdentity.Create(), Name = LiveGuestName, Kind = "phone", MirrorDirectory = mirror },
                live.Dispatcher);
            if (!await guest.JoinAsync(LinkTarget.FromInvitation(invitation), cancellationToken))
            {
                return new SelfTestResult(false, $"Live: the guest could not join: {guest.Error}", clock.Elapsed);
            }

            if (host.RelativePathOf(document.FilePath) is not { } path || await guest.OpenDocumentAsync(path, cancellationToken) is not { } shared)
            {
                return new SelfTestResult(false, $"Live: the guest could not open {document.FileName}.", clock.Elapsed);
            }

            // The guest types at the top; the host's editor shows it. The host types at the end; the guest has it.
            const string guestLine = "// typed on the self test's guest\n";
            const string hostLine = "\n// typed on this device";
            var typed = Stopwatch.StartNew();
            shared.Edit(0, string.Empty, guestLine);
            if (!await WaitForLiveAsync(() => document.Document.Text.StartsWith(guestLine, StringComparison.Ordinal), cancellationToken))
            {
                return new SelfTestResult(false, "Live: the guest's edit did not reach this device.", clock.Elapsed);
            }

            var toHost = typed.Elapsed;
            typed.Restart();
            document.Document.Insert(document.Document.TextLength, hostLine);
            if (!await WaitForLiveAsync(() => shared.Text.ToString() == document.Document.Text, cancellationToken))
            {
                return new SelfTestResult(false, "Live: this device's edit did not reach the guest.", clock.Elapsed);
            }

            var toGuest = typed.Elapsed;

            // Each undoes its own edit only, and the sample is as it was.
            shared.UndoLast();
            document.Live?.Undo();
            if (!await WaitForLiveAsync(() => document.Document.Text == original && shared.Text.ToString() == original, cancellationToken))
            {
                return new SelfTestResult(false, "Live: undoing both edits did not restore the sample.", clock.Elapsed);
            }

            document.MarkLiveSaved();
            var participants = host.Participants.Count.ToString(CultureInfo.InvariantCulture);
            var ms = (long)Math.Max(toHost.TotalMilliseconds, toGuest.TotalMilliseconds);
            return new SelfTestResult(
                true,
                string.Create(CultureInfo.InvariantCulture, $"Live: shared the sample, {LiveGuestName} joined over TLS ({participants} participants), edits went both ways within {ms} ms"),
                clock.Elapsed);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TabStudio.Link.LinkRemoteException)
        {
            return new SelfTestResult(false, $"Live: {ex.Message}", clock.Elapsed);
        }
        finally
        {
            if (guest is not null)
            {
                await guest.LeaveAsync();
                guest.DeleteCopy();
                await guest.DisposeAsync();
            }

            await live.EndAsync();
            live.LoopbackOnly = loopback;
            workspace.SelectedSideBarPanel = panel;
            if (document.Document.Text == original)
            {
                document.MarkLiveSaved();
            }
        }
    }

    /// <summary>Waits until a condition holds, for at most <see cref="LiveTimeout"/>.</summary>
    private static async Task<bool> WaitForLiveAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + LiveTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(20, cancellationToken);
        }

        return true;
    }
}
