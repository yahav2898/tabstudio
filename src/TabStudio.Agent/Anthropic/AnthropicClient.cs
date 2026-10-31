using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.Agent.Anthropic;

/// <summary>Claude's API refused or failed a request. The message is written for the user.</summary>
public sealed class AnthropicException : Exception
{
    /// <summary>Creates the exception.</summary>
    public AnthropicException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public AnthropicException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public AnthropicException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for an error the API reported.</summary>
    /// <param name="statusCode">The HTTP status, if there was a response.</param>
    /// <param name="errorType">The API's error type, such as <c>authentication_error</c>.</param>
    /// <param name="message">What went wrong.</param>
    public AnthropicException(HttpStatusCode? statusCode, string? errorType, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
    }

    /// <summary>The HTTP status, if there was a response.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>The API's error type, such as <c>authentication_error</c> or <c>overloaded_error</c>.</summary>
    public string? ErrorType { get; }

    /// <summary>The API key is missing or wrong.</summary>
    public bool IsAuthenticationError => StatusCode == HttpStatusCode.Unauthorized || ErrorType == "authentication_error";
}

/// <summary>
/// A client of Claude's Messages API that streams answers: text arrives as it is written, and the complete
/// message, with any tool calls, is returned at the end.
/// </summary>
public sealed class AnthropicClient : IAgentClient
{
    /// <summary>The API's address.</summary>
    public static readonly Uri DefaultAddress = new("https://api.anthropic.com/");

    /// <summary>The model the agent uses unless the user picks another.</summary>
    public const string DefaultModel = "claude-opus-5-5";

    private const string ApiVersion = "2023-06-01";
    private const int MaxAttempts = 4;

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly Uri _address;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary>Creates a client.</summary>
    /// <param name="http">The HTTP client. Its timeout should allow long answers.</param>
    /// <param name="apiKey">An API key from the Claude Console.</param>
    /// <param name="address">The API's address; Anthropic's by default.</param>
    public AnthropicClient(HttpClient http, string apiKey, Uri? address = null)
        : this(http, apiKey, address, Task.Delay)
    {
    }

    /// <summary>Creates a client with a custom way to wait between retries, for tests.</summary>
    internal AnthropicClient(HttpClient http, string apiKey, Uri? address, Func<TimeSpan, CancellationToken, Task> delay)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _http = http;
        _apiKey = apiKey.Trim();
        _address = address ?? DefaultAddress;
        _delay = delay;
    }

    /// <summary>
    /// Sends a request and streams the answer. Overloaded or rate-limited requests are retried with backoff.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="onText">Receives text as Claude writes it.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The complete answer.</returns>
    public async Task<MessageResponse> SendAsync(MessageRequest request, Action<string>? onText = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = MessageSerializer.Serialize(request, stream: true).ToJsonString();

        for (var attempt = 1; ; attempt++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(_address, "v1/messages"))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            message.Headers.Add("x-api-key", _apiKey);
            message.Headers.Add("anthropic-version", ApiVersion);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new AnthropicException($"Could not reach Claude: {ex.Message}", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The client's own timeout, not the user's Stop: it was shown as "Stopped." as if they had pressed it.
                throw new AnthropicException("Claude did not answer in time. Check the network and try again.", ex);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    return await MessageStreamReader.ReadAsync(stream, onText, cancellationToken).ConfigureAwait(false);
                }

                var error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                if (attempt < MaxAttempts && IsRetryable(response.StatusCode))
                {
                    await _delay(RetryDelay(response, attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw error;
            }
        }
    }

    private static bool IsRetryable(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout || (int)status == 529;

    private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.TryGetValues("retry-after", out var values)
            && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(Math.Pow(2, attempt));
    }

    private static async Task<AnthropicException> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? type = null;
        string? message = null;
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var error = JsonNode.Parse(text)?["error"];
            type = error?["type"]?.GetValue<string>();
            message = error?["message"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            // Not every failure has a JSON body.
        }

        var description = type switch
        {
            "authentication_error" => "Claude did not accept the API key. Check it in the agent's settings.",
            "permission_error" => $"The API key may not use this model or feature: {message}",
            "not_found_error" => $"Claude's API does not know this model or address: {message}",
            "rate_limit_error" => "The API key's rate limit is reached. Try again in a minute.",
            "overloaded_error" => "Claude is overloaded right now. Try again in a minute.",
            "request_too_large" or "invalid_request_error" when response.StatusCode == HttpStatusCode.RequestEntityTooLarge => "The conversation is too long. Start a new one.",
            _ => message ?? $"Claude's API answered {(int)response.StatusCode} {response.ReasonPhrase}.",
        };
        return new AnthropicException(response.StatusCode, type, description);
    }
}
