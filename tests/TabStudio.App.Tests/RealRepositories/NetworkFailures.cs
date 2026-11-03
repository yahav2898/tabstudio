using System.Text.RegularExpressions;
using TabStudio.Core.Diagnostics;
using TabStudio.Packages;

namespace TabStudio.App.Tests.RealRepositories;

/// <summary>
/// Tells a repository that failed because the network did from one that regressed, so the real-repository gate tries
/// the first kind once more instead of failing the job on a blip at nuget.org, GitHub or a package registry.
/// </summary>
internal static partial class NetworkFailures
{
    /// <summary>Whether a diagnostic says a package source could not be read: NU1301, or TabStudio's TS3001 from a restore.</summary>
    public static bool IsNetworkDiagnostic(BuildDiagnostic diagnostic) =>
        diagnostic.Id == PackageDiagnosticIds.SourceFailed
        || (diagnostic.Id == PackageDiagnosticIds.NetworkUnreachable
            && (diagnostic.Message.Contains("Could not reach", StringComparison.Ordinal)
                || diagnostic.Message.Contains("did not answer in time", StringComparison.Ordinal)
                || diagnostic.Message.Contains("could not be downloaded", StringComparison.Ordinal)));

    /// <summary>
    /// The first line of a toolchain's output that says the network failed, as pip, uv, npm, Cargo, Maven, Gradle,
    /// Composer, SwiftPM, Go and git word it; <see langword="null"/> when there is none.
    /// </summary>
    public static string? FindInOutput(string text)
    {
        var match = NetworkLine().Match(text);
        return match.Success ? match.Value.Trim() : null;
    }

    [GeneratedRegex(
        @"^.*(?:\b(?:NU1301|TS3001)\b|Could not resolve host|Temporary failure in name resolution|Name or service not known|getaddrinfo|EAI_AGAIN|ECONNRESET|ETIMEDOUT|ECONNREFUSED|socket hang up|Connection reset|Connection refused|Connection timed out|Read timed out|timed out while|unable to access 'https?://|RPC failed|early EOF|\b(?:502 Bad Gateway|503 Service Unavailable|504 Gateway Time-?out|429 Too Many Requests)\b|spurious network error|Could not transfer artifact|Could not (?:GET|HEAD) 'https?://|failed to download from|TLS handshake|SSL_ERROR|The remote end hung up).*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NetworkLine();
}
