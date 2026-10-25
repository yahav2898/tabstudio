namespace TabStudio.Notebooks.Outputs;

/// <summary>MIME types that notebook outputs commonly use.</summary>
public static class MimeTypes
{
    /// <summary>Plain text, which every output should have so that any front end can show it.</summary>
    public const string PlainText = "text/plain";

    /// <summary>HTML.</summary>
    public const string Html = "text/html";

    /// <summary>Markdown.</summary>
    public const string Markdown = "text/markdown";

    /// <summary>JSON.</summary>
    public const string Json = "application/json";

    /// <summary>JavaScript, which Jupyter stores as lines like text.</summary>
    public const string JavaScript = "application/javascript";

    /// <summary>A PNG image, stored as base64.</summary>
    public const string Png = "image/png";

    /// <summary>An SVG image, stored as XML text.</summary>
    public const string Svg = "image/svg+xml";

    /// <summary>Whether values of a MIME type are JSON, which Jupyter stores as JSON rather than as a string.</summary>
    /// <param name="mimeType">The MIME type, such as <c>application/vnd.plotly.v1+json</c>.</param>
    public static bool IsJson(string mimeType)
    {
        ArgumentNullException.ThrowIfNull(mimeType);
        return mimeType == Json || mimeType.EndsWith("+json", StringComparison.Ordinal);
    }

    /// <summary>Whether values of a MIME type are text that Jupyter stores as a list of lines.</summary>
    /// <param name="mimeType">The MIME type.</param>
    public static bool IsMultilineText(string mimeType)
    {
        ArgumentNullException.ThrowIfNull(mimeType);
        return mimeType.StartsWith("text/", StringComparison.Ordinal) || mimeType is JavaScript or Svg;
    }
}
