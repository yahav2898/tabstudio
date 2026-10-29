using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Runtimes;

/// <summary>
/// Routes the programs of toolchains' glibc parts, such as <c>swift</c>, <c>sourcekit-lsp</c> or <c>kotlin-lsp</c>, to
/// TabStudio's Ubuntu environment; every other program stays where it was, in Alpine.
/// </summary>
/// <param name="primary">The runtime of everything else.</param>
/// <param name="glibc">The Ubuntu environment's runtime.</param>
/// <param name="runsInGlibc">Whether a program, by name or path, belongs to an installed glibc part.</param>
public sealed class GlibcRuntime(IToolchainRuntime primary, IToolchainRuntime glibc, Func<string, bool> runsInGlibc) : IToolchainRuntime
{
    /// <inheritdoc />
    public string DisplayName => primary.DisplayName + " + Ubuntu (glibc)";

    /// <inheritdoc />
    public RuntimeKind Kind => primary.Kind;

    private IToolchainRuntime For(string program) => runsInGlibc(program) ? glibc : primary;

    /// <inheritdoc />
    public ToolchainProcess Start(ProcessStartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return For(spec.Program).Start(spec);
    }

    /// <inheritdoc />
    public bool HasCommand(string command) => Resolve(command) is not null;

    /// <inheritdoc />
    public string? Resolve(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return For(command).Resolve(command);
    }

    /// <inheritdoc />
    public string? ResolveForDirectory(string command, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(command);
        return For(command).ResolveForDirectory(command, workingDirectory);
    }

    /// <inheritdoc />
    public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => primary.Shell(workingDirectory, size);

    /// <inheritdoc />
    public void ShareFolder(string folder)
    {
        primary.ShareFolder(folder);
        glibc.ShareFolder(folder);
    }

    /// <inheritdoc />
    public string? ToDevicePath(string programPath, string? workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        return For(programPath).ToDevicePath(programPath, workingDirectory);
    }
}
