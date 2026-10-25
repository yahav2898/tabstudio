using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Device;

/// <summary>What the tablet asks and tells this device: builds, the debugger, the screen and the web tunnel.</summary>
public sealed partial class SwarmMember
{
    private SwarmDebugAgent DebugAgent =>
        Current()?.Debugger ?? throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The app on this device does not run under the debugger.");

    private async Task<LinkReply> AnswerAsync(string method, LinkPayload payload, CancellationToken cancellationToken)
    {
        switch (method)
        {
            case SwarmMethods.Prepare:
                return SwarmJson.Reply(Prepare(payload.Read(SwarmJsonContext.Default.SwarmPrepareRequest)), SwarmJsonContext.Default.SwarmPrepareResponse);

            case SwarmMethods.Run:
                await RunAsync(payload.Read(SwarmJsonContext.Default.SwarmRunRequest), payload.Attachments, cancellationToken).ConfigureAwait(false);
                return LinkReply.Empty;

            case SwarmMethods.Stop:
                await StopRunAsync(payload.Read(SwarmJsonContext.Default.SwarmStopRequest).Run).ConfigureAwait(false);
                return LinkReply.Empty;

            case SwarmMethods.Breakpoints:
                Current()?.Debugger?.SetBreakpoints(payload.Read(SwarmJsonContext.Default.SwarmBreakpointsRequest).Breakpoints);
                return LinkReply.Empty;

            case SwarmMethods.Resume:
                var resume = payload.Read(SwarmJsonContext.Default.SwarmResumeRequest);
                var resumed = Current()?.Debugger?.Resume(resume.Pause, resume.Mode) == true;
                return SwarmJson.Reply(new SwarmResumeResponse(resumed), SwarmJsonContext.Default.SwarmResumeResponse);

            case SwarmMethods.Pause:
                DebugAgent.Pause();
                return LinkReply.Empty;

            case SwarmMethods.Variables:
                var variables = payload.Read(SwarmJsonContext.Default.SwarmVariablesRequest);
                return SwarmJson.Reply(DebugAgent.Variables(variables.Pause, variables.Reference), SwarmJsonContext.Default.SwarmVariablesResponse);

            case SwarmMethods.Evaluate:
                var evaluate = payload.Read(SwarmJsonContext.Default.SwarmEvaluateRequest);
                return SwarmJson.Reply(DebugAgent.Evaluate(evaluate.Pause, evaluate.Expression), SwarmJsonContext.Default.SwarmEvaluateResponse);

            case SwarmMethods.Input:
                // An app paused at a breakpoint takes no input; on Android its UI thread does not even run.
                var delivered = !IsAppPaused && await _host.SendInputAsync(payload.Read(SwarmJsonContext.Default.SwarmInput), cancellationToken).ConfigureAwait(false);
                return SwarmJson.Reply(new SwarmInputResponse(delivered), SwarmJsonContext.Default.SwarmInputResponse);

            case SwarmMethods.Capture:
                var image = IsAppPaused ? null : await _host.CaptureAsync(cancellationToken).ConfigureAwait(false);
                return image is null
                    ? SwarmJson.Reply(new SwarmCaptureResponse(false), SwarmJsonContext.Default.SwarmCaptureResponse)
                    : SwarmJson.Reply(new SwarmCaptureResponse(true), SwarmJsonContext.Default.SwarmCaptureResponse, [image]);

            case SwarmMethods.Layout:
                IReadOnlyList<SwarmLayoutIssue> issues = IsAppPaused ? [] : await _host.InspectLayoutAsync(cancellationToken).ConfigureAwait(false);
                return SwarmJson.Reply(new SwarmLayoutResponse(issues), SwarmJsonContext.Default.SwarmLayoutResponse);

            case SwarmMethods.ReadText:
                var path = payload.Read(SwarmJsonContext.Default.SwarmTextRequest).ElementPath;
                var text = IsAppPaused ? null : await _host.ReadTextAsync(path, cancellationToken).ConfigureAwait(false);
                return SwarmJson.Reply(new SwarmTextResponse(text), SwarmJsonContext.Default.SwarmTextResponse);

            default:
                throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, $"'{method}' is not part of TabStudio Swarm.");
        }
    }

    private bool IsAppPaused => AppState is SwarmDeviceState.Paused;

    private void OnNotification(string method, LinkPayload payload)
    {
        switch (method)
        {
            case SwarmMethods.ConsoleInput:
                if (Current()?.Run is SwarmConsoleRun console)
                {
                    console.Submit(payload.Read(SwarmJsonContext.Default.SwarmConsoleInput).Line);
                }

                break;
            case SwarmMethods.Notice:
                _host.ShowNotice(payload.Read(SwarmJsonContext.Default.SwarmNotice).Text);
                break;
            case SwarmMethods.WebData:
                Current()?.Gateway?.Deliver(payload.Read(SwarmJsonContext.Default.SwarmStreamNotice).Stream, payload.GetAttachment(0));
                break;
            case SwarmMethods.WebClose:
                Current()?.Gateway?.CloseFromCoordinator(payload.Read(SwarmJsonContext.Default.SwarmStreamNotice).Stream);
                break;
            case SwarmMethods.Removed:
                _ = Task.Run(OnRemovedAsync);
                break;
        }
    }

    /// <summary>The tablet removed this device: it forgets the swarm, which it has to pair with again to come back.</summary>
    private async Task OnRemovedAsync()
    {
        var coordinator = Coordinator;
        var name = CoordinatorName ?? "The tablet";
        await LeaveCoreAsync(notify: false, error: $"{name} removed this device from its swarm.").ConfigureAwait(false);
        if (coordinator is not null)
        {
            _options.Coordinators.Forget(coordinator.Fingerprint);
        }
    }

    /// <summary>Hands the tablet's messages to the member, once the handshake is done.</summary>
    private sealed class MemberLink(SwarmMember member) : ILinkMessageHandler
    {
        public Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken) =>
            member.AnswerAsync(method, payload, cancellationToken);

        public void HandleNotification(string method, LinkPayload payload) => member.OnNotification(method, payload);
    }
}
