using System.Text.RegularExpressions;
using TabStudio.Agent.Anthropic;

namespace TabStudio.Agent;

/// <summary>The UI framework a sketch becomes XAML for.</summary>
public enum XamlDialect
{
    /// <summary>Avalonia.</summary>
    Avalonia,

    /// <summary>WPF.</summary>
    Wpf,

    /// <summary>.NET MAUI.</summary>
    Maui,

    /// <summary>WinUI 3 and the Uno Platform.</summary>
    WinUI,
}

/// <summary>A screen drawn as XAML.</summary>
/// <param name="Xaml">The XAML of the screen's content, without a window or page around it.</param>
/// <param name="Notes">What Claude assumed or left out, in a sentence or two; empty when nothing.</param>
public sealed record SketchResult(string Xaml, string Notes);

/// <summary>
/// Turns a drawing of a screen into XAML: the sketch goes to Claude as an image, and the layout comes back as
/// idiomatic XAML for the chosen framework.
/// </summary>
/// <param name="client">The Claude client.</param>
/// <param name="model">The model; Claude's most capable vision model by default.</param>
public sealed partial class SketchToXaml(AnthropicClient client, string model = AnthropicClient.DefaultModel)
{
    /// <summary>Turns a sketch into XAML.</summary>
    /// <param name="png">The sketch as a PNG image.</param>
    /// <param name="dialect">The framework to write XAML for.</param>
    /// <param name="hint">What the user said about the screen, such as "a login form", or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<SketchResult> ConvertAsync(ReadOnlyMemory<byte> png, XamlDialect dialect, string? hint = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        var framework = dialect switch
        {
            XamlDialect.Avalonia => "Avalonia 11 (xmlns=\"https://github.com/avaloniaui\")",
            XamlDialect.Wpf => "WPF (xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\")",
            XamlDialect.Maui => ".NET MAUI (xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\")",
            _ => "WinUI 3 (xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\")",
        };
        var request = $"""
            This is a hand-drawn sketch of an app screen. Write it as {framework} XAML.

            - Reproduce the layout, the controls and the text you can read. Use layout panels (Grid, StackPanel, DockPanel where it exists) rather than absolute positions.
            - Use the framework's standard controls and sensible names (x:Name) for inputs and buttons. No code-behind, no bindings to view models that do not exist, no styles beyond simple properties.
            - Answer with one ```xml code block holding a single root element for the screen's content (a panel such as Grid, not a Window or Page), followed by at most two sentences on anything you assumed.
            {(string.IsNullOrWhiteSpace(hint) ? string.Empty : $"\nThe user describes the screen as: {hint.Trim()}")}
            """;

        var response = await client.SendAsync(
            new MessageRequest(model, null, [new ChatMessage(MessageRole.User, [new ImageBlock("image/png", png), new TextBlock(request)])], [], MaxTokens: 8000),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Parse(response.Message.Text);
    }

    /// <summary>Takes the XAML out of Claude's answer, and the remarks after it.</summary>
    internal static SketchResult Parse(string answer)
    {
        var match = CodeBlock().Match(answer);
        if (!match.Success)
        {
            var trimmed = answer.Trim();
            return trimmed.StartsWith('<') ? new SketchResult(trimmed, string.Empty) : throw new AnthropicException("Claude did not answer with XAML. Try again, or add a hint about the screen.");
        }

        var notes = (answer[..match.Index] + answer[(match.Index + match.Length)..]).Trim();
        return new SketchResult(match.Groups["xaml"].Value.Trim(), notes);
    }

    [GeneratedRegex(@"```(?:xml|xaml)?\s*\n(?<xaml>.*?)```", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CodeBlock();
}
