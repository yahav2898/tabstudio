namespace TabStudio.Web.StaticWebAssets;

/// <summary>
/// The media type each static file is served with, as ASP.NET Core's <c>FileExtensionContentTypeProvider</c> and the
/// SDK's <c>StaticWebAssetContentTypeMapping</c> items assign them. WebAssembly needs <c>application/wasm</c> to compile
/// a module while it downloads.
/// </summary>
public static class ContentTypes
{
    /// <summary>The media type of files TabStudio does not know.</summary>
    public const string Binary = "application/octet-stream";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".css"] = "text/css",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".json"] = "application/json",
        [".map"] = "application/json",
        [".webmanifest"] = "application/manifest+json",
        [".wasm"] = "application/wasm",
        [".xml"] = "text/xml",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".csv"] = "text/csv",
        [".svg"] = "image/svg+xml",
        [".svgz"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".bmp"] = "image/bmp",
        [".ico"] = "image/x-icon",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".otf"] = "font/otf",
        [".ttf"] = "application/x-font-ttf",
        [".eot"] = "application/vnd.ms-fontobject",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".oga"] = "audio/ogg",
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".webm"] = "video/webm",
        [".ogg"] = "video/ogg",
        [".ogv"] = "video/ogg",
        [".mov"] = "video/quicktime",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".gz"] = "application/x-gzip",
        [".dll"] = Binary,
        [".pdb"] = Binary,
        [".dat"] = Binary,
        [".blat"] = Binary,
    };

    /// <summary>The media type of a file, from its extension.</summary>
    /// <param name="path">A file name, route or path.</param>
    public static string For(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ByExtension.TryGetValue(Path.GetExtension(path), out var type) ? type : Binary;
    }

    /// <summary>Whether a media type is text, which is served with a UTF-8 charset where the browser expects one.</summary>
    /// <param name="contentType">The media type.</param>
    public static bool IsText(string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        return contentType.StartsWith("text/", StringComparison.Ordinal)
            || contentType is "application/json" or "application/manifest+json" or "image/svg+xml";
    }
}
