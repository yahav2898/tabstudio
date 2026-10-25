using System.Collections.Concurrent;
using System.Text.Json;
using TabStudio.Link.Messaging;

namespace TabStudio.Link.Tests;

/// <summary>A message handler for tests: answers requests with a function, and records notifications in order.</summary>
internal sealed class TestHandler : ILinkMessageHandler
{
    private readonly Func<string, LinkPayload, CancellationToken, Task<LinkReply>> _answer;

    public TestHandler(Func<string, LinkPayload, CancellationToken, Task<LinkReply>>? answer = null)
    {
        _answer = answer ?? Echo;
    }

    /// <summary>Methods of the notifications received, in order.</summary>
    public ConcurrentQueue<string> Notifications { get; } = new();

    /// <summary>Methods of the requests received, in order of arrival.</summary>
    public ConcurrentQueue<string> Requests { get; } = new();

    public Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken)
    {
        Requests.Enqueue(method + ":" + string.Join(",", Notifications));
        return _answer(method, payload, cancellationToken);
    }

    public void HandleNotification(string method, LinkPayload payload) => Notifications.Enqueue(method);

    /// <summary>Answers with the request's own body and attachments.</summary>
    public static Task<LinkReply> Echo(string method, LinkPayload payload, CancellationToken cancellationToken) =>
        Task.FromResult(new LinkReply(payload.Body, [.. payload.Attachments.Select(a => (ReadOnlyMemory<byte>)a)]));

    /// <summary>A JSON body with one string.</summary>
    public static JsonElement Text(string text) => JsonSerializer.SerializeToElement(text, TestJsonContext.Default.String);
}
