using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.LanguageServers.JsonRpc;

namespace TabStudio.LanguageServers.Tests.JsonRpc;

public sealed class JsonRpcConnectionTests : IAsyncDisposable
{
    private readonly InMemoryPipe _toServer = new();
    private readonly InMemoryPipe _toClient = new();
    private readonly JsonRpcConnection _client;
    private readonly JsonRpcConnection _server;

    public JsonRpcConnectionTests()
    {
        _client = new JsonRpcConnection(_toClient.Reader, _toServer.Writer);
        _server = new JsonRpcConnection(_toServer.Reader, _toClient.Writer);
    }

    [Fact]
    public async Task A_request_gets_the_result_of_its_handler()
    {
        _server.AddRequestHandler("math/add", (parameters, _) =>
            Task.FromResult<JsonNode?>(parameters.GetProperty("a").GetInt32() + parameters.GetProperty("b").GetInt32()));
        Start();

        var result = await _client.SendRequestAsync("math/add", new JsonObject { ["a"] = 2, ["b"] = 3 }, CancellationToken.None);

        Assert.Equal(5, result.GetInt32());
    }

    [Fact]
    public async Task Requests_answered_out_of_order_find_their_callers()
    {
        var slow = new TaskCompletionSource<JsonNode?>();
        _server.AddRequestHandler("slow", (_, _) => slow.Task);
        _server.AddRequestHandler("fast", (_, _) => Task.FromResult<JsonNode?>("fast"));
        Start();

        var first = _client.SendRequestAsync("slow", null, CancellationToken.None);
        var second = await _client.SendRequestAsync("fast", null, CancellationToken.None);
        slow.SetResult("slow");

        Assert.Equal("fast", second.GetString());
        Assert.Equal("slow", (await first.WaitAsync(TestTimeouts.Default)).GetString());
    }

    [Fact]
    public async Task An_error_answer_throws_with_its_code_and_message()
    {
        _server.AddRequestHandler("fail", (_, _) => throw new JsonRpcException(JsonRpcErrorCodes.InvalidParams, "Bad parameters"));
        Start();

        var error = await Assert.ThrowsAsync<JsonRpcException>(() => _client.SendRequestAsync("fail", null, CancellationToken.None));

        Assert.Equal(JsonRpcErrorCodes.InvalidParams, error.Code);
        Assert.Equal("Bad parameters", error.Message);
    }

    [Fact]
    public async Task A_method_without_a_handler_answers_method_not_found()
    {
        Start();

        var error = await Assert.ThrowsAsync<JsonRpcException>(() => _client.SendRequestAsync("nobody/home", null, CancellationToken.None));

        Assert.Equal(JsonRpcErrorCodes.MethodNotFound, error.Code);
    }

    [Fact]
    public async Task Cancelling_a_request_tells_the_other_side_and_cancels_its_handler()
    {
        var handlerCancelled = new TaskCompletionSource();
        _server.AddRequestHandler("forever", async (_, cancellationToken) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                handlerCancelled.SetResult();
                throw;
            }

            return null;
        });
        Start();

        using var cancellation = new CancellationTokenSource();
        var request = _client.SendRequestAsync("forever", null, cancellation.Token);
        await Task.Delay(100);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await handlerCancelled.Task.WaitAsync(TestTimeouts.Default);
    }

    [Fact]
    public async Task Notifications_arrive_in_the_order_they_were_sent()
    {
        var received = new List<int>();
        var all = new TaskCompletionSource();
        _server.AddNotificationHandler("count", parameters =>
        {
            received.Add(parameters.GetProperty("n").GetInt32());
            if (received.Count == 50)
            {
                all.SetResult();
            }
        });
        Start();

        var sends = Enumerable.Range(1, 50).Select(n => _client.SendNotificationAsync("count", new JsonObject { ["n"] = n })).ToList();
        await Task.WhenAll(sends);
        await all.Task.WaitAsync(TestTimeouts.Default);

        Assert.Equal(Enumerable.Range(1, 50), received);
    }

    [Fact]
    public async Task Closing_the_stream_fails_the_requests_still_waiting()
    {
        _server.AddRequestHandler("never", (_, _) => new TaskCompletionSource<JsonNode?>().Task);
        Start();

        var request = _client.SendRequestAsync("never", null, CancellationToken.None);
        await Task.Delay(50);
        _toClient.Complete();

        var error = await Assert.ThrowsAsync<JsonRpcException>(() => request.WaitAsync(TestTimeouts.Default));
        Assert.True(error.IsConnectionClosed);
        await _client.Completion.WaitAsync(TestTimeouts.Default);
        Assert.True(_client.IsClosed);
    }

    [Fact]
    public async Task Typed_requests_write_and_read_through_source_generated_types()
    {
        _server.AddRequestHandler("echo", (parameters, _) => Task.FromResult(JsonNode.Parse(parameters.GetRawText())));
        Start();

        var result = await _client.SendRequestAsync(
            "echo",
            new Protocol.PositionDto(3, 7),
            Protocol.LspJsonContext.Default.PositionDto,
            Protocol.LspJsonContext.Default.PositionDto,
            CancellationToken.None);

        Assert.Equal(new Protocol.PositionDto(3, 7), result);
    }

    [Fact]
    public async Task A_null_result_is_answered_as_null()
    {
        _server.AddRequestHandler("nothing", (_, _) => Task.FromResult<JsonNode?>(null));
        Start();

        var result = await _client.SendRequestAsync("nothing", null, CancellationToken.None);

        Assert.Equal(JsonValueKind.Null, result.ValueKind);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _server.DisposeAsync();
        _toServer.Dispose();
        _toClient.Dispose();
    }

    private void Start()
    {
        _client.Start();
        _server.Start();
    }
}
