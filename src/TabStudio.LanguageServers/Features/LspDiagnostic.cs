using System.Text.Json;
using TabStudio.LanguageServers.Documents;

namespace TabStudio.LanguageServers.Features;

/// <summary>An error, warning or hint a server found in a file.</summary>
public sealed class LspDiagnostic
{
    internal LspDiagnostic(JsonElement raw)
    {
        Raw = raw;
    }

    /// <summary>Full path of the file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Where it is, as offsets into the file's text of the version it was found in.</summary>
    public required TextSpan Span { get; init; }

    /// <summary>One-based line of the start, as shown to the user.</summary>
    public required int Line { get; init; }

    /// <summary>One-based column of the start, as shown to the user.</summary>
    public required int Column { get; init; }

    /// <summary>How serious it is.</summary>
    public LspDiagnosticSeverity Severity { get; init; }

    /// <summary>The server's code, such as <c>reportUndefinedVariable</c> or <c>2304</c>, if any.</summary>
    public string? Code { get; init; }

    /// <summary>A page that explains the code, if the server gives one.</summary>
    public Uri? CodeLink { get; init; }

    /// <summary>Who found it, such as <c>Pyright</c> or <c>ts</c>, if the server says.</summary>
    public string? Source { get; init; }

    /// <summary>The message.</summary>
    public required string Message { get; init; }

    /// <summary>Whether it marks code that is not needed, such as an unused import, which editors fade.</summary>
    public bool IsUnnecessary { get; init; }

    /// <summary>Whether it marks the use of something deprecated.</summary>
    public bool IsDeprecated { get; init; }

    /// <summary>The JSON the server sent, which code action requests send back.</summary>
    internal JsonElement Raw { get; }
}
