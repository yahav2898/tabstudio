using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace TabStudio.Agent.Local;

/// <summary>The llama-server completion protocol, used to compare transports and test protocol compatibility.</summary>
public sealed class LoopbackCompletionClient : ILocalTextGenerator, IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _address;

    /// <summary>Creates a client restricted to numeric loopback. Redirects, cookies and proxies are disabled.</summary>
    public LoopbackCompletionClient(Uri address, HttpMessageHandler? testTransport = null)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.Scheme != "http" || !IPAddress.TryParse(address.Host, out var ip) || !IPAddress.IsLoopback(ip) || !string.IsNullOrEmpty(address.UserInfo))
            throw new ArgumentException("Local inference requires a numeric HTTP loopback address.", nameof(address));
        _http = new HttpClient(testTransport ?? new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false });
        _address = new Uri(address, "/completion");
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    /// <inheritdoc />
    public async Task<LocalCompletion> GenerateAsync(string prompt, int maxTokens, Action<string>? onText = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTokens);
        var request = new JsonObject { ["prompt"] = prompt, ["n_predict"] = maxTokens, ["temperature"] = 0, ["stream"] = false, ["cache_prompt"] = false };
        using var response = await _http.PostAsJsonAsync(_address, request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("Empty completion response.");
        var text = json["content"]?.GetValue<string>() ?? throw new InvalidDataException("Missing completion content.");
        onText?.Invoke(text);
        return new LocalCompletion(text, json["tokens_predicted"]?.GetValue<int>() ?? 0, json["timings"]?["predicted_per_second"]?.GetValue<double>() ?? 0);
    }
}
