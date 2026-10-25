using System.Collections.Concurrent;
using System.Reflection;
using TabStudio.Compilation.Running;
using TabStudio.Swarm.Device;

namespace TabStudio.Swarm.Tests;

/// <summary>
/// A device's screen for tests: records the apps it is asked to show and the inputs replayed on it, and answers the
/// tablet's questions with fixed answers. Avalonia apps "run" until stopped, or until a test ends them; with
/// <see cref="RunsEntryPoint"/>, the app's <c>Main</c> runs on a thread of its own, under the debugger the tablet
/// asked for, as an Avalonia app's code runs on its UI thread.
/// </summary>
internal sealed class FakeDeviceHost : ISwarmDeviceHost
{
    /// <summary>A tiny PNG: what <see cref="CaptureAsync"/> answers while an app is shown.</summary>
    public static readonly byte[] Picture = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 1, 2, 3];

    /// <summary>The device's name, which a hosted app gets as its first argument.</summary>
    public string Name { get; init; } = "Device";

    /// <summary>Whether a shown app's <c>Main</c> runs, with the device's name as its argument.</summary>
    public bool RunsEntryPoint { get; set; }

    public SwarmScreenInfo Screen { get; set; } = new(412, 915, 2.6);

    public string Platform => "Test";

    /// <summary>Whether showing an app fails, as XAML that does not load would.</summary>
    public bool FailToStart { get; set; }

    /// <summary>The apps shown, in order.</summary>
    public ConcurrentQueue<SwarmApp> Shown { get; } = new();

    /// <summary>The inputs replayed on the screen, in order.</summary>
    public ConcurrentQueue<SwarmInput> Inputs { get; } = new();

    /// <summary>The notices shown, in order.</summary>
    public ConcurrentQueue<string> Notices { get; } = new();

    /// <summary>The texts elements show, by element path.</summary>
    public ConcurrentDictionary<string, string> Texts { get; } = new(StringComparer.Ordinal);

    /// <summary>The app shown now.</summary>
    public FakeAppRun? Current { get; private set; }

    public Task<ISwarmAppRun> StartAsync(SwarmApp app, CancellationToken cancellationToken)
    {
        if (FailToStart)
        {
            throw new InvalidOperationException("MainView.axaml could not be loaded: unknown element 'Buton'.");
        }

        Shown.Enqueue(app);
        var run = new FakeAppRun(app);
        Current = run;
        if (RunsEntryPoint && app.Entry is { } entry)
        {
            var program = LoadedProgram.Load(entry, app.Dependencies, app.PackageAssemblies);
            program.LoadAll();
            app.Debugger?.Attach(program.Assemblies);
            var thread = new Thread(() => run.RunMain(program.EntryAssembly, Name)) { IsBackground = true, Name = $"{Name}: {app.Name}" };
            thread.Start();
        }

        return Task.FromResult<ISwarmAppRun>(run);
    }

    public Task<bool> SendInputAsync(SwarmInput input, CancellationToken cancellationToken)
    {
        Inputs.Enqueue(input);
        return Task.FromResult(Current is not null && !input.ElementPath.Contains("Missing", StringComparison.Ordinal));
    }

    public Task<byte[]?> CaptureAsync(CancellationToken cancellationToken) => Task.FromResult(Current is null ? null : Picture);

    public Task<IReadOnlyList<SwarmLayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SwarmLayoutIssue>>(Current is null ? [] : [new SwarmLayoutIssue("SmallTouchTarget", "Warning", "The Count button is 30 by 30 pixels.", "MainView/Button#CountButton", 10, 20, 30, 30)]);

    public Task<string?> ReadTextAsync(string elementPath, CancellationToken cancellationToken) =>
        Task.FromResult(Texts.TryGetValue(elementPath, out var text) ? text : null);

    public void ShowNotice(string text) => Notices.Enqueue(text);
}

/// <summary>An Avalonia app "shown" by <see cref="FakeDeviceHost"/>.</summary>
internal sealed class FakeAppRun(SwarmApp app) : ISwarmAppRun
{
    private readonly TaskCompletionSource<RunResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SwarmApp App { get; } = app;

    public bool IsDisposed { get; private set; }

    public Task<RunResult> Completion => _completion.Task;

    /// <summary>Ends the app with an exception, as an unhandled exception in a click handler would.</summary>
    public void Crash(Exception exception) => _completion.TrySetResult(new RunResult(RunOutcome.Crashed, -1, TimeSpan.Zero, exception));

    /// <summary>Runs the app's <c>Main</c>, then ends the app as it ended.</summary>
    public void RunMain(Assembly assembly, string argument)
    {
        try
        {
            assembly.EntryPoint!.Invoke(null, [new[] { argument }]);
            _completion.TrySetResult(new RunResult(RunOutcome.Completed, 0, TimeSpan.Zero));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            Crash(ex.InnerException);
        }
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        _completion.TrySetResult(new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero));
        return ValueTask.CompletedTask;
    }
}
