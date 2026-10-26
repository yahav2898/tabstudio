using System.Diagnostics;
using TabStudio.Compilation;
using TabStudio.Compilation.HotReload;
using TabStudio.Compilation.Running;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>Proves desktop updates, or the explicit Android JIT capability boundary.</summary>
    internal static SelfTestResult CheckHotReload(CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (OperatingSystem.IsAndroid())
        {
            var jitPolicy = !HotReloadSession.IsSupported
                && Environment.GetEnvironmentVariable("MONO_INTERPRETER_OPTIONS") is null
                && Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES") is null;
            return new SelfTestResult(jitPolicy,
                "Hot Reload: local Android updates deferred in 0.15; JIT runtime, restart to apply edits. " + HotReloadSession.RuntimeDetails,
                clock.Elapsed);
        }

        if (!HotReloadSession.IsSupported)
        {
            return new SelfTestResult(true, "Hot Reload: " + HotReloadSession.UnsupportedReason + " " + HotReloadSession.RuntimeDetails, clock.Elapsed);
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
            success ? "Hot Reload: ApplyUpdate changed 2 -> 12 -> 103 in the same assembly; static state preserved (" + HotReloadSession.RuntimeDetails + ")"
                : $"Hot Reload failed: {before}, {after}, {last}: {applied.Message} {again.Message}", clock.Elapsed);
    }
}
