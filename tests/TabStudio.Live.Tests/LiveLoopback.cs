using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Live.Sessions;
using TabStudio.Swarm;
using TabStudio.Testing;

namespace TabStudio.Live.Tests;

/// <summary>
/// A session inside the test: a host sharing a folder on the loopback address, and guests with identities, copies and
/// threads of their own, connected to it over real TLS, as separate devices would be.
/// </summary>
internal sealed class LiveLoopback : IAsyncDisposable
{
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly TempDirectory _temp = new();
    private readonly List<TestGuest> _guests = [];

    private LiveLoopback(LiveRole defaultRole, bool classroom, TimeSpan? reconnectGrace = null)
    {
        Root = Path.Combine(_temp.Path, "Workspace");
        Directory.CreateDirectory(Root);
        Workspace = new FakeHostWorkspace();
        Host = new LiveHost(
            new LiveHostOptions
            {
                Identity = LinkIdentity.Create(),
                Name = "Teacher's tablet",
                WorkspaceRoot = Root,
                WorkspaceName = "Hello",
                Port = 0,
                LoopbackOnly = true,
                DefaultRole = defaultRole,
                IsClassroom = classroom,
                ReconnectGrace = reconnectGrace ?? TimeSpan.FromMinutes(5),
            },
            Workspace,
            Dispatcher);
    }

    public string Root { get; }

    public LiveSerialDispatcher Dispatcher { get; } = new();

    public FakeHostWorkspace Workspace { get; }

    public LiveHost Host { get; }

    public IReadOnlyList<TestGuest> Guests => _guests;

    public static async Task<LiveLoopback> StartAsync(IReadOnlyDictionary<string, string>? files = null, LiveRole defaultRole = LiveRole.Editor, bool classroom = false, TimeSpan? reconnectGrace = null)
    {
        var loopback = new LiveLoopback(defaultRole, classroom, reconnectGrace);
        foreach (var (path, content) in files ?? new Dictionary<string, string> { ["Program.cs"] = "class Program\n{\n}\n" })
        {
            var full = Path.Combine(loopback.Root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, content);
        }

        await loopback.On(() => loopback.Host.StartAsync());
        return loopback;
    }

    /// <summary>A guest that has not joined yet.</summary>
    public TestGuest CreateGuest(string name, IReadOnlyList<TimeSpan>? reconnectDelays = null)
    {
        var guest = new TestGuest(name, Path.Combine(_temp.Path, "guests", name.Replace(' ', '-').Replace("'", string.Empty, StringComparison.Ordinal)), reconnectDelays);
        _guests.Add(guest);
        return guest;
    }

    /// <summary>A guest that scans the host's QR code and joins.</summary>
    public async Task<TestGuest> JoinAsync(string name, IReadOnlyList<TimeSpan>? reconnectDelays = null)
    {
        var guest = CreateGuest(name, reconnectDelays);
        var invitation = await On(() => Host.Invitation) ?? throw new InvalidOperationException("The host shows no QR code.");
        Assert.True(await guest.On(() => guest.Session.JoinAsync(LinkTarget.FromInvitation(invitation))), await guest.On(() => guest.Session.Error));
        await UntilAsync(() => On(() => Host.Participants.Any(p => p.Name == name && p.IsConnected)), $"{name} in the host's list");
        return guest;
    }

    /// <summary>Runs work on the host's thread.</summary>
    public Task<T> On<T>(Func<T> work) => Dispatcher.InvokeAsync(work);

    /// <summary>Runs asynchronous work started on the host's thread.</summary>
    public Task<T> On<T>(Func<Task<T>> work) => Dispatcher.InvokeAsync(work);

    /// <summary>Runs asynchronous work started on the host's thread.</summary>
    public Task On(Func<Task> work) => Dispatcher.InvokeAsync(work);

    /// <summary>Runs work on the host's thread.</summary>
    public Task On(Action work) => Dispatcher.InvokeAsync(work);

    /// <summary>The host shares a file it opened, with the text its editor shows.</summary>
    public Task<LiveDocument> ShareAsync(string path) => On(() =>
        Host.Share(Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)), File.ReadAllText(Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar))))
            ?? throw new InvalidOperationException($"{path} is not in the shared folder."));

    public static async Task UntilAsync(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {what}.");
            await Task.Delay(20);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var guest in _guests)
        {
            await guest.DisposeAsync();
        }

        await Host.DisposeAsync();
        Dispatcher.Dispose();
        _temp.Dispose();
    }
}

/// <summary>A guest of a <see cref="LiveLoopback"/>, with its own identity, copy and thread.</summary>
internal sealed class TestGuest : IAsyncDisposable
{
    public TestGuest(string name, string folder, IReadOnlyList<TimeSpan>? reconnectDelays)
    {
        Name = name;
        Identity = LinkIdentity.Create();
        Session = new LiveGuest(
            new LiveGuestOptions
            {
                Identity = Identity,
                Name = name,
                Kind = LinkDeviceKinds.Phone,
                MirrorDirectory = folder,
                PingInterval = TimeSpan.FromMilliseconds(500),
                ReconnectDelays = reconnectDelays ?? [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250)],
            },
            Dispatcher);
    }

    public string Name { get; }

    public LinkIdentity Identity { get; }

    public LiveSerialDispatcher Dispatcher { get; } = new();

    public LiveGuest Session { get; }

    public Task<T> On<T>(Func<T> work) => Dispatcher.InvokeAsync(work);

    public Task<T> On<T>(Func<Task<T>> work) => Dispatcher.InvokeAsync(work);

    public Task On(Func<Task> work) => Dispatcher.InvokeAsync(work);

    public Task On(Action work) => Dispatcher.InvokeAsync(work);

    /// <summary>Opens a shared document on this guest.</summary>
    public async Task<LiveDocument> OpenAsync(string path) =>
        await On(() => Session.OpenDocumentAsync(path)) ?? throw new InvalidOperationException($"{Name} could not open {path}.");

    public async ValueTask DisposeAsync()
    {
        await Session.DisposeAsync();
        Dispatcher.Dispose();
    }
}

/// <summary>The host's IDE, as a session sees it: it records what the session asked of it.</summary>
internal sealed class FakeHostWorkspace : ILiveHostWorkspace
{
    public List<string> Saved { get; } = [];

    public List<(LiveDebugCommand Command, string Who)> DebugCommands { get; } = [];

    public List<(string Path, int Line, bool Remove, string Who)> BreakpointRequests { get; } = [];

    public List<byte> TerminalInput { get; } = [];

    public Dictionary<string, string> OpenTexts { get; } = new(StringComparer.Ordinal);

    public string? GetOpenText(string fullPath) => OpenTexts.GetValueOrDefault(fullPath);

    public Task SaveAsync(LiveDocument document)
    {
        Saved.Add(document.Text.ToString());
        File.WriteAllText(document.FullPath, document.Text.ToString());
        return Task.CompletedTask;
    }

    public Task<bool> ControlDebuggerAsync(LiveDebugCommand command, LiveParticipant participant)
    {
        DebugCommands.Add((command, participant.Name));
        return Task.FromResult(true);
    }

    public void SetBreakpoint(string fullPath, int line, string? condition, bool removes, LiveParticipant participant) =>
        BreakpointRequests.Add((fullPath, line, removes, participant.Name));

    public Task<IReadOnlyList<SwarmVariable>> GetVariablesAsync(long pauseId, string path, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SwarmVariable>>([new SwarmVariable("Count", "3", "int", 0, $"{path}.Count")]);

    public Task<LiveEvaluation> EvaluateAsync(long pauseId, string expression, CancellationToken cancellationToken) =>
        Task.FromResult(new LiveEvaluation(new SwarmVariable(expression, "42", "int", 0, expression), null));

    public void WriteTerminal(ReadOnlyMemory<byte> input, LiveParticipant participant) => TerminalInput.AddRange(input.ToArray());
}
