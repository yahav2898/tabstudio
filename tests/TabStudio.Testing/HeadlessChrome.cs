using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TabStudio.Testing;

/// <summary>
/// A headless Chrome (or Chromium) page driven over the DevTools protocol, for tests that need a real browser: a
/// Blazor circuit that counts clicks, a WebAssembly app that boots, a screenshot of a rendered page.
/// </summary>
/// <remarks>
/// Chrome is found through <c>TABSTUDIO_CHROME</c> or on the <c>PATH</c> (<c>google-chrome</c>, <c>chromium</c>…).
/// Tests that need it use <see cref="ChromeFactAttribute"/>, so they are skipped where it is missing, except on CI.
/// </remarks>
public sealed class HeadlessChrome : IAsyncDisposable
{
    private static readonly string[] Candidates =
        ["google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "chrome", "msedge"];

    private readonly Process _process;
    private readonly TempDirectory _profile;
    private readonly ClientWebSocket _socket;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private int _nextId;

    private HeadlessChrome(Process process, TempDirectory profile, ClientWebSocket socket)
    {
        _process = process;
        _profile = profile;
        _socket = socket;
    }

    /// <summary>The browser to run, or <see langword="null"/> when none is installed.</summary>
    public static string? ExecutablePath { get; } = FindExecutable();

    /// <summary>Starts a browser with one blank page, sized like a landscape tablet.</summary>
    /// <param name="width">The page's width in CSS pixels.</param>
    /// <param name="height">The page's height in CSS pixels.</param>
    public static async Task<HeadlessChrome> StartAsync(int width = 1280, int height = 800)
    {
        var executable = ExecutablePath ?? throw new InvalidOperationException("Chrome or Chromium is not installed, or set TABSTUDIO_CHROME to it.");
        var profile = new TempDirectory();
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "--headless=new", "--no-sandbox", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
            "--remote-debugging-port=0", $"--user-data-dir={profile.Path}", $"--window-size={width},{height}", "about:blank",
        })
        {
            start.ArgumentList.Add(argument);
        }

        var process = Process.Start(start) ?? throw new InvalidOperationException($"{executable} did not start.");
        try
        {
            var browserEndpoint = await ReadDevToolsEndpointAsync(process);
            var page = await FindPageAsync(browserEndpoint);
            var socket = new ClientWebSocket();
            await socket.ConnectAsync(page, CancellationToken.None);
            var chrome = new HeadlessChrome(process, profile, socket);
            await chrome.SendAsync("Page.enable");
            await chrome.SendAsync("Runtime.enable");
            await chrome.ResizeAsync(width, height);
            return chrome;
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            process.Dispose();
            profile.Dispose();
            throw;
        }
    }

    /// <summary>Sizes the page, as a browser window of that size would.</summary>
    /// <param name="width">The width in CSS pixels.</param>
    /// <param name="height">The height in CSS pixels.</param>
    public Task ResizeAsync(int width, int height) =>
        SendAsync("Emulation.setDeviceMetricsOverride", new JsonObject
        {
            ["width"] = width,
            ["height"] = height,
            ["deviceScaleFactor"] = 1,
            ["mobile"] = false,
        });

    /// <summary>Opens a page and waits until it has loaded.</summary>
    /// <param name="address">The page.</param>
    public async Task NavigateAsync(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        await SendAsync("Page.navigate", new JsonObject { ["url"] = address.ToString() });
        await WaitUntilAsync("document.readyState === 'complete' && location.href !== 'about:blank'", TimeSpan.FromSeconds(30));
    }

    /// <summary>Evaluates JavaScript in the page and returns its value, awaiting a promise.</summary>
    /// <param name="expression">The expression.</param>
    public async Task<JsonNode?> EvaluateAsync(string expression)
    {
        var result = await SendAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = expression,
            ["returnByValue"] = true,
            ["awaitPromise"] = true,
        });
        if (result?["exceptionDetails"] is { } exception)
        {
            throw new InvalidOperationException($"The page threw: {exception.ToJsonString()}");
        }

        return result?["result"]?["value"];
    }

    /// <summary>The text of the first element a CSS selector matches, or <see langword="null"/>.</summary>
    /// <param name="selector">The selector.</param>
    public async Task<string?> TextOfAsync(string selector) =>
        (await EvaluateAsync($"document.querySelector({JsonSerializer.Serialize(selector)})?.textContent ?? null"))?.GetValue<string>();

    /// <summary>Clicks the first element a CSS selector matches.</summary>
    /// <param name="selector">The selector.</param>
    public Task ClickAsync(string selector) =>
        EvaluateAsync($"document.querySelector({JsonSerializer.Serialize(selector)}).click()");

    /// <summary>Waits until a JavaScript condition is true.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <exception cref="TimeoutException">The condition did not become true in time.</exception>
    public async Task WaitUntilAsync(string condition, TimeSpan timeout)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            if ((await EvaluateAsync($"!!({condition})"))?.GetValue<bool>() == true)
            {
                return;
            }

            if (waited.Elapsed > timeout)
            {
                var html = (await EvaluateAsync("document.documentElement.outerHTML"))?.GetValue<string>();
                throw new TimeoutException($"After {timeout.TotalSeconds:0} s, still not true: {condition}\nThe page:\n{html}");
            }

            await Task.Delay(100);
        }
    }

    /// <summary>A PNG of what the page shows.</summary>
    public async Task<byte[]> ScreenshotAsync()
    {
        var result = await SendAsync("Page.captureScreenshot", new JsonObject { ["format"] = "png" });
        return Convert.FromBase64String(result?["data"]?.GetValue<string>() ?? string.Empty);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or InvalidOperationException)
        {
            // The browser is killed below anyway.
        }

        _socket.Dispose();
        try
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }

        _process.Dispose();
        _sendGate.Dispose();
        _profile.Dispose();
    }

    /// <summary>Sends a DevTools command and returns its result.</summary>
    private async Task<JsonNode?> SendAsync(string method, JsonObject? parameters = null)
    {
        var id = Interlocked.Increment(ref _nextId);
        var message = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JsonObject() };
        await _sendGate.WaitAsync();
        try
        {
            await _socket.SendAsync(Encoding.UTF8.GetBytes(message.ToJsonString()), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (true)
            {
                var reply = await ReceiveAsync(timeout.Token);
                if (reply?["id"]?.GetValue<int>() != id)
                {
                    continue;
                }

                if (reply["error"] is { } error)
                {
                    throw new InvalidOperationException($"{method} failed: {error.ToJsonString()}");
                }

                return reply["result"];
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task<JsonNode?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(buffer, cancellationToken);
            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonNode.Parse(message.ToArray());
    }

    /// <summary>The browser's DevTools address, which it prints at startup.</summary>
    private static async Task<Uri> ReadDevToolsEndpointAsync(Process process)
    {
        const string Prefix = "DevTools listening on ";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (await process.StandardError.ReadLineAsync(timeout.Token) is { } line)
        {
            if (line.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return new Uri(line[Prefix.Length..].Trim());
            }
        }

        throw new InvalidOperationException("Chrome exited before it opened its DevTools port.");
    }

    /// <summary>The DevTools address of the browser's blank page.</summary>
    private static async Task<Uri> FindPageAsync(Uri browserEndpoint)
    {
        using var http = new HttpClient();
        var list = new Uri($"http://{browserEndpoint.Authority}/json/list");
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var targets = JsonNode.Parse(await http.GetStringAsync(list))?.AsArray() ?? new JsonArray();
            if (targets.FirstOrDefault(t => t?["type"]?.GetValue<string>() == "page")?["webSocketDebuggerUrl"]?.GetValue<string>() is { } page)
            {
                return new Uri(page);
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException("Chrome opened no page.");
    }

    private static string? FindExecutable()
    {
        if (Environment.GetEnvironmentVariable("TABSTUDIO_CHROME") is { Length: > 0 } configured && File.Exists(configured))
        {
            return configured;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in Candidates)
            {
                foreach (var file in new[] { Path.Combine(directory, name), Path.Combine(directory, name + ".exe") })
                {
                    if (File.Exists(file))
                    {
                        return file;
                    }
                }
            }
        }

        return null;
    }
}

/// <summary>A test that needs <see cref="HeadlessChrome"/>: skipped where no browser is installed, except on CI.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ChromeFactAttribute : FactAttribute
{
    /// <summary>Marks the test.</summary>
    public ChromeFactAttribute()
    {
        if (HeadlessChrome.ExecutablePath is null && !string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Chrome or Chromium is not installed; set TABSTUDIO_CHROME to run this test.";
        }
    }
}
