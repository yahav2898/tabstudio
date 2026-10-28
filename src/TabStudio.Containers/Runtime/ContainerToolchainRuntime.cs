using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Containers.Runtime;

/// <summary>
/// Runs the IDE's programs inside a running container's file system ("Reopen in Container"): the terminal, builds,
/// F5, debug adapters and language servers start there with the container's environment, user and mounts, as VS
/// Code's dev containers do. The workspace is shared at its own path, so paths in output and debugging stay the
/// files the editor has open.
/// </summary>
public sealed class ContainerToolchainRuntime : IToolchainRuntime
{
    private readonly ContainerEngine _engine;
    private readonly ContainerInfo _container;
    private readonly ProotRuntime _inner;
    private readonly IReadOnlyDictionary<string, string> _environment;
    private readonly ContainerUser _user;
    private readonly IReadOnlyList<ProcessMount> _mounts;

    internal ContainerToolchainRuntime(ContainerEngine engine, ContainerInfo container, ProotRuntime inner, IReadOnlyDictionary<string, string> environment, ContainerUser user, IReadOnlyList<ProcessMount> mounts)
    {
        _engine = engine;
        _container = container;
        _inner = inner;
        _environment = environment;
        _user = user;
        _mounts = mounts;
    }

    /// <summary>The container it runs in.</summary>
    public ContainerInfo Container => _engine.Find(_container.Id) ?? _container;

    /// <inheritdoc />
    public string DisplayName => $"Dev container {_container.Name}";

    /// <inheritdoc />
    public RuntimeKind Kind => RuntimeKind.Linux;

    /// <inheritdoc />
    public ToolchainProcess Start(ProcessStartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var environment = _environment.ToDictionary(e => e.Key, e => (string?)e.Value, StringComparer.Ordinal);
        foreach (var (key, value) in spec.Environment)
        {
            environment[key] = key == "PATH" && value is not null ? value.Replace("$PATH", _environment.GetValueOrDefault("PATH") ?? ProotRuntime.SearchPath, StringComparison.Ordinal) : value;
        }

        return _inner.Start(spec with
        {
            Environment = environment,
            Mounts = [.. _mounts, .. spec.Mounts],
            UserId = spec.UserId ?? (_user.Uid == 0 ? null : _user.Uid),
            GroupId = spec.GroupId ?? (_user.Uid == 0 ? null : _user.Gid),
        });
    }

    /// <inheritdoc />
    public bool HasCommand(string command) => _inner.HasCommand(command);

    /// <inheritdoc />
    public string? Resolve(string command) => _inner.Resolve(command);

    /// <inheritdoc />
    public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => _inner.Shell(workingDirectory, size);

    /// <inheritdoc />
    public void ShareFolder(string folder) => _inner.ShareFolder(folder);

    /// <inheritdoc />
    public string? ToDevicePath(string programPath, string? workingDirectory) => _inner.ToDevicePath(programPath, workingDirectory);
}
