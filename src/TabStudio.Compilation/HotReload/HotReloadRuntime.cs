using System.Reflection;
using System.Reflection.Metadata;
using TabStudio.Compilation.Running;

namespace TabStudio.Compilation.HotReload;

/// <summary>Checks the public capability flag and verifies Mono's startup-enabled updater when its flag is stale.</summary>
internal static class HotReloadRuntime
{
    private const string FeatureSwitch = "System.Reflection.Metadata.MetadataUpdater.IsSupported";
    private static readonly Lazy<(bool Supported, string Detail)> MonoProbe = new(VerifyMono);

    public static bool IsSupported => MetadataUpdater.IsSupported || (IsAndroidMono && MonoProbe.Value.Supported);

    public static string Details => $"MetadataUpdater.IsSupported={MetadataUpdater.IsSupported}; AndroidMono={IsAndroidMono}; "
        + $"startup environment={Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES") ?? "unset"}; "
        + $"Mono interpreter options={Environment.GetEnvironmentVariable("MONO_INTERPRETER_OPTIONS") ?? "default"}; "
        + $"feature switch={(AppContext.TryGetSwitch(FeatureSwitch, out var enabled) ? enabled.ToString() : "default")}; "
        + (IsAndroidMono ? MonoProbe.Value.Detail : "native capability check");

    // Use the same marker as dotnet/runtime's PlatformDetection; Mono.Runtime was removed from modern Mono.
    private static bool IsAndroidMono => OperatingSystem.IsAndroid() && typeof(object).Assembly.GetType("Mono.RuntimeStructs") is not null;

    private static (bool, string) VerifyMono()
    {
        // .NET 10 Mono's hot_reload_update_enabled caches the mode but returns a local false on subsequent
        // calls, including IsSupported after the interpreter's startup check. Never assume that means support:
        // require the startup settings AND the actual component, then prove ApplyUpdate on an isolated module.
        // https://github.com/dotnet/runtime/blob/v10.0.14/src/mono/mono/component/hot_reload.c
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES"), "debug", StringComparison.OrdinalIgnoreCase)
            || (AppContext.TryGetSwitch(FeatureSwitch, out var enabled) && !enabled))
        {
            return (false, "Mono startup settings do not enable metadata updates");
        }

        try
        {
            // Mono's component-only check does not consult the broken cached environment flag. It must report
            // a real component before attempting ApplyUpdate; a packaged stub is insufficient.
            var capabilities = typeof(MetadataUpdater).GetMethod("GetCapabilities", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, null) as string;
            if (string.IsNullOrWhiteSpace(capabilities)) return (false, "Mono hot-reload component is unavailable");
            var verified = VerifyApplyUpdate();
            return (verified, verified ? "Mono ApplyUpdate probe passed despite the stale public flag" : "Mono ApplyUpdate probe returned the old method body");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (false, $"Mono ApplyUpdate probe failed: {ex.GetBaseException().Message}");
        }
    }

    /// <summary>Uses only public delta/update APIs; never updates an IDE assembly or the user's program.</summary>
    internal static bool VerifyApplyUpdate()
    {
        var compiler = new CSharpCompiler();
        CompiledAssembly Compile(int value)
        {
            var result = compiler.Compile(new CompilationRequest
            {
                AssemblyName = "TabStudioHotReloadCapabilityProbe",
                Kind = AssemblyKind.Library,
                Documents = [new SourceDocument("Capability.cs", $"public static class Capability {{ [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public static int Value() => {value}; public static int Read() => Value(); }}")],
            });
            return result.Assembly ?? throw new InvalidOperationException(string.Join('\n', result.Diagnostics));
        }

        var original = Compile(1);
        using var program = LoadedProgram.Load(original, [], [], unloadable: false);
        // Invoke the unchanged caller both before and after the update, proving an existing call site uses new IL.
        var method = program.EntryAssembly.GetType("Capability")!.GetMethod("Read")!;
        if (!Equals(method.Invoke(null, null), 1)) return false;
        using var baseline = new HotReloadBaseline(original);
        var generated = baseline.EmitDifference(Compile(2).HotReloadCompilation!, out var delta);
        if (!generated.Success || delta is null) return false;
        MetadataUpdater.ApplyUpdate(program.EntryAssembly, delta.Metadata, delta.IL, delta.Pdb);
        return Equals(method.Invoke(null, null), 2);
    }
}
