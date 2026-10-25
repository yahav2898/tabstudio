using TabStudio.Web.StaticWebAssets;

namespace TabStudio.Web.WebAssembly;

/// <summary>
/// A .NET app that runs in the browser on .NET for WebAssembly: its assemblies, the runtime, and the web content it is
/// served with. Hand it to a <see cref="WebAssemblyAppHost"/> to show it in the web preview, or to
/// <see cref="WebAssemblyBundle.Create"/> for the files another server should serve. Blazor WebAssembly is one such
/// app; any app built for <c>browser-wasm</c>, such as an OpenSilver or Uno Platform app, is another.
/// </summary>
public sealed record WebAssemblyApp
{
    /// <summary>The entry assembly's simple name, such as <c>BlazorApp1</c>; the runtime runs its <c>Main</c>.</summary>
    public required string MainAssemblyName { get; init; }

    /// <summary>
    /// The app's assemblies: its own, the projects it references and its packages'. The framework's come from
    /// <see cref="Runtime"/>, as many as these reference.
    /// </summary>
    public required IReadOnlyList<WebAssemblyAssembly> Assemblies { get; init; }

    /// <summary>The runtime it runs on.</summary>
    public required BrowserRuntime Runtime { get; init; }

    /// <summary>The files served with it as they are, such as its <c>wwwroot</c>.</summary>
    public IReadOnlyList<StaticWebAsset> Content { get; init; } = [];

    /// <summary>
    /// The route of the page that starts the app, such as <c>index.html</c>: it is served for the site's root and for
    /// any path without a file extension, the app's own routes. Its asset placeholders are filled in, as the SDK does.
    /// <see langword="null"/> when another server renders the pages, as for the client of a Blazor Web App.
    /// </summary>
    public string? StartPage { get; init; }

    /// <summary>The hosting environment the app sees, <c>Development</c> by default as when Visual Studio starts it.</summary>
    public string ApplicationEnvironment { get; init; } = "Development";

    /// <summary>Whether the app runs without culture data, as the project's <c>InvariantGlobalization</c> asks.</summary>
    public bool InvariantGlobalization { get; init; }

    /// <summary>Whether all of ICU's data is loaded rather than the shard for the user's culture.</summary>
    public bool LoadAllGlobalizationData { get; init; }

    /// <summary>The routes of the configuration files the app reads at startup, such as <c>appsettings.json</c>.</summary>
    public IReadOnlyList<string> AppSettings { get; init; } = [];

    /// <summary>The boot configuration's extensions, such as <c>blazor</c> for a Blazor app.</summary>
    public IReadOnlyList<string> BootExtensions { get; init; } = [];

    /// <summary>The runtime configuration properties, such as feature switches, as the app's <c>runtimeconfig.json</c> would have them.</summary>
    public IReadOnlyDictionary<string, object> RuntimeProperties { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// The routes of the app's JavaScript initializers, such as <c>_content/Library/Library.lib.module.js</c>, which Blazor
    /// and the runtime call while the app starts.
    /// </summary>
    public IReadOnlyList<string> LibraryInitializers { get; init; } = [];

    /// <summary>Where the runtime and the assemblies are served, <c>_framework</c> as everywhere in .NET.</summary>
    public string FrameworkPath { get; init; } = "_framework";
}

/// <summary>An assembly of a WebAssembly app: a file, or an image compiled in memory with its symbols.</summary>
public sealed record WebAssemblyAssembly
{
    /// <summary>The assembly's simple name, such as <c>BlazorApp1</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The assembly file, when it is on disk.</summary>
    public string? FilePath { get; init; }

    /// <summary>The assembly's image, when it was compiled in memory.</summary>
    public ReadOnlyMemory<byte>? Image { get; init; }

    /// <summary>Its portable PDB, when it has one, which the runtime uses for line numbers in stack traces.</summary>
    public ReadOnlyMemory<byte>? Symbols { get; init; }

    /// <summary>An assembly file, such as a package's.</summary>
    /// <param name="path">The file.</param>
    public static WebAssemblyAssembly FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return new WebAssemblyAssembly { Name = Path.GetFileNameWithoutExtension(path), FilePath = path };
    }

    /// <summary>An assembly compiled in memory.</summary>
    /// <param name="name">Its simple name.</param>
    /// <param name="image">Its image.</param>
    /// <param name="symbols">Its portable PDB, if any.</param>
    public static WebAssemblyAssembly FromImage(string name, byte[] image, byte[]? symbols)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(image);
        return new WebAssemblyAssembly { Name = name, Image = image, Symbols = symbols };
    }
}
