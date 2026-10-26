using System.Globalization;
using System.Net;
using System.Text;
using TabStudio.Compilation.Running;

namespace TabStudio.Web.Serving;

/// <summary>A request a <see cref="LocalWebServer"/> answered.</summary>
/// <param name="Method">The HTTP method: <c>GET</c> or <c>HEAD</c>, or <c>POST</c> for a message the page posted.</param>
/// <param name="Path">The path, without the leading <c>/</c> and unescaped, such as <c>_framework/dotnet.js</c>.</param>
/// <param name="StatusCode">The status it was answered with, such as 200, 304 or 404.</param>
/// <param name="UserAgent">The browser that asked, from its <c>User-Agent</c> header, when it sent one.</param>
public sealed record ServedRequest(string Method, string Path, int StatusCode, string? UserAgent = null);

/// <summary>
/// A small static web server on a local port, for the web preview: it answers <c>GET</c> and <c>HEAD</c> with the
/// files a resolver returns, with their media type, an ETag and <c>Cache-Control: no-cache</c>, so the browser checks
/// every file again on reload and gets a new build's files without serving stale ones, and answers 304 when the file
/// did not change.
/// </summary>
/// <remarks>
/// It listens on <c>127.0.0.1</c> only: nothing outside the device can reach it.
/// </remarks>
public sealed class LocalWebServer : IAsyncDisposable
{
    /// <summary>The largest message a page may post, so a page cannot fill the IDE's memory.</summary>
    private const int MaxPostLength = 1024 * 1024;

    private readonly HttpListener _listener;
    private readonly Func<string, ServedFile?> _resolve;
    private readonly Func<string, string, bool>? _receive;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accepting;

    private LocalWebServer(HttpListener listener, Uri address, Func<string, ServedFile?> resolve, Func<string, string, bool>? receive)
    {
        _listener = listener;
        _resolve = resolve;
        _receive = receive;
        Address = address;
        _accepting = Task.Run(AcceptAsync);
    }

    /// <summary>Raised on a thread-pool thread after each request is answered.</summary>
    public event EventHandler<ServedRequest>? RequestServed;

    /// <summary>Where it listens, such as <c>http://127.0.0.1:52713/</c>.</summary>
    public Uri Address { get; }

    /// <summary>Starts serving.</summary>
    /// <param name="resolve">
    /// Returns the file for a path (without the leading <c>/</c>, unescaped, empty for the site's root), or
    /// <see langword="null"/> for a 404. Called on thread-pool threads, concurrently.
    /// </param>
    /// <param name="preferredPort">A port to listen on when it is free, so a restarted app keeps its address; any free port otherwise.</param>
    public static LocalWebServer Start(Func<string, ServedFile?> resolve, int? preferredPort = null) => Start(resolve, receive: null, preferredPort);

    /// <summary>Starts serving, and takes the messages a page posts.</summary>
    /// <param name="resolve">Returns the file for a path, as for <see cref="Start(Func{string, ServedFile?}, int?)"/>.</param>
    /// <param name="receive">
    /// Takes a <c>POST</c>'s path (without the leading <c>/</c>) and its body as UTF-8 text, and returns whether it
    /// was for it; the page gets 204, or 405 when the route does not accept messages. Called on thread-pool threads, concurrently.
    /// <see langword="null"/> answers every <c>POST</c> with 405.
    /// </param>
    /// <param name="preferredPort">A port to listen on when it is free.</param>
    public static LocalWebServer Start(Func<string, ServedFile?> resolve, Func<string, string, bool>? receive, int? preferredPort)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        HttpListenerException? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var port = attempt == 0 && preferredPort is { } preferred ? preferred : WebAppHost.FindFreePort();
            var address = new Uri($"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/");
            var listener = new HttpListener { IgnoreWriteExceptions = true };
            listener.Prefixes.Add(address.ToString());
            try
            {
                listener.Start();
                return new LocalWebServer(listener, address, resolve, receive);
            }
            catch (HttpListenerException ex)
            {
                // The port was taken between finding it and listening on it, or the preferred one is in use.
                listener.Close();
                last = ex;
            }
        }

        throw new IOException("TabStudio could not listen on a local port for the web preview.", last);
    }

    /// <summary>Stops listening. Requests being answered are cut off.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        try
        {
            await _accepting.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped while waiting for a request.
        }

        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => AnswerAsync(context));
        }
    }

    private async Task AnswerAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = Uri.UnescapeDataString(request.Url?.AbsolutePath ?? "/").TrimStart('/');
        var status = 500;
        try
        {
            status = await RespondAsync(request, response, path).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The browser went away, or the file changed while it was being sent; it asks again on reload.
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException or InvalidOperationException)
            {
                // Already closed by the browser.
            }
        }

        RequestServed?.Invoke(this, new ServedRequest(request.HttpMethod, path, status, request.UserAgent));
    }

    private async Task<int> RespondAsync(HttpListenerRequest request, HttpListenerResponse response, string path)
    {
        var isHead = string.Equals(request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase);
        if (_receive is not null && string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return await ReceiveAsync(request, response, path, _receive).ConfigureAwait(false);
        }

        if (!isHead && !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 405;
            response.Headers["Allow"] = "GET, HEAD";
            return 405;
        }

        var file = _resolve(path);
        if (file is null)
        {
            await WriteTextAsync(response, 404, $"Not found: /{path}", isHead).ConfigureAwait(false);
            return 404;
        }

        var etag = $"\"{file.Hash}\"";
        response.ContentType = file.ContentType;
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["ETag"] = etag;
        if (IsUnchanged(request.Headers["If-None-Match"], etag))
        {
            response.StatusCode = 304;
            return 304;
        }

        response.StatusCode = 200;
        response.ContentLength64 = file.Length;
        if (!isHead)
        {
            await file.CopyToAsync(response.OutputStream, _stop.Token).ConfigureAwait(false);
        }

        return 200;
    }

    /// <summary>Hands a posted message to the receiver: 204 when it took it, 405 when the route does not accept it, 413 when too long.</summary>
    private async Task<int> ReceiveAsync(HttpListenerRequest request, HttpListenerResponse response, string path, Func<string, string, bool> receive)
    {
        if (request.ContentLength64 > MaxPostLength)
        {
            response.StatusCode = 413;
            return 413;
        }

        using var body = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await request.InputStream.ReadAsync(buffer, _stop.Token).ConfigureAwait(false)) > 0)
        {
            if (body.Length + read > MaxPostLength)
            {
                response.StatusCode = 413;
                return 413;
            }

            body.Write(buffer, 0, read);
        }

        var status = receive(path, Encoding.UTF8.GetString(body.GetBuffer(), 0, (int)body.Length)) ? 204 : 405;
        response.StatusCode = status;
        if (status == 405)
        {
            response.Headers["Allow"] = "GET, HEAD";
        }

        response.Headers["Cache-Control"] = "no-cache";
        return status;
    }

    /// <summary>Whether the browser's copy, named by its <c>If-None-Match</c> header, is the current one.</summary>
    private static bool IsUnchanged(string? ifNoneMatch, string etag)
    {
        if (string.IsNullOrEmpty(ifNoneMatch))
        {
            return false;
        }

        foreach (var candidate in ifNoneMatch.Split(',', StringSplitOptions.TrimEntries))
        {
            var tag = candidate.StartsWith("W/", StringComparison.Ordinal) ? candidate[2..] : candidate;
            if (tag == "*" || string.Equals(tag, etag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private async Task WriteTextAsync(HttpListenerResponse response, int status, string text, bool isHead)
    {
        var body = Encoding.UTF8.GetBytes(text);
        response.StatusCode = status;
        response.ContentType = "text/plain; charset=utf-8";
        response.Headers["Cache-Control"] = "no-cache";
        response.ContentLength64 = body.Length;
        if (!isHead)
        {
            await response.OutputStream.WriteAsync(body, _stop.Token).ConfigureAwait(false);
        }
    }
}
