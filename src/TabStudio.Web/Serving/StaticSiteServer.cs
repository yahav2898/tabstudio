using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Channels;
using TabStudio.Compilation.Running;
using TabStudio.Web.StaticWebAssets;

namespace TabStudio.Web.Serving;

/// <summary>
/// Serves a static web site's folder on a local port, for the web preview, with live reload: every HTML page gets a
/// small script that listens for saved files, and <see cref="NotifyChanged"/> tells the open pages to reload, or,
/// for a style sheet, to swap it in place without a reload.
/// </summary>
/// <remarks>
/// It listens on <c>127.0.0.1</c> only, so nothing outside the device can reach it, answers only <c>GET</c> and
/// <c>HEAD</c>, never serves a file outside the folder or under a dot folder such as <c>.git</c>, and sends every
/// file with <c>Cache-Control: no-store</c>, so a reload always shows what was saved.
/// </remarks>
public sealed class StaticSiteServer : IAsyncDisposable
{
    /// <summary>The path of the live reload script the pages load.</summary>
    public const string ScriptPath = "__tabstudio/live-reload.js";

    /// <summary>The path of the live reload event stream (server-sent events) the script listens to.</summary>
    public const string EventsPath = "__tabstudio/live-reload";

    private const string DefaultDocument = "index.html";

    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    private static readonly byte[] ScriptTag = Encoding.UTF8.GetBytes($"<script src=\"/{ScriptPath}\"></script>\n");

    private static readonly byte[] Script = Encoding.UTF8.GetBytes("""
        // TabStudio's live reload: the page reloads when a file of the site is saved, and a saved style sheet is
        // swapped in place, without a reload.
        (() => {
          if (window.__tabStudioLiveReload) {
            return;
          }

          window.__tabStudioLiveReload = true;
          const events = new EventSource('/__tabstudio/live-reload');
          events.addEventListener('open', () => { window.__tabStudioLiveReloadReady = true; });
          events.addEventListener('reload', () => location.reload());
          events.addEventListener('css', event => {
            const changed = new URL(event.data, location.href).pathname;
            let swapped = false;
            for (const link of document.querySelectorAll('link[rel~="stylesheet"]')) {
              const address = new URL(link.href, location.href);
              if (address.pathname !== changed) {
                continue;
              }

              address.searchParams.set('tabstudio-reload', Date.now().toString());
              const next = link.cloneNode();
              next.href = address.href;
              next.addEventListener('load', () => link.remove(), { once: true });
              next.addEventListener('error', () => next.remove(), { once: true });
              link.after(next);
              swapped = true;
            }

            // A style sheet the page loads another way, such as with @import, needs the page reloaded.
            if (!swapped) {
              location.reload();
            }
          });
        })();

        """);

    private readonly HttpListener _listener;
    private readonly string _root;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<int, Channel<string>> _pages = new();
    private readonly Task _accepting;
    private int _nextPage;

    private StaticSiteServer(HttpListener listener, Uri address, string root)
    {
        _listener = listener;
        _root = root;
        Address = address;
        _accepting = Task.Run(AcceptAsync);
    }

    /// <summary>Raised on a thread pool thread after each request is answered, live reload's own ones aside.</summary>
    public event EventHandler<ServedRequest>? RequestServed;

    /// <summary>The site's address, such as <c>http://127.0.0.1:5173/</c>.</summary>
    public Uri Address { get; }

    /// <summary>The folder it serves.</summary>
    public string RootDirectory => _root;

    /// <summary>How many pages listen for changes now: the open pages of the site, in any browser on the device.</summary>
    public int ListeningPages => _pages.Count;

    /// <summary>Starts serving a folder on a free local port.</summary>
    /// <param name="rootDirectory">The site's folder, with its <c>index.html</c>.</param>
    /// <param name="preferredPort">The port to try first, such as the one the site had before, so the preview keeps its address.</param>
    /// <returns>The running server.</returns>
    /// <exception cref="IOException">No local port could be listened on.</exception>
    public static StaticSiteServer Start(string rootDirectory, int? preferredPort = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar);
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
                return new StaticSiteServer(listener, address, root);
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

    /// <summary>
    /// Tells the open pages that a file was saved: a style sheet is swapped in place, and anything else reloads
    /// the page. Files outside the site are ignored.
    /// </summary>
    /// <param name="filePath">The saved file.</param>
    /// <returns>Whether the file is part of the site, so the pages were told.</returns>
    public bool NotifyChanged(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (RouteOf(filePath) is not { } route)
        {
            return false;
        }

        var message = Path.GetExtension(filePath).Equals(".css", StringComparison.OrdinalIgnoreCase)
            ? $"event: css\ndata: {route}\n\n"
            : $"event: reload\ndata: {route}\n\n";
        foreach (var page in _pages.Values)
        {
            page.Writer.TryWrite(message);
        }

        return true;
    }

    /// <summary>The address path of a file of the site, such as <c>/css/site.css</c>, or <see langword="null"/>.</summary>
    /// <param name="filePath">The file.</param>
    public string? RouteOf(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var full = Path.GetFullPath(filePath);
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        var segments = Path.GetRelativePath(_root, full).Split(Path.DirectorySeparatorChar);
        return "/" + string.Join('/', segments.Select(Uri.EscapeDataString));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        foreach (var page in _pages.Values)
        {
            page.Writer.TryComplete();
        }

        _listener.Close();
        try
        {
            await _accepting.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped while waiting for a request.
        }

        // The pages' event streams end as the listener closes.
        for (var waited = 0; !_pages.IsEmpty && waited < 50; waited++)
        {
            await Task.Delay(20).ConfigureAwait(false);
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
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            // The browser went away, or the server stopped; the page asks again when it reloads.
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

        if (!path.StartsWith("__tabstudio/", StringComparison.Ordinal))
        {
            RequestServed?.Invoke(this, new ServedRequest(request.HttpMethod, path, status, request.UserAgent));
        }
    }

    private async Task<int> RespondAsync(HttpListenerRequest request, HttpListenerResponse response, string path)
    {
        var isHead = string.Equals(request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase);
        if (!isHead && !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 405;
            response.Headers["Allow"] = "GET, HEAD";
            return 405;
        }

        response.Headers["Cache-Control"] = "no-store";
        switch (path)
        {
            case ScriptPath:
                await WriteAsync(response, 200, "text/javascript; charset=utf-8", Script, isHead).ConfigureAwait(false);
                return 200;
            case EventsPath:
                if (isHead)
                {
                    response.ContentType = "text/event-stream; charset=utf-8";
                    return 200;
                }

                await StreamChangesAsync(response).ConfigureAwait(false);
                return 200;
        }

        if (FileOf(path) is not { } file)
        {
            await WriteAsync(response, 404, "text/html; charset=utf-8", NotFoundPage(path), isHead).ConfigureAwait(false);
            return 404;
        }

        if (Directory.Exists(file))
        {
            // A folder's address ends with a slash, so that its page's relative links resolve inside it.
            response.StatusCode = 301;
            response.RedirectLocation = (request.Url?.AbsolutePath ?? "/" + path) + "/";
            return 301;
        }

        var contentType = ContentTypes.For(file);
        if (string.Equals(contentType, "text/html", StringComparison.Ordinal))
        {
            var page = WithScript(await File.ReadAllBytesAsync(file, _stop.Token).ConfigureAwait(false));
            await WriteAsync(response, 200, "text/html; charset=utf-8", page, isHead).ConfigureAwait(false);
            return 200;
        }

        response.StatusCode = 200;
        response.ContentType = ContentTypes.IsText(contentType) ? contentType + "; charset=utf-8" : contentType;
        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 81920, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            response.ContentLength64 = stream.Length;
            if (!isHead)
            {
                await stream.CopyToAsync(response.OutputStream, _stop.Token).ConfigureAwait(false);
            }
        }

        return 200;
    }

    /// <summary>
    /// The file or folder an address path names, with a folder's <c>index.html</c> for a path that ends with a
    /// slash; <see langword="null"/> when there is none, or the path leaves the site or enters a dot folder.
    /// </summary>
    private string? FileOf(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s.StartsWith('.') || s.Contains('\\', StringComparison.Ordinal)))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine([_root, .. segments]));
        if (full != _root && !full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        var current = _root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }
        }

        if (path.Length == 0 || path.EndsWith('/'))
        {
            full = Path.Combine(full, DefaultDocument);
            if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }
        }

        return File.Exists(full) || (Directory.Exists(full) && full != _root) ? full : null;
    }

    /// <summary>Sends live reload's events to one page until it closes or the server stops.</summary>
    private async Task StreamChangesAsync(HttpListenerResponse response)
    {
        var id = Interlocked.Increment(ref _nextPage);
        var changes = Channel.CreateUnbounded<string>();
        _pages[id] = changes;
        try
        {
            var stop = _stop.Token;
            response.StatusCode = 200;
            response.ContentType = "text/event-stream; charset=utf-8";
            response.SendChunked = true;
            var output = response.OutputStream;

            // The browser reconnects a second after the stream ends, such as when F5 starts the site again.
            await WriteEventAsync(output, "retry: 1000\n\n", stop).ConfigureAwait(false);
            Task<bool>? waiting = null;
            while (!stop.IsCancellationRequested)
            {
                waiting ??= changes.Reader.WaitToReadAsync(stop).AsTask();
                if (await Task.WhenAny(waiting, Task.Delay(KeepAliveInterval, stop)).ConfigureAwait(false) != waiting)
                {
                    // A comment keeps the connection open, and finds out when the page has gone.
                    stop.ThrowIfCancellationRequested();
                    await WriteEventAsync(output, ": keep-alive\n\n", stop).ConfigureAwait(false);
                    continue;
                }

                var more = await waiting.ConfigureAwait(false);
                waiting = null;
                if (!more)
                {
                    return;
                }

                while (changes.Reader.TryRead(out var change))
                {
                    await WriteEventAsync(output, change, stop).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _pages.TryRemove(id, out _);
        }
    }

    private static async Task WriteEventAsync(Stream output, string text, CancellationToken cancellationToken)
    {
        await output.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAsync(HttpListenerResponse response, int status, string contentType, byte[] body, bool isHead)
    {
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = body.Length;
        if (!isHead)
        {
            await response.OutputStream.WriteAsync(body, _stop.Token).ConfigureAwait(false);
        }
    }

    /// <summary>A page with the live reload script: before its <c>&lt;/body&gt;</c>, or at its end when it has none.</summary>
    /// <param name="page">The page, as UTF-8.</param>
    internal static byte[] WithScript(byte[] page)
    {
        var at = LastBodyEnd(page) is var body and >= 0 ? body : page.Length;
        var result = new byte[page.Length + ScriptTag.Length];
        page.AsSpan(0, at).CopyTo(result);
        ScriptTag.CopyTo(result.AsSpan(at));
        page.AsSpan(at).CopyTo(result.AsSpan(at + ScriptTag.Length));
        return result;
    }

    /// <summary>Where the last <c>&lt;/body&gt;</c> starts, in any case, or -1: in bytes, whatever the page's encoding.</summary>
    private static int LastBodyEnd(byte[] page)
    {
        ReadOnlySpan<byte> tag = "</body>"u8;
        for (var start = page.Length - tag.Length; start >= 0; start--)
        {
            var matches = true;
            for (var i = 0; i < tag.Length && matches; i++)
            {
                matches = (page[start + i] | 0x20) == (tag[i] | 0x20);
            }

            if (matches)
            {
                return start;
            }
        }

        return -1;
    }

    /// <summary>The 404 page, which reloads by itself once the missing page is saved.</summary>
    private static byte[] NotFoundPage(string path)
    {
        var shown = WebUtility.HtmlEncode("/" + path);
        var page = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Not found</title>
            <style>body { font: 15px system-ui, sans-serif; margin: 40px; color: #444; } code { color: #a31515; }</style></head>
            <body><h1>Not found</h1><p>The site has no <code>{{shown}}</code>. Save the file and this page shows it.</p></body>
            </html>
            """;
        return WithScript(Encoding.UTF8.GetBytes(page));
    }
}
