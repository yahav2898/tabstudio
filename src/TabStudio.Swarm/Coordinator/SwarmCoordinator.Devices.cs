using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Coordinator;

/// <summary>The devices: joining, what they report, their latency, and losing them.</summary>
public sealed partial class SwarmCoordinator
{
    /// <summary>A device joined, or came back: it gets the build that runs now.</summary>
    private SwarmDevice OnJoin(LinkPeer peer, LinkConnection connection, SwarmHello hello)
    {
        SwarmDevice device;
        lock (_gate)
        {
            device = _devices.Find(d => d.Fingerprint == peer.Fingerprint) ?? new SwarmDevice(this, peer);
            if (!_devices.Contains(device))
            {
                _devices.Add(device);
            }
        }

        device.Attach(connection, peer, hello);
        connection.Closed += (_, _) => OnConnectionClosed(device, connection);
        if (!connection.IsOpen)
        {
            OnConnectionClosed(device, connection);
        }

        _ = Task.Run(() => MeasureLatencyAsync(device, connection));
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        if (CurrentRun is { } run)
        {
            _ = Task.Run(() => DeployAsync(device, run, _stopping.Token));
        }

        return device;
    }

    private void OnConnectionClosed(SwarmDevice device, LinkConnection connection)
    {
        _relay.CloseAll(connection);
        if (!device.Detach(connection, out var pause))
        {
            return;
        }

        if (pause is not null)
        {
            Resumed?.Invoke(this, device);
        }

        if (device.IsLeaving)
        {
            Forget(device);
            return;
        }

        RaiseDeviceChanged(device);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task MeasureLatencyAsync(SwarmDevice device, LinkConnection connection)
    {
        try
        {
            using var timer = new PeriodicTimer(_options.PingInterval);
            do
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                device.SetLatency(await connection.PingAsync(timeout.Token).ConfigureAwait(false));
                RaiseDeviceChanged(device);
            }
            while (connection.IsOpen && await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or LinkConnectionLostException)
        {
            // The device left, or the coordinator stopped. A device paused on its UI thread still answers pings,
            // because pings are answered by the connection on its own thread.
        }
    }

    private void OnState(SwarmDevice device, SwarmStateNotice notice)
    {
        var ended = device.SetState(notice.Run, notice.State, notice.AppName, notice.Detail);
        RaiseDeviceChanged(device);
        if (ended is not null)
        {
            Resumed?.Invoke(this, device);
        }
    }

    private void OnScreen(SwarmDevice device, SwarmScreenInfo screen)
    {
        device.SetScreen(screen);
        RaiseDeviceChanged(device);
    }

    private void OnExited(SwarmDevice device, SwarmExitNotice notice)
    {
        if (notice.Fault is { } fault)
        {
            device.SetFault(fault);
        }

        if (device.EndPause(0) is not null)
        {
            Resumed?.Invoke(this, device);
        }

        AppExited?.Invoke(this, new SwarmExitEventArgs(device, notice.Run, notice.Outcome, notice.ExitCode, notice.Fault));
        RaiseDeviceChanged(device);
    }

    /// <summary>Serves one device's connection: what the device asks and tells the coordinator.</summary>
    private sealed class SwarmDeviceLink(SwarmCoordinator owner, LinkPeer peer, LinkConnection connection) : ILinkMessageHandler
    {
        private volatile SwarmDevice? _device;

        public async Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken)
        {
            switch (method)
            {
                case SwarmMethods.Join:
                    _device = owner.OnJoin(peer, connection, payload.Read(SwarmJsonContext.Default.SwarmHello));
                    return SwarmJson.Reply(new SwarmWelcome(owner._options.Name), SwarmJsonContext.Default.SwarmWelcome);
                case SwarmMethods.WebConnect when _device is not null:
                    var stream = payload.Read(SwarmJsonContext.Default.SwarmStreamNotice).Stream;
                    var answer = await owner._relay.ConnectAsync(connection, stream, cancellationToken).ConfigureAwait(false);
                    return SwarmJson.Reply(answer, SwarmJsonContext.Default.SwarmConnectResponse);
                default:
                    throw new LinkRemoteException(
                        LinkErrorCodes.UnknownMethod,
                        _device is null
                            ? $"{owner._options.Name} coordinates a TabStudio Swarm. Join it first."
                            : $"'{method}' is not part of TabStudio Swarm.");
            }
        }

        public void HandleNotification(string method, LinkPayload payload)
        {
            if (_device is not { } device)
            {
                return;
            }

            switch (method)
            {
                case SwarmMethods.State:
                    owner.OnState(device, payload.Read(SwarmJsonContext.Default.SwarmStateNotice));
                    break;
                case SwarmMethods.Output:
                    var output = payload.Read(SwarmJsonContext.Default.SwarmOutputNotice);
                    owner.OutputReceived?.Invoke(owner, new SwarmOutputEventArgs(device, output.Text, output.IsError));
                    break;
                case SwarmMethods.Exited:
                    owner.OnExited(device, payload.Read(SwarmJsonContext.Default.SwarmExitNotice));
                    break;
                case SwarmMethods.Screen:
                    owner.OnScreen(device, payload.Read(SwarmJsonContext.Default.SwarmScreenInfo));
                    break;
                case SwarmMethods.Captured:
                    owner.InputCaptured?.Invoke(owner, new SwarmInputEventArgs(device, payload.Read(SwarmJsonContext.Default.SwarmInput)));
                    break;
                case SwarmMethods.Paused:
                    owner.OnPaused(device, payload.Read(SwarmJsonContext.Default.SwarmPauseNotice));
                    break;
                case SwarmMethods.Resumed:
                    owner.OnResumed(device, payload.Read(SwarmJsonContext.Default.SwarmResumedNotice));
                    break;
                case SwarmMethods.WebData:
                    owner._relay.Deliver(connection, payload.Read(SwarmJsonContext.Default.SwarmStreamNotice).Stream, payload.GetAttachment(0));
                    break;
                case SwarmMethods.WebClose:
                    owner._relay.CloseFromDevice(connection, payload.Read(SwarmJsonContext.Default.SwarmStreamNotice).Stream);
                    break;
                case SwarmMethods.Leave:
                    device.IsLeaving = true;
                    break;
            }
        }
    }
}
