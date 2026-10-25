using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Coordinator;

/// <summary>Runs: sending a build to every device through the cache of files by hash, stopping it, and console input.</summary>
public sealed partial class SwarmCoordinator
{
    private readonly SwarmPackageHashes _hashes = new();
    private SwarmRun? _run;
    private SwarmPayload? _payload;
    private long _lastRunId;

    /// <summary>The build that runs on the swarm, or <see langword="null"/> when none does.</summary>
    public SwarmRun? CurrentRun
    {
        get
        {
            lock (_gate)
            {
                return _run;
            }
        }
    }

    /// <summary>
    /// Runs a build on every device of the swarm, and on every device that joins while it runs: each one is sent only
    /// the files it lacks, then replaces the app it runs with this build, as a save does. Devices that fail are reported
    /// in <see cref="SwarmRun.Deployments"/> and <see cref="Deployed"/>, and never stop the others.
    /// </summary>
    /// <param name="build">The build.</param>
    /// <param name="cancellationToken">Stops waiting; the devices keep what they received.</param>
    /// <returns>The run, once every device connected now received it or failed.</returns>
    /// <exception cref="IOException">A package assembly of the build cannot be read.</exception>
    public async Task<SwarmRun> RunAsync(SwarmBuild build, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        var payload = await Task.Run(() => SwarmPayload.Create(build, _hashes), cancellationToken).ConfigureAwait(false);
        var secret = build.Kind == SwarmAppKind.Web ? Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24)) : null;
        var run = new SwarmRun(Interlocked.Increment(ref _lastRunId), build, secret);
        lock (_gate)
        {
            _run = run;
            _payload = payload;
        }

        // A new build replaces the web connections of the old one; the pages reload.
        _relay.CloseAll();
        var devices = Devices.Where(d => d.IsConnected).ToList();
        await Task.WhenAll(devices.Select(d => DeployAsync(d, run, cancellationToken))).ConfigureAwait(false);
        return run;
    }

    /// <summary>Stops the app on every device; they wait for the next build.</summary>
    public async Task StopRunAsync()
    {
        SwarmRun? run;
        lock (_gate)
        {
            run = _run;
            _run = null;
            _payload = null;
        }

        _relay.CloseAll();
        if (run is null)
        {
            return;
        }

        var stop = SwarmJson.Body(new SwarmStopRequest(run.Id), SwarmJsonContext.Default.SwarmStopRequest);
        await Task.WhenAll(Devices.Select(device => StopOnAsync(device, stop))).ConfigureAwait(false);
    }

    /// <summary>Sends a line typed on the tablet to the console app on every device, or ends their input.</summary>
    /// <param name="line">The line, without its newline, or <see langword="null"/> for the end of input.</param>
    public void SendConsoleInput(string? line)
    {
        if (CurrentRun is not { Build.Kind: SwarmAppKind.Console })
        {
            return;
        }

        var body = SwarmJson.Body(new SwarmConsoleInput(line), SwarmJsonContext.Default.SwarmConsoleInput);
        foreach (var device in Devices)
        {
            if (device.Connection is { } connection)
            {
                _ = NotifySafelyAsync(connection, SwarmMethods.ConsoleInput, body);
            }
        }
    }

    /// <summary>Sends a run to one device: what it lacks, then the request to run it.</summary>
    private async Task DeployAsync(SwarmDevice device, SwarmRun run, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        SwarmPayload? payload;
        lock (_gate)
        {
            payload = ReferenceEquals(_run, run) ? _payload : null;
        }

        if (payload is null || device.Connection is not { } connection)
        {
            return;
        }

        SwarmDeployment deployment;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        timeout.CancelAfter(_options.DeployTimeout);
        try
        {
            var prepare = await connection.RequestAsync(
                SwarmMethods.Prepare,
                SwarmJson.Body(new SwarmPrepareRequest(payload.Files), SwarmJsonContext.Default.SwarmPrepareRequest),
                cancellationToken: timeout.Token).ConfigureAwait(false);
            var missing = prepare.Read(SwarmJsonContext.Default.SwarmPrepareResponse).Missing.ToHashSet(StringComparer.Ordinal);

            var attached = new List<string>();
            var attachments = new List<ReadOnlyMemory<byte>>();
            long bytes = 0;
            foreach (var file in payload.Files.Where(f => missing.Contains(f.Hash)))
            {
                var content = payload.Read(file.Hash);
                attached.Add(file.Hash);
                attachments.Add(content);
                bytes += content.Length;
            }

            var request = payload.CreateRequest(run, attached, Breakpoints);
            await connection.RequestAsync(
                SwarmMethods.Run,
                SwarmJson.Body(request, SwarmJsonContext.Default.SwarmRunRequest),
                attachments,
                timeout.Token).ConfigureAwait(false);
            device.SetUpdated(run.Id, run.Build.AppName, DateTimeOffset.UtcNow);
            deployment = new SwarmDeployment(device, null, attached.Count, payload.Files.Count, bytes, clock.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_stopping.IsCancellationRequested)
        {
            var seconds = _options.DeployTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture);
            deployment = Failed(device, $"{device.Name} did not take the build within {seconds} seconds.", clock);
        }
        catch (LinkRemoteException ex)
        {
            deployment = Failed(device, ex.Message, clock);
        }
        catch (Exception ex) when (ex is LinkConnectionLostException or LinkProtocolException or IOException)
        {
            deployment = Failed(device, $"{device.Name} was lost while it received the build.", clock);
        }

        run.Add(deployment);
        RaiseDeviceChanged(device);
        Deployed?.Invoke(this, deployment);
    }

    private static SwarmDeployment Failed(SwarmDevice device, string error, Stopwatch clock)
    {
        device.SetState(0, SwarmDeviceState.Failed, null, error);
        return new SwarmDeployment(device, error, 0, 0, 0, clock.Elapsed);
    }

    private static async Task StopOnAsync(SwarmDevice device, JsonElement stop)
    {
        if (device.Connection is not { } connection)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await connection.RequestAsync(SwarmMethods.Stop, stop, cancellationToken: timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or LinkConnectionLostException or LinkRemoteException or LinkProtocolException)
        {
            // A device that does not stop in time is told again by the next build; one that left has nothing to stop.
        }
    }

    private static async Task NotifySafelyAsync(LinkConnection connection, string method, JsonElement body)
    {
        try
        {
            await connection.NotifyAsync(method, body).ConfigureAwait(false);
        }
        catch (LinkConnectionLostException)
        {
            // The device left.
        }
    }
}
