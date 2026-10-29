using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Runtimes;

public sealed partial class ProotRuntime
{
    /// <summary>JVM launchers and their children use the APK's musl-compatible mapping shim on Android.</summary>
    private string? JavaMappingShim(ProcessStartSpec spec)
    {
        if (spec.Environment.ContainsKey("LD_PRELOAD")
            || Path.GetFileName(spec.Program) is not ("java" or "javac" or "jdtls" or "mvn" or "gradle" or "kotlinc" or "kotlin" or "kotlin-lsp" or "intellij-server"))
        {
            return null;
        }

        var shim = Path.Combine(Path.GetDirectoryName(_options.ProotPath)!, "libtabstudio-jvm.so");
        return File.Exists(shim) ? shim : null;
    }
}
