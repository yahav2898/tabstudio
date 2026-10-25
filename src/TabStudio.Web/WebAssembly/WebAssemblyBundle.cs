using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using TabStudio.Web.Serving;
using TabStudio.Web.StaticWebAssets;

namespace TabStudio.Web.WebAssembly;

/// <summary>
/// Every file a WebAssembly app is served with: the runtime, with the boot configuration in its <c>dotnet.js</c>, the
/// framework's and the app's assemblies as IL (without ahead-of-time compilation or trimming, as in a Debug build),
/// their symbols, and the app's web content with its start page's placeholders filled in.
/// </summary>
public sealed class WebAssemblyBundle
{
    private readonly Dictionary<string, ServedFile> _byRoute;
    private readonly Dictionary<string, ServedFile> _byRouteIgnoringCase;

    private WebAssemblyBundle(IReadOnlyList<ServedFile> files, string? startPage, string mainAssemblyName)
    {
        Files = files;
        StartPage = startPage;
        MainAssemblyName = mainAssemblyName;
        _byRoute = new Dictionary<string, ServedFile>(StringComparer.Ordinal);
        _byRouteIgnoringCase = new Dictionary<string, ServedFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            _byRoute.TryAdd(file.Route, file);
            _byRouteIgnoringCase.TryAdd(file.Route, file);
        }
    }

    /// <summary>The files, each under its route.</summary>
    public IReadOnlyList<ServedFile> Files { get; }

    /// <summary>The route of the start page, or <see langword="null"/> when another server renders the pages.</summary>
    public string? StartPage { get; }

    /// <summary>The entry assembly's simple name.</summary>
    public string MainAssemblyName { get; }

    /// <summary>Bundles an app.</summary>
    /// <param name="app">The app.</param>
    /// <exception cref="InvalidDataException">The runtime is not .NET 10 or later.</exception>
    public static WebAssemblyBundle Create(WebAssemblyApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var framework = StaticWebAsset.NormalizeRoute(app.FrameworkPath);
        var runtime = app.Runtime;
        var resources = new BootResources();

        resources.JsModuleNative.Add(ServedFile.FromFile($"{framework}/dotnet.native.js", runtime.NativeFile("dotnet.native.js")));
        resources.JsModuleRuntime.Add(ServedFile.FromFile($"{framework}/dotnet.runtime.js", runtime.NativeFile("dotnet.runtime.js")));
        resources.WasmNative.Add(ServedFile.FromFile($"{framework}/dotnet.native.wasm", runtime.NativeFile("dotnet.native.wasm")));
        foreach (var icu in IcuFiles(app))
        {
            resources.Icu.Add(ServedFile.FromFile($"{framework}/{icu}", runtime.NativeFile(icu)));
        }

        foreach (var assembly in ResolveAssemblies(app))
        {
            var route = $"{framework}/{assembly.Name}.dll";
            var file = assembly.Image is { } image ? ServedFile.FromContent(route, image) : ServedFile.FromFile(route, assembly.FilePath!);
            var isCore = BrowserRuntime.CoreAssemblyNames.Contains(assembly.Name, StringComparer.OrdinalIgnoreCase);
            (isCore ? resources.CoreAssemblies : resources.Assemblies).Add(file);
            if (SymbolsOf(assembly, $"{framework}/{assembly.Name}.pdb") is { } symbols)
            {
                resources.Symbols.Add(symbols);
            }
        }

        var bootConfig = WebAssemblyBootConfig.Create(app, resources);
        var dotnetJs = WebAssemblyBootConfig.MergeInto(File.ReadAllText(runtime.DotnetJsPath), bootConfig);
        var loader = ServedFile.FromContent($"{framework}/dotnet.js", Encoding.UTF8.GetBytes(dotnetJs));

        List<ServedFile> files =
        [
            loader,
            .. resources.JsModuleNative,
            .. resources.JsModuleRuntime,
            .. resources.WasmNative,
            .. resources.Icu,
            .. resources.CoreAssemblies,
            .. resources.Assemblies,
            .. resources.Symbols,
        ];

        var startPage = app.StartPage is null ? null : StaticWebAsset.NormalizeRoute(app.StartPage);
        foreach (var asset in app.Content.DistinctBy(a => a.Route, StringComparer.Ordinal))
        {
            if (startPage is not null && string.Equals(asset.Route, startPage, StringComparison.OrdinalIgnoreCase))
            {
                var html = HtmlAssetPlaceholders.Apply(File.ReadAllText(asset.FilePath), framework, loader.Integrity);
                files.Add(ServedFile.FromContent(asset.Route, Encoding.UTF8.GetBytes(html)));
                startPage = asset.Route;
            }
            else
            {
                files.Add(ServedFile.FromFile(asset.Route, asset.FilePath));
            }
        }

        return new WebAssemblyBundle(files, startPage, app.MainAssemblyName);
    }

    /// <summary>The file served at a route, matched exactly or else ignoring case, or <see langword="null"/>.</summary>
    /// <param name="route">The route, such as <c>_framework/dotnet.js</c>.</param>
    public ServedFile? Find(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var normalized = StaticWebAsset.NormalizeRoute(route);
        return _byRoute.TryGetValue(normalized, out var exact) ? exact
            : _byRouteIgnoringCase.TryGetValue(normalized, out var other) ? other
            : null;
    }

    /// <summary>
    /// Writes the files built in memory to a folder, so another server can serve the bundle from disk, such as the
    /// ASP.NET Core app a Blazor WebAssembly client belongs to. A file whose content did not change is not written
    /// again, so its date stays.
    /// </summary>
    /// <param name="directory">The folder; each file goes to its route under it.</param>
    /// <returns>Every file of the bundle as a static web asset.</returns>
    public IReadOnlyList<StaticWebAsset> WriteTo(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        var assets = new List<StaticWebAsset>(Files.Count);
        foreach (var file in Files)
        {
            if (file.FilePath is { } path)
            {
                assets.Add(new StaticWebAsset(file.Route, path));
                continue;
            }

            var target = Path.Combine(directory, file.Route.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target) || !File.ReadAllBytes(target).AsSpan().SequenceEqual(file.Content.Span))
            {
                File.WriteAllBytes(target, file.Content.ToArray());
            }

            assets.Add(new StaticWebAsset(file.Route, target));
        }

        return assets;
    }

    private static IEnumerable<string> IcuFiles(WebAssemblyApp app) =>
        app.InvariantGlobalization ? [] : app.LoadAllGlobalizationData ? [BrowserRuntime.FullIcuFile] : BrowserRuntime.ShardedIcuFiles;

    /// <summary>
    /// The app's assemblies and the framework's, one per name. The app's compiled assemblies always win; between a
    /// package's assembly and the framework's, the higher version wins, as the SDK resolves conflicts.
    /// </summary>
    private static List<WebAssemblyAssembly> ResolveAssemblies(WebAssemblyApp app)
    {
        var chosen = new Dictionary<string, WebAssemblyAssembly>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var assembly in app.Assemblies)
        {
            if (!chosen.TryGetValue(assembly.Name, out var existing))
            {
                chosen[assembly.Name] = assembly;
                order.Add(assembly.Name);
            }
            else if (assembly.Image is not null && existing.Image is null)
            {
                chosen[assembly.Name] = assembly;
            }
        }

        foreach (var (name, path) in app.Runtime.FrameworkAssemblies.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            var frameworkAssembly = WebAssemblyAssembly.FromFile(path);
            if (!chosen.TryGetValue(name, out var existing))
            {
                chosen[name] = frameworkAssembly;
                order.Add(name);
            }
            else if (existing.Image is null && VersionOf(frameworkAssembly) > VersionOf(existing))
            {
                chosen[name] = frameworkAssembly;
            }
        }

        return [.. order.Select(name => chosen[name])];
    }

    private static ServedFile? SymbolsOf(WebAssemblyAssembly assembly, string route)
    {
        if (assembly.Image is not null)
        {
            return assembly.Symbols is { Length: > 0 } symbols ? ServedFile.FromContent(route, symbols) : null;
        }

        var pdb = Path.ChangeExtension(assembly.FilePath!, ".pdb");
        return File.Exists(pdb) ? ServedFile.FromFile(route, pdb) : null;
    }

    /// <summary>An assembly's version, or 0.0 when it cannot be read.</summary>
    internal static Version VersionOf(WebAssemblyAssembly assembly)
    {
        try
        {
            using var reader = assembly.Image is { } image
                ? new PEReader(new MemoryStream(image.ToArray(), writable: false))
                : new PEReader(File.OpenRead(assembly.FilePath!));
            return reader.HasMetadata ? reader.GetMetadataReader().GetAssemblyDefinition().Version : new Version(0, 0);
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new Version(0, 0);
        }
    }
}
