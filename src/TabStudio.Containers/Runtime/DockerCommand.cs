using System.Globalization;
using System.Text;
using TabStudio.Containers.Registry;
using TabStudio.Containers.Store;
using TabStudio.Services;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Containers.Runtime;

/// <summary>The supported docker terminal commands. Arguments remain an argument vector throughout.</summary>
public sealed class DockerCommand(ContainerEngine engine, Func<string, ServiceManager> compose,
    IProgress<PullProgress>? progress = null)
{
    /// <summary>Runs one command; output is streamed and the result is a shell exit code.</summary>
    public async Task<int> ExecuteAsync(IReadOnlyList<string> arguments, string directory, Stream input,
        Func<ReadOnlyMemory<byte>, bool, Task> write, CancellationToken cancellationToken = default)
    {
        Task Print(string value, bool error = false) => write(Encoding.UTF8.GetBytes(value + "\n"), error);
        try
        {
            if (arguments.Count == 0 || arguments[0] is "--help" or "help")
            {
                await Print("TabStudio containers: pull, run, ps, images, logs, exec, stop, restart, rm, compose up/down.\nImages run under PRoot as this Android app's user; kernel namespaces, Dockerfile builds, dev containers and the Engine API are unavailable.").ConfigureAwait(false);
                return 0;
            }
            var args = arguments.Skip(1).ToList();
            string Take()
            {
                if (args.Count == 0) throw new ContainerException("Missing command argument; use docker --help.");
                var value = args[0]; args.RemoveAt(0); return value;
            }
            switch (arguments[0])
            {
                case "pull":
                {
                    string? platform = null;
                    if (args.Count > 0 && args[0] == "--platform") { Take(); platform = Take(); }
                    var image = await engine.PullAsync(Take(), platform, progress, cancellationToken).ConfigureAwait(false);
                    await Print(image.ManifestDigest ?? image.Id).ConfigureAwait(false);
                    return 0;
                }
                case "images":
                    await Print("IMAGE\tID\tPLATFORM\tBYTES").ConfigureAwait(false);
                    foreach (var image in await engine.Store.ListAsync(cancellationToken).ConfigureAwait(false))
                        await Print($"{image.DisplayName}\t{image.ShortId}\t{image.Platform}\t{image.Size.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                    return 0;
                case "ps":
                    if (args.Any(a => a is not ("-a" or "--all"))) throw new ContainerException("Supported ps option: -a.");
                    await Print("ID\tNAME\tIMAGE\tSTATE\tPORTS").ConfigureAwait(false);
                    foreach (var c in engine.Containers.Where(c => args.Count > 0 || c.IsRunning))
                        await Print($"{c.ShortId}\t{c.Name}\t{c.ImageName}\t{c.Status}\t{string.Join(", ", c.Ports.Select(p => $"{p.HostIp}:{p.HostPort}->{p.ContainerPort}/{p.Protocol}"))}").ConfigureAwait(false);
                    return 0;
                case "run":
                {
                    var (spec, detached) = ParseRun(args, directory);
                    var c = await engine.CreateAsync(spec, progress, cancellationToken).ConfigureAwait(false);
                    await engine.StartAsync(c.Id, cancellationToken).ConfigureAwait(false);
                    if (detached) { await Print(c.Id).ConfigureAwait(false); return 0; }
                    using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var following = Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var entry in engine.Log(c.Id).FollowAsync(null, stop.Token).ConfigureAwait(false))
                                await Print(entry.Text, entry.Stream == 2).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                    }, CancellationToken.None);
                    try { return await engine.WaitAsync(c.Id, cancellationToken).ConfigureAwait(false); }
                    finally { await stop.CancelAsync().ConfigureAwait(false); await following.ConfigureAwait(false); }
                }
                case "logs":
                {
                    var follow = false; int? tail = null;
                    while (args.Count > 0 && args[0].StartsWith('-'))
                    {
                        var option = Take();
                        if (option is "-f" or "--follow") follow = true;
                        else if (option == "--tail") tail = int.Parse(Take(), CultureInfo.InvariantCulture);
                        else throw new ContainerException($"Unsupported logs option: {option}");
                    }
                    var log = engine.Log(Take());
                    if (follow)
                    {
                        await foreach (var entry in log.FollowAsync(tail, cancellationToken).ConfigureAwait(false))
                            await Print(entry.Text, entry.Stream == 2).ConfigureAwait(false);
                    }
                    else foreach (var entry in log.Recent(tail)) await Print(entry.Text, entry.Stream == 2).ConfigureAwait(false);
                    return 0;
                }
                case "exec":
                {
                    var interactive = false;
                    string? workdir = null; string? user = null;
                    var env = new Dictionary<string, string>(StringComparer.Ordinal);
                    while (args.Count > 0 && args[0].StartsWith('-'))
                    {
                        switch (Take())
                        {
                            case "-i": case "-it": case "-ti": interactive = true; break;
                            case "-t": break;
                            case "-w": case "--workdir": workdir = Take(); break;
                            case "-u": case "--user": user = Take(); break;
                            case "-e": case "--env": AddEnvironment(env, Take()); break;
                            default: throw new ContainerException("Unsupported exec option; use -i, -e, -w or -u.");
                        }
                    }
                    var id = Take();
                    if (args.Count == 0) throw new ContainerException("exec requires a command.");
                    using var process = engine.Exec(id, new ExecSpec(args) { WorkingDirectory = workdir, User = user, Environment = env });
                    using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    async Task Pump(Stream stream, bool error)
                    {
                        var buffer = new byte[8192]; int count;
                        while ((count = await stream.ReadAsync(buffer, stop.Token).ConfigureAwait(false)) > 0)
                            await write(buffer.AsMemory(0, count), error).ConfigureAwait(false);
                    }
                    async Task Feed()
                    {
                        try { await input.CopyToAsync(process.Input, stop.Token).ConfigureAwait(false); process.CloseInput(); }
                        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
                    }
                    var readers = Task.WhenAll(Pump(process.Output, false), process.Error is { } error ? Pump(error, true) : Task.CompletedTask);
                    var feeding = interactive ? Feed() : Task.CompletedTask;
                    if (!interactive) process.CloseInput();
                    try
                    {
                        var exit = await process.WaitAsync(cancellationToken).ConfigureAwait(false);
                        await readers.ConfigureAwait(false);
                        return exit.ExitCode;
                    }
                    finally { await stop.CancelAsync().ConfigureAwait(false); await feeding.ConfigureAwait(false); }
                }
                case "stop": case "restart": case "rm":
                {
                    var force = args.Remove("-f") || args.Remove("--force");
                    if (args.Count == 0) throw new ContainerException("Give a container name or ID.");
                    foreach (var id in args)
                    {
                        if (arguments[0] == "rm") await engine.RemoveAsync(id, force, cancellationToken).ConfigureAwait(false);
                        else if (arguments[0] == "restart") await engine.RestartAsync(id, cancellationToken: cancellationToken).ConfigureAwait(false);
                        else await engine.StopAsync(id, cancellationToken: cancellationToken).ConfigureAwait(false);
                        await Print(id).ConfigureAwait(false);
                    }
                    return 0;
                }
                case "compose":
                {
                    var action = Take();
                    if (args.Any(a => a is not ("-d" or "--detach"))) throw new ContainerException("Supported compose commands: up [-d], down (from the repository directory).");
                    var manager = compose(directory);
                    void OnLog(object? sender, ServiceLogLine line) => _ = Print($"{line.Service}: {line.Text}");
                    manager.Logged += OnLog;
                    try
                    {
                        if (action == "up") await manager.StartAllAsync(cancellationToken).ConfigureAwait(false);
                        else if (action == "down") await manager.StopAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                        else throw new ContainerException("Supported compose commands: up and down.");
                        foreach (var s in manager.Statuses) await Print($"{s.Name}: {s.State} {s.Detail}").ConfigureAwait(false);
                    }
                    finally { manager.Logged -= OnLog; }
                    return 0;
                }
                default: throw new ContainerException($"docker {arguments[0]} is not available in this version; use docker --help.");
            }
        }
        catch (OperationCanceledException) { return 130; }
        catch (Exception ex) when (ex is ContainerException or RegistryException or IOException or FormatException or ComposeException or NotSupportedException)
        {
            await Print(ex.Message, true).ConfigureAwait(false); return 1;
        }
    }

    /// <summary>Parses the run subset, rejecting unsupported options instead of silently changing their meaning.</summary>
    public static (ContainerSpec Spec, bool Detached) ParseRun(IReadOnlyList<string> arguments, string directory)
    {
        string? name = null, workdir = null, user = null, platform = null;
        IReadOnlyList<string>? entrypoint = null;
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        var ports = new List<PortBinding>(); var mounts = new List<ContainerMount>();
        var detached = false; var all = false; var remove = false;
        var index = 0;
        string Value() => ++index < arguments.Count ? arguments[index] : throw new ContainerException("Missing run option value.");
        while (index < arguments.Count && arguments[index].StartsWith('-'))
        {
            switch (arguments[index])
            {
                case "--": index++; goto OptionsDone;
                case "-d": case "--detach": detached = true; break;
                case "--name": name = Value(); break;
                case "--platform": platform = Value(); break;
                case "--rm": remove = true; break;
                case "-P": case "--publish-all": all = true; break;
                case "-p": case "--publish": ports.AddRange(PortBinding.Parse(Value())); break;
                case "-v": case "--volume":
                    var mount = ContainerMount.ParseVolume(Value());
                    mounts.Add(mount.Type == "bind" ? mount with { Source = Path.GetFullPath(mount.Source!, directory) } : mount); break;
                case "-e": case "--env": AddEnvironment(env, Value()); break;
                case "-w": case "--workdir": workdir = Value(); break;
                case "-u": case "--user": user = Value(); break;
                case "--entrypoint": entrypoint = [Value()]; break;
                default: throw new ContainerException($"Unsupported run option: {arguments[index]}. Use the Containers terminal for an interactive shell.");
            }
            index++;
        }
        OptionsDone:
        if (index == arguments.Count) throw new ContainerException("run requires an image.");
        return (new ContainerSpec
        {
            Image = arguments[index++], Name = name, WorkingDirectory = workdir, User = user, Platform = platform,
            Entrypoint = entrypoint, Command = index < arguments.Count ? arguments.Skip(index).ToArray() : null,
            Environment = env, Ports = ports, Mounts = mounts, PublishAll = all, AutoRemove = remove,
        }, detached);
    }

    private static void AddEnvironment(Dictionary<string, string> environment, string value)
    {
        var equals = value.IndexOf('=', StringComparison.Ordinal);
        var key = equals < 0 ? value : value[..equals];
        if (key.Length == 0) throw new ContainerException("An environment variable needs a name.");
        environment[key] = equals < 0 ? Environment.GetEnvironmentVariable(key) ?? "" : value[(equals + 1)..];
    }
}
