using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TabStudio.Web.Serving;

namespace TabStudio.Web.WebAssembly;

/// <summary>The files a boot configuration lists, by the role the runtime gives them.</summary>
internal sealed class BootResources
{
    /// <summary>The runtime's native JavaScript module, <c>dotnet.native.js</c>.</summary>
    public List<ServedFile> JsModuleNative { get; } = [];

    /// <summary>The runtime's JavaScript, <c>dotnet.runtime.js</c>.</summary>
    public List<ServedFile> JsModuleRuntime { get; } = [];

    /// <summary>The runtime itself, <c>dotnet.native.wasm</c>.</summary>
    public List<ServedFile> WasmNative { get; } = [];

    /// <summary>ICU's culture data.</summary>
    public List<ServedFile> Icu { get; } = [];

    /// <summary>The assemblies loaded before any other: <c>System.Private.CoreLib</c> and the JavaScript interop.</summary>
    public List<ServedFile> CoreAssemblies { get; } = [];

    /// <summary>The other assemblies, the framework's and the app's.</summary>
    public List<ServedFile> Assemblies { get; } = [];

    /// <summary>The symbols of the app's assemblies.</summary>
    public List<ServedFile> Symbols { get; } = [];
}

/// <summary>
/// The boot configuration of .NET for WebAssembly: what the runtime loads and how it starts. In .NET 10 it is no
/// longer a <c>blazor.boot.json</c> file but part of <c>dotnet.js</c>, where the SDK writes it over a placeholder; this
/// writes it the same way.
/// </summary>
internal static partial class WebAssemblyBootConfig
{
    /// <summary>Writes the configuration of an app.</summary>
    /// <param name="app">The app.</param>
    /// <param name="resources">Its files, with routes under <see cref="WebAssemblyApp.FrameworkPath"/>.</param>
    public static string Create(WebAssemblyApp app, BootResources resources)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(resources);

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("mainAssemblyName", app.MainAssemblyName);
            json.WriteString("applicationEnvironment", app.ApplicationEnvironment);
            WriteResources(json, app, resources);
            json.WriteNumber("debugLevel", resources.Symbols.Count > 0 ? -1 : 0);
            json.WriteString("globalizationMode", GlobalizationMode(app));
            if (app.AppSettings.Count > 0)
            {
                json.WriteStartArray("appsettings");
                foreach (var route in app.AppSettings)
                {
                    json.WriteStringValue(RelativeToFramework(app.FrameworkPath, route));
                }

                json.WriteEndArray();
            }

            if (app.BootExtensions.Count > 0)
            {
                json.WriteStartObject("extensions");
                foreach (var extension in app.BootExtensions)
                {
                    json.WriteStartObject(extension);
                    json.WriteEndObject();
                }

                json.WriteEndObject();
            }

            json.WriteStartObject("runtimeConfig");
            json.WriteStartObject("runtimeOptions");
            json.WriteStartObject("configProperties");
            foreach (var (name, value) in RuntimeProperties(app))
            {
                json.WritePropertyName(name);
                WriteValue(json, value);
            }

            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <summary>
    /// Writes a configuration into the runtime pack's <c>dotnet.js</c>, over its <c>/*! dotnetBootConfig */{}</c>
    /// placeholder, as the SDK's <c>GenerateWasmBootJson</c> task does.
    /// </summary>
    /// <param name="dotnetJs">The runtime pack's <c>dotnet.js</c>.</param>
    /// <param name="bootConfig">The configuration, from <see cref="Create"/>.</param>
    /// <exception cref="InvalidDataException">This <c>dotnet.js</c> has no placeholder: it is from a .NET before 10.</exception>
    public static string MergeInto(string dotnetJs, string bootConfig)
    {
        ArgumentNullException.ThrowIfNull(dotnetJs);
        ArgumentNullException.ThrowIfNull(bootConfig);
        if (!BootConfigPlaceholder().IsMatch(dotnetJs))
        {
            throw new InvalidDataException("This dotnet.js has no place for a boot configuration. TabStudio runs .NET 10 WebAssembly apps.");
        }

        var merged = BootConfigPlaceholder().Replace(dotnetJs, _ => "/*json-start*/" + bootConfig + "/*json-end*/", 1);
        return BundlerImportsPlaceholder().Replace(merged, string.Empty);
    }

    private static void WriteResources(Utf8JsonWriter json, WebAssemblyApp app, BootResources resources)
    {
        json.WriteStartObject("resources");
        json.WriteString("hash", ResourcesHash(resources));
        WriteNames(json, "jsModuleNative", resources.JsModuleNative, app.FrameworkPath);
        WriteNames(json, "jsModuleRuntime", resources.JsModuleRuntime, app.FrameworkPath);
        WriteFiles(json, "wasmNative", resources.WasmNative, app.FrameworkPath, withVirtualPath: false);
        WriteFiles(json, "icu", resources.Icu, app.FrameworkPath, withVirtualPath: true);
        WriteFiles(json, "coreAssembly", resources.CoreAssemblies, app.FrameworkPath, withVirtualPath: true);
        WriteFiles(json, "assembly", resources.Assemblies, app.FrameworkPath, withVirtualPath: true);
        WriteFiles(json, "pdb", resources.Symbols, app.FrameworkPath, withVirtualPath: true);
        if (app.LibraryInitializers.Count > 0)
        {
            // Blazor reads the first list, relative to the page; the runtime the second, relative to dotnet.js.
            WriteRoutes(json, "libraryInitializers", app.LibraryInitializers);
            WriteRoutes(json, "modulesAfterConfigLoaded", [.. app.LibraryInitializers.Select(r => RelativeToFramework(app.FrameworkPath, r))]);
        }

        json.WriteEndObject();
    }

    private static void WriteNames(Utf8JsonWriter json, string property, List<ServedFile> files, string frameworkPath)
    {
        json.WriteStartArray(property);
        foreach (var file in files)
        {
            json.WriteStartObject();
            json.WriteString("name", NameUnder(frameworkPath, file.Route));
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    private static void WriteRoutes(Utf8JsonWriter json, string property, IReadOnlyList<string> routes)
    {
        json.WriteStartArray(property);
        foreach (var route in routes)
        {
            json.WriteStartObject();
            json.WriteString("name", route);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    private static void WriteFiles(Utf8JsonWriter json, string property, List<ServedFile> files, string frameworkPath, bool withVirtualPath)
    {
        if (files.Count == 0)
        {
            return;
        }

        json.WriteStartArray(property);
        foreach (var file in files)
        {
            var name = NameUnder(frameworkPath, file.Route);
            json.WriteStartObject();
            if (withVirtualPath)
            {
                json.WriteString("virtualPath", name);
            }

            json.WriteString("name", name);
            json.WriteString("hash", file.Integrity);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    /// <summary>The hash of every resource together, the key the runtime caches them under.</summary>
    private static string ResourcesHash(BootResources resources)
    {
        IEnumerable<ServedFile> all =
        [
            .. resources.WasmNative, .. resources.Icu, .. resources.CoreAssemblies, .. resources.Assemblies, .. resources.Symbols,
        ];
        var hashes = string.Concat(all.Select(f => f.Integrity).Order(StringComparer.Ordinal));
        return "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(hashes)));
    }

    /// <summary>Writes a runtime property's value: a switch, a number or text, as <c>runtimeconfig.json</c> holds them.</summary>
    private static void WriteValue(Utf8JsonWriter json, object value)
    {
        switch (value)
        {
            case bool flag:
                json.WriteBooleanValue(flag);
                break;
            case int number:
                json.WriteNumberValue(number);
                break;
            case long number:
                json.WriteNumberValue(number);
                break;
            case double number:
                json.WriteNumberValue(number);
                break;
            default:
                json.WriteStringValue(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
        }
    }

    private static string GlobalizationMode(WebAssemblyApp app) =>
        app.InvariantGlobalization ? "invariant" : app.LoadAllGlobalizationData ? "all" : "sharded";

    private static SortedDictionary<string, object> RuntimeProperties(WebAssemblyApp app)
    {
        var properties = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, value) in BrowserRuntime.DefaultRuntimeProperties)
        {
            properties[name] = value;
        }

        foreach (var (name, value) in app.RuntimeProperties)
        {
            properties[name] = value;
        }

        properties["System.Globalization.Invariant"] = app.InvariantGlobalization;
        return properties;
    }

    /// <summary>A file's name relative to the framework folder, as the boot configuration names files.</summary>
    private static string NameUnder(string frameworkPath, string route)
    {
        var prefix = frameworkPath.Trim('/') + "/";
        return route.StartsWith(prefix, StringComparison.Ordinal) ? route[prefix.Length..] : route;
    }

    /// <summary>A route relative to the framework folder, such as <c>../appsettings.json</c>.</summary>
    private static string RelativeToFramework(string frameworkPath, string route)
    {
        var depth = frameworkPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        return string.Concat(Enumerable.Repeat("../", depth)) + route.TrimStart('/');
    }

    [GeneratedRegex(@"/\*!\s*dotnetBootConfig\s*\*/\s*\{\}")]
    private static partial Regex BootConfigPlaceholder();

    [GeneratedRegex(@"/\*!\s*bundlerFriendlyImports\s*\*/")]
    private static partial Regex BundlerImportsPlaceholder();
}
