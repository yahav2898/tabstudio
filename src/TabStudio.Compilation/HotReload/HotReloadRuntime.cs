using System.Reflection.Metadata;
using TabStudio.Compilation.Running;

namespace TabStudio.Compilation.HotReload;

/// <summary>Checks desktop update support without enabling Android's process-wide interpreter.</summary>
internal static class HotReloadRuntime
{
    private const string FeatureSwitch = "System.Reflection.Metadata.MetadataUpdater.IsSupported";
    public static bool IsSupported => !OperatingSystem.IsAndroid() && MetadataUpdater.IsSupported;

    public static string Details => $"MetadataUpdater.IsSupported={MetadataUpdater.IsSupported}; AndroidMono={IsAndroidMono}; "
        + $"startup environment={Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES") ?? "unset"}; "
        + $"Mono interpreter options={Environment.GetEnvironmentVariable("MONO_INTERPRETER_OPTIONS") ?? "default"}; "
        + $"feature switch={(AppContext.TryGetSwitch(FeatureSwitch, out var enabled) ? enabled.ToString() : "default")}; "
        + (OperatingSystem.IsAndroid() ? "local Android Hot Reload deferred; the IDE uses the JIT" : "native capability check");

    // Use the same marker as dotnet/runtime's PlatformDetection; Mono.Runtime was removed from modern Mono.
    private static bool IsAndroidMono => OperatingSystem.IsAndroid() && typeof(object).Assembly.GetType("Mono.RuntimeStructs") is not null;

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
