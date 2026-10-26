using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Runtimes;

/// <summary>Routes Flutter and its bundled Dart SDK to a glibc environment; other commands stay in Alpine.</summary>
public sealed class FlutterRuntime(IToolchainRuntime primary, IToolchainRuntime flutter, Func<bool>? isReady = null) : IToolchainRuntime
{
    /// <inheritdoc />
    public string DisplayName => primary.DisplayName + " + Flutter (Ubuntu glibc)";
    /// <inheritdoc />
    public RuntimeKind Kind => primary.Kind;
    /// <summary>Whether a command belongs to the Flutter side environment.</summary>
    public static bool IsFlutterCommand(string command) =>
        command is "flutter" or "dart" || command.StartsWith("/opt/flutter/", StringComparison.Ordinal);
    private IToolchainRuntime For(string command)
    {
        if (IsFlutterCommand(command) && isReady?.Invoke() == false)
        {
            throw new ToolchainRuntimeException("Flutter is not ready. Finish installing it in the Toolchain Store.");
        }
        return IsFlutterCommand(command) ? flutter : primary;
    }
    /// <inheritdoc />
    public ToolchainProcess Start(ProcessStartSpec spec) => For(spec.Program).Start(spec);
    /// <inheritdoc />
    public bool HasCommand(string command) => Resolve(command) is not null;
    /// <inheritdoc />
    public string? Resolve(string command) => IsFlutterCommand(command) && isReady?.Invoke() == false ? null : For(command).Resolve(command);
    /// <inheritdoc />
    public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => primary.Shell(workingDirectory, size);
    /// <inheritdoc />
    public void ShareFolder(string folder) { primary.ShareFolder(folder); flutter.ShareFolder(folder); }
    /// <inheritdoc />
    public string? ToDevicePath(string programPath, string? workingDirectory) =>
        IsFlutterCommand(programPath) ? flutter.ToDevicePath(programPath, workingDirectory) : primary.ToDevicePath(programPath, workingDirectory);
}
