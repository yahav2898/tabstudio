using System.Text.RegularExpressions;

namespace TabStudio.Web.WebAssembly;

/// <summary>
/// Fills in the placeholders of a WebAssembly app's start page the way the SDK does when
/// <c>OverrideHtmlAssetPlaceholders</c> is on, as in the Blazor WebAssembly template's <c>index.html</c>.
/// </summary>
/// <remarks>
/// TabStudio serves every file under its own name, without the fingerprints the SDK adds to names for caching, and
/// sends <c>Cache-Control: no-cache</c> instead. So a fingerprint placeholder (<c>blazor.webassembly#[.{fingerprint}].js</c>)
/// becomes the plain name, the import map maps nothing, and the preload link names <c>dotnet.js</c>.
/// </remarks>
public static partial class HtmlAssetPlaceholders
{
    /// <summary>Fills in a page's placeholders.</summary>
    /// <param name="html">The page.</param>
    /// <param name="frameworkPath">Where the runtime is served, such as <c>_framework</c>.</param>
    /// <param name="dotnetJsIntegrity">The integrity of the served <c>dotnet.js</c>, for its preload link.</param>
    public static string Apply(string html, string frameworkPath, string dotnetJsIntegrity)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(frameworkPath);
        ArgumentNullException.ThrowIfNull(dotnetJsIntegrity);

        var withNames = Fingerprint().Replace(html, string.Empty);
        var withImportMap = EmptyImportMap().Replace(withNames, match => $"{match.Groups["open"].Value}{{ \"imports\": {{}} }}{match.Groups["close"].Value}");
        return PreloadLink().Replace(
            withImportMap,
            _ => $"<link href=\"{frameworkPath}/dotnet.js\" rel=\"preload\" as=\"script\" fetchpriority=\"high\" crossorigin=\"anonymous\" integrity=\"{dotnetJsIntegrity}\" />");
    }

    /// <summary><c>#[.{fingerprint}]</c>, and its variants with another separator, such as <c>#[{fingerprint}]?</c>.</summary>
    [GeneratedRegex(@"#\[[^\]\{]*\{fingerprint\}[^\]]*\]\??")]
    private static partial Regex Fingerprint();

    /// <summary>An empty import map, <c>&lt;script type="importmap"&gt;&lt;/script&gt;</c>.</summary>
    [GeneratedRegex(@"(?<open><script\s+type=""importmap""\s*>)\s*(?<close></script>)", RegexOptions.IgnoreCase)]
    private static partial Regex EmptyImportMap();

    /// <summary>The preload placeholder, <c>&lt;link rel="preload" id="webassembly" /&gt;</c>.</summary>
    [GeneratedRegex(@"<link\s+rel=""preload""\s+id=""webassembly""\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex PreloadLink();
}
