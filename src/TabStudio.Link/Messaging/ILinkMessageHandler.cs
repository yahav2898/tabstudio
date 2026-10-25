namespace TabStudio.Link.Messaging;

/// <summary>Answers the requests and handles the notifications that arrive on a <see cref="LinkConnection"/>.</summary>
public interface ILinkMessageHandler
{
    /// <summary>
    /// Answers a request. Runs on the thread pool, so requests are answered concurrently, but only after every
    /// notification received before the request was handled.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="payload">The request's body and attachments.</param>
    /// <param name="cancellationToken">Canceled when the other device cancels the request or the connection closes.</param>
    /// <returns>The answer. Throw <see cref="LinkRemoteException"/> to answer with a specific error.</returns>
    Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken);

    /// <summary>
    /// Handles a notification, such as an edit of a file, before the next message is read, so notifications and the
    /// requests after them are handled in the order they were sent. Keep it short.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="payload">The notification's body and attachments.</param>
    void HandleNotification(string method, LinkPayload payload);
}
