using System.Diagnostics;
using TabStudio.Compilation;
using TabStudio.Compilation.HotReload;
using TabStudio.Compilation.Running;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>Proves the actual runtime accepts Roslyn metadata/IL/PDB deltas, including on Android Mono.</summary>
    internal static SelfTestResult CheckHotReload(CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (!HotReloadSession.IsSupported)
        {
            // A desktop may be deliberately started without Hot Reload. The Android emulator must prove it works.
            return new SelfTestResult(!OperatingSystem.IsAndroid(), "Hot Reload: " + HotReloadSession.UnsupportedReason, clock.Elapsed);
        }

        var compiler = new CSharpCompiler();
        CompiledAssembly Compile(int value)
        {
            var result = compiler.Compile(new CompilationRequest
            {
                AssemblyName = "TabStudioHotReloadSelfTest",
                Kind = AssemblyKind.Library,
                Documents = [new SourceDocument("Probe.cs", $"public static class Probe {{ public static int State; public static int Value() => ++State + {value}; }}")],
            }, cancellationToken);
            return result.Assembly ?? throw new InvalidOperationException(string.Join('\n', result.Diagnostics));
        }

        var original = Compile(1);
        using var program = LoadedProgram.Load(original, [], [], unloadable: false);
        using var reload = new HotReloadSession(original, [], program.EntryAssembly);
        var method = program.EntryAssembly.GetType("Probe")!.GetMethod("Value")!;
        var before = (int)method.Invoke(null, null)!;
        var applied = reload.Apply([Compile(10)], cancellationToken);
        var after = (int)method.Invoke(null, null)!;
        var again = reload.Apply([Compile(100)], cancellationToken);
        var last = (int)method.Invoke(null, null)!;
        var success = before == 2 && after == 12 && last == 103 && applied.Success && again.Success;
        return new SelfTestResult(success,
            success ? "Hot Reload: ApplyUpdate changed 2 -> 12 -> 103 in the same assembly; static state preserved"
                : $"Hot Reload failed: {before}, {after}, {last}: {applied.Message} {again.Message}", clock.Elapsed);
    }
}
