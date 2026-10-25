using System.Collections.Concurrent;
using TabStudio.Web.Serving;

namespace TabStudio.Web.WebAssembly;

/// <summary>
/// Serves a .NET WebAssembly app on a local port, for the web preview: the runtime, the boot configuration, the
/// assemblies and the app's web content, with the right media types and no stale caching. The app itself runs in the
/// browser, on .NET for WebAssembly.
/// </summary>
/// <remarks>
/// <para>
/// This is the host for every app TabStudio runs in the browser: Blazor WebAssembly, and any other app built for
/// <c>browser-wasm</c>, such as an OpenSilver or Uno Platform app. Describe the app with a <see cref="WebAssemblyApp"/>,
/// start it with <see cref="StartAsync"/>, show <see cref="Address"/> in the web preview, and after a new build call
/// <see cref="UpdateAsync"/> and reload the preview: the address stays the same.
/// </para>
/// <para>
/// A path that is no file and has no extension, such as <c>/counter</c>, is answered with the start page, so the app's
/// own router shows the page, as the development server of the SDK does. Debugging the app is not supported.
/// </para>
/// </remarks>
public sealed class WebAssemblyAppHost : IAsyncDisposable
{
    private readonly LocalWebServer _server;
    private readonly ConcurrentDictionary<string, byte> _served = new(StringComparer.OrdinalIgnoreCase);
    private WebAssemblyBundle _bundle;

    private WebAssemblyAppHost(WebAssemblyBundle bundle, int? preferredPort)
    {
        _bundle = bundle;
        _server = LocalWebServer.Start(Resolve, Receive, preferredPort);
        _server.RequestServed += OnRequestServed;
    }

    /// <summary>Raised on a thread-pool thread after each request is answered.</summary>
    public event EventHandler<ServedRequest>? RequestServed;

    /// <summary>
    /// Raised on a thread-pool thread when the app's page posts a message to a route under <see cref="MessageRoute"/>,
    /// such as what the app writes to its console, for the IDE.
    /// </summary>
    public event EventHandler<PostedMessage>? MessageReceived;

    /// <summary>The routes the app's page posts its messages to, such as <c>_tabstudio/console</c>.</summary>
    public const string MessageRoute = "_tabstudio/";

    /// <summary>Where the app is served, such as <c>http://127.0.0.1:52713/</c>.</summary>
    public Uri Address => _server.Address;

    /// <summary>The files being served.</summary>
    public WebAssemblyBundle Bundle => Volatile.Read(ref _bundle);

    /// <summary>Bundles an app and starts serving it.</summary>
    /// <param name="app">The app.</param>
    /// <param name="preferredPort">A port to serve on when it is free, such as the one of the app's previous run, so the preview keeps its address.</param>
    /// <param name="cancellationToken">Cancels bundling.</param>
    /// <exception cref="InvalidDataException">The runtime is not .NET 10 or later.</exception>
    /// <exception cref="IOException">No local port could be listened on.</exception>
    public static async Task<WebAssemblyAppHost> StartAsync(WebAssemblyApp app, int? preferredPort = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        var bundle = await Task.Run(() => WebAssemblyBundle.Create(app), cancellationToken).ConfigureAwait(false);
        return new WebAssemblyAppHost(bundle, preferredPort);
    }

    /// <summary>
    /// Serves a new build of the app from now on, at the same address. Reload the page to run it; requests already
    /// being answered finish with the old files.
    /// </summary>
    /// <param name="app">The new build.</param>
    /// <param name="cancellationToken">Cancels bundling; the old build stays served.</param>
    public async Task UpdateAsync(WebAssemblyApp app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        var bundle = await Task.Run(() => WebAssemblyBundle.Create(app), cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _bundle, bundle);
        _served.Clear();
    }

    /// <summary>Whether a file was served with status 200 or 304 since the app started or was last updated.</summary>
    /// <param name="route">The route, such as <c>_framework/dotnet.native.wasm</c>.</param>
    public bool HasServed(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return _served.ContainsKey(route.TrimStart('/'));
    }

    /// <summary>Stops serving.</summary>
    public async ValueTask DisposeAsync()
    {
        _server.RequestServed -= OnRequestServed;
        await _server.DisposeAsync().ConfigureAwait(false);
    }

    private ServedFile? Resolve(string path)
    {
        var bundle = Bundle;
        if (path.Length == 0 || path.EndsWith('/'))
        {
            return bundle.Find(path + "index.html") ?? (bundle.StartPage is { } root ? bundle.Find(root) : null);
        }

        if (bundle.Find(path) is { } file)
        {
            return file;
        }

        // A route of the app, such as /counter: the start page, whose router shows it in the browser.
        var name = path[(path.LastIndexOf('/') + 1)..];
        return bundle.StartPage is { } page && !name.Contains('.', StringComparison.Ordinal) ? bundle.Find(page) : null;
    }

    private bool Receive(string path, string body)
    {
        if (!path.StartsWith(MessageRoute, StringComparison.Ordinal) || MessageReceived is not { } handler)
        {
            return false;
        }

        handler(this, new PostedMessage(path, body));
        return true;
    }

    private void OnRequestServed(object? sender, ServedRequest request)
    {
        if (request.StatusCode is 200 or 304)
        {
            _served.TryAdd(request.Path, 0);
        }

        RequestServed?.Invoke(this, request);
    }
}
