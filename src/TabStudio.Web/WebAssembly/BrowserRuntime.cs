namespace TabStudio.Web.WebAssembly;

/// <summary>
/// .NET for the browser: the extracted runtime pack <c>Microsoft.NETCore.App.Runtime.Mono.browser-wasm</c>, with the
/// runtime's JavaScript and WebAssembly (<c>dotnet.js</c>, <c>dotnet.native.wasm</c>), its ICU data and the framework's
/// assemblies built for the browser.
/// </summary>
public sealed class BrowserRuntime
{
    /// <summary>The runtime's own assemblies, which it loads before any other.</summary>
    public static IReadOnlyList<string> CoreAssemblyNames { get; } = ["System.Private.CoreLib", "System.Runtime.InteropServices.JavaScript"];

    /// <summary>The ICU data files of the sharded globalization mode, which the runtime picks from by culture.</summary>
    public static IReadOnlyList<string> ShardedIcuFiles { get; } = ["icudt_CJK.dat", "icudt_EFIGS.dat", "icudt_no_CJK.dat"];

    /// <summary>The ICU data file with every culture.</summary>
    public const string FullIcuFile = "icudt.dat";

    /// <summary>
    /// The runtime configuration the SDK gives every app built for the browser, as a Debug build of a Blazor
    /// WebAssembly app has it in its boot configuration: features the browser has no use for are off.
    /// </summary>
    public static IReadOnlyDictionary<string, object> DefaultRuntimeProperties { get; } = new Dictionary<string, object>(StringComparer.Ordinal)
    {
        ["Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability"] = true,
        ["System.ComponentModel.DefaultValueAttribute.IsSupported"] = false,
        ["System.ComponentModel.Design.IDesignerHost.IsSupported"] = false,
        ["System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization"] = false,
        ["System.ComponentModel.TypeDescriptor.IsComObjectDescriptorSupported"] = false,
        ["System.Data.DataSet.XmlSerializationIsSupported"] = false,
        ["System.Diagnostics.Metrics.Meter.IsSupported"] = false,
        ["System.Diagnostics.Tracing.EventSource.IsSupported"] = false,
        ["System.GC.Server"] = true,
        ["System.TimeZoneInfo.Invariant"] = false,
        ["System.Linq.Enumerable.IsSizeOptimized"] = true,
        ["System.Net.Http.EnableActivityPropagation"] = false,
        ["System.Net.Http.WasmEnableStreamingResponse"] = true,
        ["System.Net.SocketsHttpHandler.Http3Support"] = false,
        ["System.Reflection.Metadata.MetadataUpdater.IsSupported"] = false,
        ["System.Resources.ResourceManager.AllowCustomResourceTypes"] = false,
        ["System.Resources.UseSystemResourceKeys"] = true,
        ["System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported"] = true,
        ["System.Runtime.InteropServices.BuiltInComInterop.IsSupported"] = false,
        ["System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting"] = false,
        ["System.Runtime.InteropServices.EnableCppCLIHostActivation"] = false,
        ["System.Runtime.InteropServices.Marshalling.EnableGeneratedComInterfaceComImportInterop"] = false,
        ["System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization"] = false,
        ["System.StartupHookProvider.IsSupported"] = false,
        ["System.Text.Encoding.EnableUnsafeUTF7Encoding"] = false,
        ["System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault"] = true,
        ["System.Threading.Thread.EnableAutoreleasePool"] = false,
    };

    private readonly Lazy<IReadOnlyDictionary<string, string>> _assemblies;

    /// <summary>Opens an extracted runtime pack.</summary>
    /// <param name="packageDirectory">The pack's folder, such as <c>packages/microsoft.netcore.app.runtime.mono.browser-wasm/10.0.13</c>.</param>
    /// <exception cref="DirectoryNotFoundException">The folder is not a browser-wasm runtime pack.</exception>
    public BrowserRuntime(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageDirectory);
        PackageDirectory = packageDirectory;
        var runtime = Path.Combine(packageDirectory, "runtimes", "browser-wasm");
        NativeDirectory = Path.Combine(runtime, "native");
        var libraries = Path.Combine(runtime, "lib");
        LibraryDirectory = Directory.Exists(libraries)
            ? Directory.EnumerateDirectories(libraries, "net*").Order(StringComparer.Ordinal).LastOrDefault() ?? libraries
            : libraries;
        if (!File.Exists(DotnetJsPath) || !Directory.Exists(LibraryDirectory))
        {
            throw new DirectoryNotFoundException($"'{packageDirectory}' is not a .NET for WebAssembly runtime pack: it has no runtimes/browser-wasm/native/dotnet.js.");
        }

        Version = Path.GetFileName(packageDirectory.TrimEnd(Path.DirectorySeparatorChar));
        _assemblies = new(FindAssemblies);
    }

    /// <summary>The pack's folder.</summary>
    public string PackageDirectory { get; }

    /// <summary>The pack's version, such as <c>10.0.13</c>: the name of its folder in the packages folder.</summary>
    public string Version { get; }

    /// <summary>The folder with the runtime's JavaScript, WebAssembly and ICU data, and <c>System.Private.CoreLib</c>.</summary>
    public string NativeDirectory { get; }

    /// <summary>The folder with the framework's assemblies, such as <c>lib/net10.0</c>.</summary>
    public string LibraryDirectory { get; }

    /// <summary>The runtime's loader, <c>dotnet.js</c>, before its boot configuration is merged in.</summary>
    public string DotnetJsPath => Path.Combine(NativeDirectory, "dotnet.js");

    /// <summary>The framework's assemblies by simple name, such as <c>System.Text.Json</c>.</summary>
    public IReadOnlyDictionary<string, string> FrameworkAssemblies => _assemblies.Value;

    /// <summary>A file of the runtime, such as <c>dotnet.native.wasm</c>.</summary>
    /// <param name="fileName">The file name.</param>
    public string NativeFile(string fileName) => Path.Combine(NativeDirectory, fileName);

    private Dictionary<string, string> FindAssemblies()
    {
        var assemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(LibraryDirectory, "*.dll").Order(StringComparer.Ordinal))
        {
            assemblies.TryAdd(Path.GetFileNameWithoutExtension(file), file);
        }

        var coreLibrary = Path.Combine(NativeDirectory, "System.Private.CoreLib.dll");
        if (File.Exists(coreLibrary))
        {
            assemblies["System.Private.CoreLib"] = coreLibrary;
        }

        return assemblies;
    }
}
