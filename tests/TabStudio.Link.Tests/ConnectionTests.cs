using System.Security.Cryptography;
using TabStudio.Link.Messaging;

namespace TabStudio.Link.Tests;

public sealed class ConnectionTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Requests_are_answered_with_their_results()
    {
        await using var pair = await ConnectionPair.CreateAsync(new TestHandler());

        var answer = await pair.Client.RequestAsync("echo", TestHandler.Text("hello"));

        Assert.Equal("hello", answer.Read(TestJsonContext.Default.String));
    }

    [Fact]
    public async Task Both_sides_can_ask()
    {
        await using var pair = await ConnectionPair.CreateAsync(new TestHandler(), new TestHandler());

        var fromClient = await pair.Client.RequestAsync("echo", TestHandler.Text("to the host"));
        var fromServer = await pair.Server.RequestAsync("echo", TestHandler.Text("to the tablet"));

        Assert.Equal("to the host", fromClient.Read(TestJsonContext.Default.String));
        Assert.Equal("to the tablet", fromServer.Read(TestJsonContext.Default.String));
    }

    [Fact]
    public async Task Notifications_are_handled_before_the_requests_sent_after_them()
    {
        var server = new TestHandler();
        await using var pair = await ConnectionPair.CreateAsync(server);

        await pair.Client.NotifyAsync("edit/1");
        await pair.Client.NotifyAsync("edit/2");
        await pair.Client.RequestAsync("ask");

        Assert.Equal(["edit/1", "edit/2"], server.Notifications);
        Assert.Equal(["ask:edit/1,edit/2"], server.Requests);
    }

    [Fact]
    public async Task Errors_reach_the_caller_with_their_code()
    {
        var server = new TestHandler((_, _, _) => throw new LinkRemoteException(LinkErrorCodes.NoWorkspace, "Nothing is loaded."));
        await using var pair = await ConnectionPair.CreateAsync(server);

        var error = await Assert.ThrowsAsync<LinkRemoteException>(() => pair.Client.RequestAsync("ask"));

        Assert.Equal(LinkErrorCodes.NoWorkspace, error.Code);
        Assert.Equal("Nothing is loaded.", error.Message);
    }

    [Fact]
    public async Task A_failing_handler_answers_with_its_message()
    {
        var server = new TestHandler((_, _, _) => throw new InvalidOperationException("Roslyn fell over."));
        await using var pair = await ConnectionPair.CreateAsync(server);

        var error = await Assert.ThrowsAsync<LinkRemoteException>(() => pair.Client.RequestAsync("ask"));

        Assert.Equal(LinkErrorCodes.Failed, error.Code);
        Assert.Contains("Roslyn fell over.", error.Message, StringComparison.Ordinal);
        Assert.True(pair.Client.IsOpen);
    }

    [Fact]
    public async Task Canceling_a_request_cancels_it_on_the_other_side()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new TestHandler(async (_, _, cancellationToken) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                canceled.SetResult();
                throw;
            }

            return LinkReply.Empty;
        });
        await using var pair = await ConnectionPair.CreateAsync(server);
        using var cancellation = new CancellationTokenSource();

        var request = pair.Client.RequestAsync("build", cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(Patience);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await canceled.Task.WaitAsync(Patience);
        Assert.True(pair.Client.IsOpen);
        Assert.NotNull(await pair.Client.RequestAsync("still-there"));
    }

    [Fact]
    public async Task Large_attachments_arrive_intact_in_both_directions()
    {
        await using var pair = await ConnectionPair.CreateAsync(new TestHandler());
        var assembly = RandomNumberGenerator.GetBytes(40 * 1024 * 1024);
        var symbols = RandomNumberGenerator.GetBytes(3 * 1024 * 1024);

        var answer = await pair.Client.RequestAsync("echo", TestHandler.Text("build"), [assembly, symbols, Array.Empty<byte>()]);

        Assert.Equal(3, answer.Attachments.Count);
        Assert.Equal(SHA256.HashData(assembly), SHA256.HashData(answer.Attachments[0]));
        Assert.Equal(SHA256.HashData(symbols), SHA256.HashData(answer.Attachments[1]));
        Assert.Empty(answer.Attachments[2]);
    }

    [Fact]
    public async Task Small_messages_are_not_held_up_by_a_large_transfer()
    {
        await using var pair = await ConnectionPair.CreateAsync(new TestHandler());
        var large = pair.Client.RequestAsync("echo", null, [RandomNumberGenerator.GetBytes(64 * 1024 * 1024)]);

        var ping = await pair.Client.PingAsync().WaitAsync(Patience);
        await large.WaitAsync(Patience);

        Assert.True(ping < Patience);
        Assert.Equal<TimeSpan?>(ping, pair.Client.Latency);
    }

    [Fact]
    public async Task A_message_larger_than_a_frame_still_arrives()
    {
        await using var pair = await ConnectionPair.CreateAsync(new TestHandler());
        var text = new string('x', 6 * 1024 * 1024);

        var answer = await pair.Client.RequestAsync("echo", TestHandler.Text(text));

        Assert.Equal(text, answer.Read(TestJsonContext.Default.String));
    }

    [Fact]
    public async Task Requests_in_flight_fail_when_the_connection_closes()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new TestHandler(async (_, _, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return LinkReply.Empty;
        });
        await using var pair = await ConnectionPair.CreateAsync(server);
        var closed = new TaskCompletionSource<LinkClosedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        pair.Client.Closed += (_, e) => closed.TrySetResult(e);

        var request = pair.Client.RequestAsync("build");
        await started.Task.WaitAsync(Patience);
        pair.Server.Close();

        await Assert.ThrowsAsync<LinkConnectionLostException>(() => request).WaitAsync(Patience);
        await closed.Task.WaitAsync(Patience);
        Assert.False(pair.Client.IsOpen);
        await Assert.ThrowsAsync<LinkConnectionLostException>(() => pair.Client.RequestAsync("again"));
    }

    [Fact]
    public async Task A_silent_device_is_dropped_after_the_idle_timeout()
    {
        await using var pair = await ConnectionPair.CreateAsync(new TestHandler(), serverOptions: new LinkConnectionOptions { IdleTimeout = TimeSpan.FromMilliseconds(400) });

        await pair.Server.Completion.WaitAsync(Patience);

        Assert.IsType<TimeoutException>(pair.Server.CloseError);
    }

    [Fact]
    public async Task Regular_pings_keep_a_connection_alive_and_measure_the_latency()
    {
        await using var pair = await ConnectionPair.CreateAsync(
            new TestHandler(),
            clientOptions: new LinkConnectionOptions { PingInterval = TimeSpan.FromMilliseconds(100) },
            serverOptions: new LinkConnectionOptions { IdleTimeout = TimeSpan.FromMilliseconds(600) });

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        Assert.True(pair.Server.IsOpen);
        Assert.True(pair.Client.Latency.HasValue);
    }

    [Fact]
    public async Task Unknown_methods_of_the_built_in_kind_are_answered_by_the_connection()
    {
        await using var pair = await ConnectionPair.CreateAsync(new TestHandler());

        var answer = await pair.Client.RequestAsync(LinkConnection.PingMethod);

        Assert.Null(answer.Body);
    }

    /// <summary>A client and a server connection over loopback TCP.</summary>
    private sealed class ConnectionPair : IAsyncDisposable
    {
        private ConnectionPair(LinkConnection client, LinkConnection server)
        {
            Client = client;
            Server = server;
        }

        public LinkConnection Client { get; }

        public LinkConnection Server { get; }

        public static async Task<ConnectionPair> CreateAsync(
            ILinkMessageHandler serverHandler,
            ILinkMessageHandler? clientHandler = null,
            LinkConnectionOptions? clientOptions = null,
            LinkConnectionOptions? serverOptions = null)
        {
            var (client, server) = await Loopback.ConnectAsync();
            var pair = new ConnectionPair(new LinkConnection(client, clientOptions), new LinkConnection(server, serverOptions));
            pair.Server.Start(serverHandler);
            pair.Client.Start(clientHandler ?? new TestHandler());
            return pair;
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await Server.DisposeAsync();
        }
    }
}

