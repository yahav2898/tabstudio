namespace TabStudio.Web;

/// <summary>The ids of the problems TabStudio reports for web apps, in the Error List.</summary>
public static class WebDiagnosticIds
{
    /// <summary>A scoped CSS file (<c>Counter.razor.css</c>) has something scoped CSS cannot have, such as an <c>@import</c>.</summary>
    public const string ScopedCss = "TS6001";

    /// <summary>A WebAssembly app could not be put together to be served, such as when .NET for WebAssembly is damaged.</summary>
    public const string WebAssemblyBundle = "TS6002";

    /// <summary>A Blazor Web App's WebAssembly client is missing, so its WebAssembly components will not start.</summary>
    public const string WebAssemblyClient = "TS6003";
}
