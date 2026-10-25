namespace TabStudio.Uno.Hosting;

/// <summary>How Uno Platform starts in TabStudio.</summary>
public sealed record UnoRuntimeOptions
{
    /// <summary>Where the runtime keeps its files: the compiled host, and the content folder <c>ms-appx:///</c> resolves in.</summary>
    public required string CacheDirectory { get; init; }

    /// <summary>Whether TabStudio uses its dark theme, which apps see as the system theme.</summary>
    public bool DarkTheme { get; init; } = true;

    /// <summary>The preview's width, in device-independent pixels, until it is laid out.</summary>
    public double Width { get; init; } = 1024;

    /// <summary>The preview's height, in device-independent pixels, until it is laid out.</summary>
    public double Height { get; init; } = 640;

    /// <summary>Pixels per device-independent pixel.</summary>
    public double Scale { get; init; } = 1;
}
