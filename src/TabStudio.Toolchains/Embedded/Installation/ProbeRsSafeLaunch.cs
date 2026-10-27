namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>Scopes managed probe-rs CLI invocations to a fresh explicit empty preset, preventing automatic project/user erase permissions.</summary>
public static class ProbeRsSafeLaunch
{
    /// <summary>Opt-in marker on typed IDE commands; ordinary Terminal commands retain their upstream configuration behavior.</summary>
    public const string Marker = "TABSTUDIO_PROBE_RS_NO_PRESETS";
    /// <summary>App-owned launcher beside the unchanged upstream executable.</summary>
    public const string ScriptPath = "/opt/tabstudio/probe-rs/tabstudio-probe-launcher.py";

    /// <summary>The same launcher source exercised by native and production-installer CI.</summary>
    public static string Source
    {
        get
        {
            using var stream = typeof(ProbeRsSafeLaunch).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Embedded.probe_rs_runtime.py")
                ?? throw new InvalidOperationException("The managed probe-rs preset launcher is missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>Argument-preserving PATH wrapper; it invokes the downloaded executable without modifying its bytes.</summary>
    public static string Launcher(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (executable != "/opt/tabstudio/probe-rs/probe-rs")
            throw new ArgumentException("The managed launcher requires the fixed private upstream probe-rs path.", nameof(executable));
        return "#!/bin/sh\nexec python3 " + ScriptPath + " " + executable + " \"$@\"\n";
    }
}
