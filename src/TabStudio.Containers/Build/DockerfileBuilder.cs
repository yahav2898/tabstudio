using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TabStudio.Containers.Images;
using TabStudio.Containers.Registry;
using TabStudio.Containers.Runtime;
using TabStudio.Containers.Store;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Containers.Build;

/// <summary>What to build: a context folder, its Dockerfile and the name the image gets.</summary>
/// <param name="Context">The build context: the folder <c>COPY</c> and <c>ADD</c> read from.</param>
/// <param name="Tag">The name the image gets, such as <c>compose-1a2b3c4d5e6f-app</c> or <c>myapp:dev</c>.</param>
public sealed record BuildRequest(string Context, string Tag)
{
    /// <summary>The Dockerfile; <c>Dockerfile</c> in the context when unset. A relative path is relative to the context.</summary>
    public string? DockerfilePath { get; init; }

    /// <summary>Build arguments, as <c>--build-arg</c> gives them.</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; init; } = new Dictionary<string, string>();

    /// <summary>The stage to build, by name or number; the last stage when unset.</summary>
    public string? Target { get; init; }

    /// <summary>The platform, such as <c>linux/arm64</c>; the device's when unset.</summary>
    public string? Platform { get; init; }
}

/// <summary>
/// Builds an image from a Dockerfile, as <c>docker build</c> does, into the engine's store: each stage starts from
/// a copy of its base image's files, <c>RUN</c> steps run under PRoot as a container's processes do, and every step
/// that changes files becomes a layer of its own (<see cref="LayerDiff"/>). An image built from the same Dockerfile,
/// arguments and context files is used again instead of being built twice.
/// </summary>
/// <param name="engine">The engine whose store gets the image and whose PRoot runs the steps.</param>
public sealed class DockerfileBuilder(ContainerEngine engine)
{
    /// <summary>The label that records what an image was built from, so an unchanged build is not repeated.</summary>
    public const string BuildKeyLabel = "dev.tabstudio.build-key";

    private static readonly string[] DefaultShell = ["/bin/sh", "-c"];
    private static readonly char[] Blanks = [' ', '\t'];
    private static readonly char[] Wildcards = ['*', '?', '['];
    private static readonly byte[] Separator = [0];
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private static readonly JsonElement EmptyObject = ParseEmptyObject();

    /// <summary>Builds the image, or finds the one already built from the same inputs.</summary>
    /// <param name="request">What to build.</param>
    /// <param name="log">Receives the build's steps and output, line by line.</param>
    /// <param name="cancellationToken">Stops the build; nothing half-built is kept.</param>
    /// <returns>The image, named <see cref="BuildRequest.Tag"/>.</returns>
    /// <exception cref="ContainerException">The Dockerfile is invalid, or one of its steps failed.</exception>
    public async Task<StoredImage> BuildAsync(BuildRequest request, Action<string> log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(log);
        if (engine.Problem is { } problem) throw new ContainerException(problem);
        var context = Path.GetFullPath(request.Context);
        if (!Directory.Exists(context)) throw new ContainerException($"The build context {context} does not exist.");
        var dockerfilePath = Path.GetFullPath(Path.Combine(context, request.DockerfilePath ?? "Dockerfile"));
        if (!File.Exists(dockerfilePath)) throw new ContainerException($"There is no Dockerfile at {dockerfilePath}.");
        if (!ImageReference.TryParse(request.Tag, out var reference, out var error)) throw new ContainerException(error);
        var text = await File.ReadAllTextAsync(dockerfilePath, cancellationToken).ConfigureAwait(false);
        Dockerfile dockerfile;
        try
        {
            dockerfile = Dockerfile.Parse(text);
        }
        catch (FormatException ex)
        {
            throw new ContainerException($"{Path.GetFileName(dockerfilePath)}: {ex.Message}", ex);
        }

        var ignore = DockerIgnore.ForContext(context, dockerfilePath);
        var files = ignore.Files(context).ToList();
        var key = BuildKey(text, request, context, files);
        var previous = await engine.Store.FindAsync(reference.Canonical, cancellationToken).ConfigureAwait(false);
        if (previous is not null && previous.Config.Config.Labels?.GetValueOrDefault(BuildKeyLabel) == key)
        {
            log($"{reference.Familiar} is up to date: nothing it is built from has changed.");
            return previous;
        }

        var globals = PredefinedArguments(request.Platform is { Length: > 0 } wanted ? OciPlatform.Parse(wanted) : engine.Options.Host, engine.Options.Host);
        foreach (var instruction in dockerfile.GlobalArgs)
        {
            foreach (var (name, value) in BuildWords.Pairs(BuildWords.Expand(instruction.Arguments, globals, dockerfile.Escape), legacySpaceForm: false, dockerfile.Escape))
            {
                if ((request.Arguments.GetValueOrDefault(name) ?? value) is { } given) globals[name] = given;
            }
        }

        var target = request.Target is { Length: > 0 } targetName
            ? dockerfile.Stage(targetName) ?? throw new ContainerException($"The Dockerfile has no stage named {targetName}.")
            : dockerfile.Stages[^1];
        var needed = new HashSet<int>();
        Need(dockerfile, target, globals, needed);
        var work = Path.Combine(engine.Store.TemporaryFolder, "build-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(work);
        var inputs = new Inputs(request, dockerfile, context, ignore, files, globals, new Dictionary<int, Stage>(), work, log);
        try
        {
            foreach (var definition in dockerfile.Stages.Where(s => needed.Contains(s.Index)))
            {
                var stage = await BeginAsync(definition, inputs, cancellationToken).ConfigureAwait(false);
                inputs.Stages[definition.Index] = stage;
                foreach (var instruction in definition.Instructions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    log($"[{definition.Name ?? definition.Index.ToString(CultureInfo.InvariantCulture)}] {FirstLine(instruction.Original)}");
                    await ApplyAsync(stage, instruction, inputs, cancellationToken).ConfigureAwait(false);
                }
            }

            var image = await FinishAsync(inputs.Stages[target.Index], reference, key, cancellationToken).ConfigureAwait(false);
            if (previous is not null && previous.Id != image.Id && !engine.IsImageInUse(previous.Id))
            {
                // The earlier build lost its name to this one; nothing else can use it.
                await RemoveQuietlyAsync(previous.Id, cancellationToken).ConfigureAwait(false);
            }

            log($"Built {reference.Familiar} ({image.ShortId}).");
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or InvalidDataException or JsonException or FormatException or NotSupportedException)
        {
            throw new ContainerException($"Building {reference.Familiar} failed: {ex.Message}", ex);
        }
        finally
        {
            if (Directory.Exists(work)) LayerUnpacker.Delete(work);
        }
    }

    /// <summary>Removes an image that lost its name to a newer build, unless something still needs it.</summary>
    /// <param name="imageId">The image's ID.</param>
    /// <param name="cancellationToken">Cancels before the index is changed.</param>
    internal async Task RemoveQuietlyAsync(string imageId, CancellationToken cancellationToken)
    {
        try
        {
            await engine.RemoveImageAsync(imageId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or IOException or ContainerException)
        {
        }
    }

    /// <summary>A digest of everything a build reads: the Dockerfile, the arguments and the context's files.</summary>
    private static string BuildKey(string dockerfile, BuildRequest request, string context, List<string> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
            hash.AppendData(Separator);
        }

        Add(dockerfile);
        foreach (var (name, value) in request.Arguments.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            Add(name);
            Add(value);
        }

        Add(request.Target ?? "");
        Add(request.Platform ?? "");
        foreach (var file in files)
        {
            Add(file);
            if (file.EndsWith('/')) continue;
            var info = new FileInfo(Path.Combine(context, file));
            Add(info.LinkTarget ?? (info.Exists ? info.Length.ToString(CultureInfo.InvariantCulture) + ":" + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) : "missing"));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>The arguments BuildKit defines for every build: <c>TARGETARCH</c>, <c>BUILDPLATFORM</c> and the rest.</summary>
    private static Dictionary<string, string> PredefinedArguments(OciPlatform target, OciPlatform build)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (prefix, platform) in new[] { ("TARGET", target), ("BUILD", build) })
        {
            result[prefix + "PLATFORM"] = platform.Os + "/" + platform.Architecture + (platform.Variant is { Length: > 0 } v ? "/" + v : "");
            result[prefix + "OS"] = platform.Os;
            result[prefix + "ARCH"] = platform.Architecture;
            result[prefix + "VARIANT"] = platform.Variant ?? "";
        }

        return result;
    }

    /// <summary>Marks a stage and every earlier stage it starts from or copies from as needed.</summary>
    private static void Need(Dockerfile dockerfile, DockerfileStage stage, Dictionary<string, string> globals, HashSet<int> needed)
    {
        if (!needed.Add(stage.Index)) return;
        if (FindStage(dockerfile, BuildWords.Expand(stage.BaseImage, globals, dockerfile.Escape), stage.Index) is { } parent)
        {
            Need(dockerfile, parent, globals, needed);
        }

        foreach (var instruction in stage.Instructions)
        {
            if (instruction.Keyword is "COPY" or "ADD" && instruction.Flag("from") is { } fromName
                && FindStage(dockerfile, BuildWords.Expand(fromName, globals, dockerfile.Escape), stage.Index) is { } copied)
            {
                Need(dockerfile, copied, globals, needed);
            }
        }
    }

    /// <summary>An earlier stage by its <c>AS</c> name or its number, or <see langword="null"/> for an image name.</summary>
    private static DockerfileStage? FindStage(Dockerfile dockerfile, string name, int before) =>
        dockerfile.Stage(name) is { } stage && stage.Index < before ? stage : null;

    /// <summary>Starts a stage from its base: an image, an earlier stage, or nothing (<c>scratch</c>).</summary>
    private async Task<Stage> BeginAsync(DockerfileStage definition, Inputs inputs, CancellationToken cancellationToken)
    {
        var escape = inputs.Dockerfile.Escape;
        var rootfs = Path.Combine(inputs.Work, "stage-" + definition.Index.ToString(CultureInfo.InvariantCulture));
        var baseName = BuildWords.Expand(definition.BaseImage, inputs.Globals, escape).Trim();
        var platform = definition.Platform is { } given ? BuildWords.Expand(given, inputs.Globals, escape) : inputs.Request.Platform;
        if (string.IsNullOrWhiteSpace(platform)) platform = null;
        Stage stage;
        if (FindStage(inputs.Dockerfile, baseName, definition.Index) is { } parentDefinition)
        {
            var parent = inputs.Stages.GetValueOrDefault(parentDefinition.Index) ?? throw new ContainerException($"Line {definition.Line}: stage {baseName} was not built.");
            await TreeCopy.CopyAsync(parent.Rootfs, rootfs, cancellationToken).ConfigureAwait(false);
            stage = new Stage(definition.Index, rootfs, parent.Platform) { Run = parent.Run };
            stage.Layers.AddRange(parent.Layers);
            stage.DiffIds.AddRange(parent.DiffIds);
            stage.History.AddRange(parent.History);
            foreach (var (name, value) in parent.Environment) stage.Environment[name] = value;
        }
        else if (string.Equals(baseName, "scratch", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(rootfs);
            stage = new Stage(definition.Index, rootfs, platform is null ? engine.Options.Host : OciPlatform.Parse(platform));
        }
        else
        {
            inputs.Log($"Using {baseName}");
            var image = await engine.ImageAsync(baseName, pull: true, platform, progress: null, cancellationToken).ConfigureAwait(false);
            if (image.Config.Config.OnBuild is { Count: > 0 })
            {
                throw new ContainerException($"Line {definition.Line}: {baseName} has ONBUILD instructions, which TabStudio's builder does not run.");
            }

            await TreeCopy.CopyAsync(engine.Store.RootfsOf(image.Id), rootfs, cancellationToken).ConfigureAwait(false);
            stage = new Stage(definition.Index, rootfs, image.Platform) { Run = image.Config.Config };
            stage.Layers.AddRange(image.Layers);
            stage.DiffIds.AddRange(image.Config.Rootfs.DiffIds);
            stage.History.AddRange(image.Config.History);
            foreach (var entry in image.Config.Config.Env ?? [])
            {
                var equals = entry.IndexOf('=', StringComparison.Ordinal);
                if (equals > 0) stage.Environment[entry[..equals]] = entry[(equals + 1)..];
            }
        }

        return stage;
    }

    /// <summary>Carries out one instruction of a stage.</summary>
    private async Task ApplyAsync(Stage stage, DockerfileInstruction instruction, Inputs inputs, CancellationToken cancellationToken)
    {
        var escape = inputs.Dockerfile.Escape;
        switch (instruction.Keyword)
        {
            case "RUN":
            {
                var before = LayerDiff.Snapshot(stage.Rootfs);
                await RunAsync(stage, instruction, inputs, cancellationToken).ConfigureAwait(false);
                await CommitAsync(stage, before, instruction, inputs.Work, cancellationToken).ConfigureAwait(false);
                return;
            }

            case "COPY" or "ADD":
            {
                var before = LayerDiff.Snapshot(stage.Rootfs);
                await CopyAsync(stage, instruction, inputs, cancellationToken).ConfigureAwait(false);
                await CommitAsync(stage, before, instruction, inputs.Work, cancellationToken).ConfigureAwait(false);
                return;
            }

            case "WORKDIR":
            {
                var path = BuildWords.Expand(instruction.Arguments, stage.Variables(), escape).Trim().Trim('"');
                var guest = "/" + string.Join('/', SafePath.Split(path.StartsWith('/') ? path : stage.WorkingDirectory.TrimEnd('/') + "/" + path));
                stage.Run = stage.Run with { WorkingDir = guest };
                var folder = SafePath.ResolveFollowing(stage.Rootfs, guest);
                if (Directory.Exists(folder))
                {
                    stage.Remember(instruction);
                    return;
                }

                var before = LayerDiff.Snapshot(stage.Rootfs);
                Directory.CreateDirectory(folder);
                await CommitAsync(stage, before, instruction, inputs.Work, cancellationToken).ConfigureAwait(false);
                return;
            }

            case "ENV":
                foreach (var (name, value) in BuildWords.Pairs(BuildWords.Expand(instruction.Arguments, stage.Variables(), escape), legacySpaceForm: true, escape))
                {
                    stage.Environment[name] = value ?? "";
                }

                break;
            case "ARG":
                foreach (var (name, value) in BuildWords.Pairs(BuildWords.Expand(instruction.Arguments, stage.Variables(), escape), legacySpaceForm: false, escape))
                {
                    // A build argument wins over the default; ARG without one takes the value of a global ARG.
                    if ((inputs.Request.Arguments.GetValueOrDefault(name) ?? value ?? inputs.Globals.GetValueOrDefault(name)) is { } given)
                    {
                        stage.Arguments[name] = given;
                    }
                }

                break;
            case "LABEL":
            {
                var labels = new Dictionary<string, string>(stage.Run.Labels ?? new Dictionary<string, string>(), StringComparer.Ordinal);
                foreach (var (name, value) in BuildWords.Pairs(BuildWords.Expand(instruction.Arguments, stage.Variables(), escape), legacySpaceForm: true, escape))
                {
                    labels[name] = value ?? "";
                }

                stage.Run = stage.Run with { Labels = labels };
                break;
            }

            case "USER":
                stage.Run = stage.Run with { User = BuildWords.Expand(instruction.Arguments, stage.Variables(), escape).Trim() };
                break;
            case "EXPOSE":
            {
                var ports = new Dictionary<string, JsonElement>(stage.Run.ExposedPorts ?? new Dictionary<string, JsonElement>(), StringComparer.Ordinal);
                foreach (var word in BuildWords.Words(BuildWords.Expand(instruction.Arguments, stage.Variables(), escape), escape))
                {
                    ports[word.Contains('/', StringComparison.Ordinal) ? word.ToLowerInvariant() : word + "/tcp"] = EmptyObject;
                }

                stage.Run = stage.Run with { ExposedPorts = ports };
                break;
            }

            case "VOLUME":
            {
                var volumes = new Dictionary<string, JsonElement>(stage.Run.Volumes ?? new Dictionary<string, JsonElement>(), StringComparer.Ordinal);
                var paths = instruction.JsonArguments ?? BuildWords.Words(BuildWords.Expand(instruction.Arguments, stage.Variables(), escape), escape);
                foreach (var path in paths) volumes[path] = EmptyObject;
                stage.Run = stage.Run with { Volumes = volumes };
                break;
            }

            case "CMD":
                stage.Run = stage.Run with { Cmd = CommandOf(stage, instruction) };
                stage.CommandSet = true;
                break;
            case "ENTRYPOINT":
                // As in Docker, an ENTRYPOINT drops the CMD the base image had, but not one this stage set.
                stage.Run = stage.Run with { Entrypoint = CommandOf(stage, instruction), Cmd = stage.CommandSet ? stage.Run.Cmd : null };
                break;
            case "SHELL":
            {
                var shell = instruction.JsonArguments is { Count: > 0 } words
                    ? new List<string>(words)
                    : throw new ContainerException($"Line {instruction.Line}: SHELL needs the JSON form, such as [\"/bin/bash\", \"-c\"].");
                stage.Run = stage.Run with { Shell = shell };
                break;
            }

            case "STOPSIGNAL":
                stage.Run = stage.Run with { StopSignal = BuildWords.Expand(instruction.Arguments, stage.Variables(), escape).Trim() };
                break;
            case "HEALTHCHECK":
                stage.Run = stage.Run with { Healthcheck = HealthcheckOf(instruction) };
                break;
            case "ONBUILD":
                stage.OnBuild.Add(instruction.Arguments.Trim());
                break;
            default:
                // MAINTAINER, and what the parser knows but changes nothing here.
                break;
        }

        stage.Remember(instruction);
    }

    /// <summary>The program of <c>CMD</c> or <c>ENTRYPOINT</c>: its JSON form, or the shell form run by the shell.</summary>
    private static List<string> CommandOf(Stage stage, DockerfileInstruction instruction) =>
        instruction.JsonArguments is { } json ? new List<string>(json) : [.. stage.Shell, instruction.Arguments.Trim()];

    /// <summary>A <c>HEALTHCHECK</c>: <c>NONE</c>, or its options and its <c>CMD</c>.</summary>
    private static ImageHealthcheck HealthcheckOf(DockerfileInstruction instruction)
    {
        var arguments = instruction.Arguments.Trim();
        if (arguments.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return new ImageHealthcheck { Test = ["NONE"] };
        }

        if (!arguments.StartsWith("CMD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ContainerException($"Line {instruction.Line}: HEALTHCHECK needs NONE or CMD.");
        }

        var command = arguments[3..].Trim();
        var json = new DockerfileInstruction("CMD", [], command, instruction.Line).JsonArguments;
        var test = new List<string> { json is null ? "CMD-SHELL" : "CMD" };
        if (json is null)
        {
            test.Add(command);
        }
        else
        {
            test.AddRange(json);
        }

        return new ImageHealthcheck
        {
            Test = test,
            Interval = Nanoseconds(instruction.Flag("interval"), instruction.Line),
            Timeout = Nanoseconds(instruction.Flag("timeout"), instruction.Line),
            StartPeriod = Nanoseconds(instruction.Flag("start-period"), instruction.Line),
            StartInterval = Nanoseconds(instruction.Flag("start-interval"), instruction.Line),
            Retries = instruction.Flag("retries") is { } retries && int.TryParse(retries, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null,
        };
    }

    /// <summary>A Go duration such as <c>1m30s</c> or <c>500ms</c>, in nanoseconds; <see langword="null"/> when not given.</summary>
    private static long? Nanoseconds(string? text, int line)
    {
        if (string.IsNullOrEmpty(text)) return null;
        double total = 0;
        var i = 0;
        while (i < text.Length)
        {
            var start = i;
            while (i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] == '.')) i++;
            if (start == i || !double.TryParse(text.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new ContainerException($"Line {line}: '{text}' is not a duration.");
            }

            var unitStart = i;
            while (i < text.Length && char.IsAsciiLetter(text[i])) i++;
            total += number * (text[unitStart..i] switch
            {
                "h" => 3_600_000_000_000d,
                "m" => 60_000_000_000d,
                "s" => 1_000_000_000d,
                "ms" => 1_000_000d,
                "us" => 1_000d,
                "ns" => 1d,
                _ => throw new ContainerException($"Line {line}: '{text}' is not a duration."),
            });
        }

        return (long)total;
    }

    /// <summary>Runs a <c>RUN</c> step in the stage's files under PRoot, with the device's network.</summary>
    private async Task RunAsync(Stage stage, DockerfileInstruction instruction, Inputs inputs, CancellationToken cancellationToken)
    {
        var options = engine.Options;
        var temporary = new List<string>();
        foreach (var mount in instruction.FlagValues("mount"))
        {
            var settings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in mount.Split(','))
            {
                var pair = part.Split('=', 2);
                settings[pair[0].Trim()] = pair.Length == 2 ? pair[1].Trim() : "true";
            }

            var type = settings.GetValueOrDefault("type") ?? "bind";
            var target = settings.GetValueOrDefault("target") ?? settings.GetValueOrDefault("dst") ?? settings.GetValueOrDefault("destination");
            if (type is not ("cache" or "tmpfs") || target is null)
            {
                throw new ContainerException($"Line {instruction.Line}: RUN --mount=type={type} is not supported by TabStudio's builder; only cache and tmpfs mounts are.");
            }

            // A cache or tmpfs folder is an ordinary folder for this step, and is not left in the image.
            var folder = SafePath.ResolveFollowing(stage.Rootfs, target.StartsWith('/') ? target : stage.WorkingDirectory.TrimEnd('/') + "/" + target);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                temporary.Add(folder);
            }
        }

        List<string> command;
        if (instruction.Heredocs.Count > 0)
        {
            var head = instruction.Arguments.Trim();
            var script = head.Split(Blanks, StringSplitOptions.RemoveEmptyEntries) is [var only] && only.StartsWith("<<", StringComparison.Ordinal)
                ? instruction.Heredocs[0].Content
                : head + "\n" + string.Concat(instruction.Heredocs.Select(h => h.Content + h.Name + "\n"));
            command = [.. stage.Shell, script];
        }
        else
        {
            command = instruction.JsonArguments is { } json ? new List<string>(json) : [.. stage.Shell, instruction.Arguments];
        }

        var user = ContainerUser.Resolve(stage.Rootfs, stage.Run.User);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = ProotRuntime.SearchPath,
            ["HOME"] = user.Home,
        };
        foreach (var (name, value) in stage.Arguments) environment[name] = value;
        foreach (var (name, value) in stage.Environment) environment[name] = value;
        var spec = ContainerEngine.ProcessSpec(stage.Rootfs, command, stage.WorkingDirectory, environment, user, [], terminal: null);
        var extra = new List<string>();
        if (stage.Platform.NeedsEmulation(options.Host))
        {
            var emulator = options.Emulator ?? throw new ContainerException($"Line {instruction.Line}: this stage is for {stage.Platform}, which needs Box64 to run; it comes with the Microchip PIC toolchain in the Toolchain Store.");
            if (emulator.Problem(stage.Platform, stage.Rootfs) is { } problem) throw new ContainerException(problem);
            extra.AddRange(emulator.ProotArguments);
            spec = emulator.Wrap(spec);
        }

        var temporaryFolder = Path.Combine(engine.Store.TemporaryFolder, "proot");
        Directory.CreateDirectory(temporaryFolder);
        var runtime = new ProotRuntime(new ProotRuntimeOptions
        {
            ProotPath = options.ProotPath ?? "",
            LoaderPath = options.LoaderPath ?? "",
            Rootfs = stage.Rootfs,
            TemporaryDirectory = temporaryFolder,
            DisplayName = "Dockerfile build",
            Shell = stage.Shell[0],
            SearchPath = environment["PATH"],
            ExtraArguments = extra,
            ShareWorkingDirectory = false,
            ProcStandInRoot = options.ProcStandInRoot,
        });
        var provided = ProvideNetworkFiles(stage.Rootfs, options);
        try
        {
            var result = await CommandRunner.RunAsync(runtime, spec, inputs.Log, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new ContainerException($"Line {instruction.Line}: RUN {FirstLine(instruction.Arguments)} {result.Exit.Description}.\n{result.Tail()}");
            }
        }
        catch (ToolchainRuntimeException ex)
        {
            throw new ContainerException($"Line {instruction.Line}: RUN could not start: {ex.Message}", ex);
        }
        finally
        {
            RestoreNetworkFiles(provided);
            foreach (var folder in temporary)
            {
                if (Directory.Exists(folder)) LayerUnpacker.Delete(folder);
            }
        }
    }

    /// <summary>
    /// Gives a step the device's DNS servers and a <c>localhost</c> while it runs, as Docker does, and keeps what
    /// the image had to put back afterwards: they are never part of the image.
    /// </summary>
    private static List<(string Path, string? Saved)> ProvideNetworkFiles(string rootfs, ContainerEngineOptions options)
    {
        var etc = SafePath.ResolveFollowing(rootfs, "/etc");
        Directory.CreateDirectory(etc);
        var dns = options.DnsServers();
        if (dns.Count == 0 && File.Exists("/etc/resolv.conf"))
        {
            dns = [.. File.ReadLines("/etc/resolv.conf").Where(l => l.StartsWith("nameserver ", StringComparison.Ordinal)).Select(l => l[11..].Trim())
                .Where(a => !a.StartsWith("127.", StringComparison.Ordinal))];
        }

        if (dns.Count == 0) dns = ["8.8.8.8", "1.1.1.1"];
        return
        [
            Provide(Path.Combine(etc, "resolv.conf"), string.Concat(dns.Select(d => "nameserver " + d + "\n"))),
            Provide(Path.Combine(etc, "hosts"), "127.0.0.1\tlocalhost\n::1\tlocalhost ip6-localhost ip6-loopback\n"),
        ];
    }

    /// <summary>Writes a file for the length of a step, keeping the image's own file beside it.</summary>
    private static (string Path, string? Saved) Provide(string path, string text)
    {
        string? saved = null;
        if (File.Exists(path) || new FileInfo(path).LinkTarget is not null)
        {
            saved = path + ".tabstudio-build";
            File.Move(path, saved, overwrite: true);
        }

        File.WriteAllText(path, text);
        return (path, saved);
    }

    /// <summary>Puts back what <see cref="ProvideNetworkFiles"/> replaced.</summary>
    private static void RestoreNetworkFiles(List<(string Path, string? Saved)> files)
    {
        foreach (var (path, saved) in files)
        {
            if (File.Exists(path) || new FileInfo(path).LinkTarget is not null) File.Delete(path);
            if (saved is not null) File.Move(saved, path);
        }
    }

    /// <summary>Carries out <c>COPY</c> or <c>ADD</c>: from the context, an earlier stage, an image, a here-document or a URL.</summary>
    private async Task CopyAsync(Stage stage, DockerfileInstruction instruction, Inputs inputs, CancellationToken cancellationToken)
    {
        var escape = inputs.Dockerfile.Escape;
        var variables = stage.Variables();
        List<string> words = instruction.JsonArguments is { } json
            ? [.. json.Select(w => BuildWords.Expand(w, variables, escape))]
            : BuildWords.Words(BuildWords.Expand(instruction.Arguments, variables, escape), escape);
        if (words.Count < 2) throw new ContainerException($"Line {instruction.Line}: {instruction.Keyword} needs a source and a destination.");
        var destination = words[^1];
        var guestDestination = destination.StartsWith('/') ? destination : stage.WorkingDirectory.TrimEnd('/') + "/" + destination;
        var mode = ModeOf(instruction.Flag("chmod"), instruction.Line);
        string? root = null;
        if (instruction.Flag("from") is { } fromFlag)
        {
            var fromName = BuildWords.Expand(fromFlag, variables, escape).Trim();
            if (FindStage(inputs.Dockerfile, fromName, stage.Index) is { } other && inputs.Stages.TryGetValue(other.Index, out var built))
            {
                root = built.Rootfs;
            }
            else
            {
                var image = await engine.ImageAsync(fromName, pull: true, platform: null, progress: null, cancellationToken).ConfigureAwait(false);
                root = engine.Store.RootfsOf(image.Id);
            }
        }

        var sources = new List<Source>();
        var heredocs = new List<Heredoc>();
        var downloads = new List<Uri>();
        foreach (var word in words.Take(words.Count - 1))
        {
            if (word.StartsWith("<<", StringComparison.Ordinal) && HeredocFor(instruction, word) is { } heredoc)
            {
                heredocs.Add(heredoc);
            }
            else if (instruction.Keyword == "ADD" && root is null
                && (word.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || word.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                downloads.Add(Uri.TryCreate(word, UriKind.Absolute, out var address) ? address : throw new ContainerException($"Line {instruction.Line}: {word} is not a valid address."));
            }
            else
            {
                sources.AddRange(root is null ? ContextSources(inputs, word, instruction.Line) : RootSources(root, word, instruction.Line));
            }
        }

        // Several sources, or a destination that ends in / or is a folder already, go into the destination folder.
        var intoFolder = destination.EndsWith('/') || sources.Count + heredocs.Count + downloads.Count > 1
            || Directory.Exists(SafePath.ResolveFollowing(stage.Rootfs, guestDestination));
        foreach (var source in sources)
        {
            if (Directory.Exists(source.Device) && new FileInfo(source.Device).LinkTarget is null)
            {
                CopyFolder(source, stage.Rootfs, guestDestination, source.Relative is null ? null : inputs.Ignore, mode);
            }
            else if (instruction.Keyword == "ADD" && IsArchive(source.Device))
            {
                var folder = SafePath.ResolveFollowing(stage.Rootfs, guestDestination);
                Directory.CreateDirectory(folder);
                await using var archive = File.OpenRead(source.Device);
                await LayerUnpacker.ApplyAsync(archive, null, folder, null, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                CopyFile(source.Device, stage.Rootfs, intoFolder ? guestDestination.TrimEnd('/') + "/" + Path.GetFileName(source.Device) : guestDestination, mode);
            }
        }

        foreach (var heredoc in heredocs)
        {
            var target = Prepare(stage.Rootfs, intoFolder ? guestDestination.TrimEnd('/') + "/" + heredoc.Name : guestDestination);
            await File.WriteAllTextAsync(target, heredoc.Expand ? BuildWords.Expand(heredoc.Content, variables, escape) : heredoc.Content, cancellationToken).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(target, mode ?? (UnixFileMode)0b110_100_100);
        }

        foreach (var address in downloads)
        {
            var name = Path.GetFileName(address.AbsolutePath);
            var target = Prepare(stage.Rootfs, intoFolder || name.Length == 0 ? guestDestination.TrimEnd('/') + "/" + (name.Length == 0 ? "index.html" : name) : guestDestination);
            inputs.Log($"Downloading {address}");
            using var response = await Http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var file = File.Create(target))
            {
                await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(target, mode ?? (UnixFileMode)0b110_000_000);
        }
    }

    /// <summary>The here-document a source word such as <c>&lt;&lt;EOF</c> names.</summary>
    private static Heredoc? HeredocFor(DockerfileInstruction instruction, string word)
    {
        var name = word.TrimStart('<').TrimStart('-').Trim('"', '\'');
        return instruction.Heredocs.FirstOrDefault(h => h.Name == name);
    }

    /// <summary>Whether <c>ADD</c> unpacks a local file instead of copying it: a tar archive, compressed with gzip or not.</summary>
    private static bool IsArchive(string path) =>
        path.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);

    /// <summary><c>--chmod</c> as octal digits, such as <c>755</c>; <see langword="null"/> to keep each file's own.</summary>
    private static UnixFileMode? ModeOf(string? text, int line)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var value = 0;
        foreach (var digit in text)
        {
            if (digit is < '0' or > '7' || value > 0xFFF) throw new ContainerException($"Line {line}: --chmod={text} is not an octal mode.");
            value = (value * 8) + (digit - '0');
        }

        return (UnixFileMode)value;
    }

    /// <summary>The files and folders a source in the build context names, wildcards and <c>.dockerignore</c> applied.</summary>
    private static List<Source> ContextSources(Inputs inputs, string word, int line)
    {
        var relative = string.Join('/', SafePath.Split(word));
        if (relative.Length == 0) return [new Source(inputs.Context, "")];
        if (relative.IndexOfAny(Wildcards) >= 0)
        {
            var pattern = new Regex("^" + DockerIgnore.Translate(relative) + "$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var matched = inputs.Files.Select(f => f.TrimEnd('/')).Where(f => pattern.IsMatch(f)).Distinct(StringComparer.Ordinal)
                .Select(f => new Source(Path.Combine(inputs.Context, f), f)).ToList();
            return matched.Count > 0 ? matched : throw new ContainerException($"Line {line}: nothing in the build context matches {word}.");
        }

        if (inputs.Ignore.IsExcluded(relative)) throw new ContainerException($"Line {line}: {word} is left out of the build context by .dockerignore.");
        var device = Path.Combine(inputs.Context, relative);
        if (!File.Exists(device) && !Directory.Exists(device)) throw new ContainerException($"Line {line}: {word} is not in the build context.");
        return [new Source(device, relative)];
    }

    /// <summary>The files and folders a source names in an earlier stage's or an image's files.</summary>
    private static List<Source> RootSources(string root, string word, int line)
    {
        var parts = SafePath.Split(word);
        if (parts.Count > 0 && parts[^1].IndexOfAny(Wildcards) >= 0)
        {
            var folder = SafePath.ResolveFollowing(root, string.Join('/', parts.Take(parts.Count - 1)));
            var matched = Directory.Exists(folder)
                ? Directory.EnumerateFileSystemEntries(folder, parts[^1]).Order(StringComparer.Ordinal).Select(p => new Source(p, null)).ToList()
                : [];
            return matched.Count > 0 ? matched : throw new ContainerException($"Line {line}: nothing matches {word}.");
        }

        var device = SafePath.ResolveFollowing(root, word);
        if (!File.Exists(device) && !Directory.Exists(device)) throw new ContainerException($"Line {line}: {word} does not exist there.");
        return [new Source(device, null)];
    }

    /// <summary>
    /// Makes a path in the stage's files ready to be written: its folders exist, and whatever was there (a file, or a
    /// link that might lead outside) is gone.
    /// </summary>
    private static string Prepare(string rootfs, string guest)
    {
        var target = SafePath.Resolve(rootfs, guest);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target) || new FileInfo(target).LinkTarget is not null) LayerUnpacker.Delete(target);
        return target;
    }

    /// <summary>Copies one file into the stage's files.</summary>
    private static void CopyFile(string source, string rootfs, string guest, UnixFileMode? mode)
    {
        var target = Prepare(rootfs, guest);
        File.Copy(source, target);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(target, mode ?? File.GetUnixFileMode(source));
    }

    /// <summary>Copies what a folder holds into a folder of the stage's files, keeping links and leaving out ignored paths.</summary>
    private static void CopyFolder(Source source, string rootfs, string guestTarget, DockerIgnore? ignore, UnixFileMode? mode)
    {
        var folders = new List<(string Device, UnixFileMode Mode)>();
        var top = SafePath.ResolveFollowing(rootfs, guestTarget);
        Directory.CreateDirectory(top);
        var pending = new Stack<(string Device, string Guest, string? Relative)>();
        pending.Push((source.Device, guestTarget, source.Relative));
        while (pending.TryPop(out var folder))
        {
            foreach (var entry in new DirectoryInfo(folder.Device).EnumerateFileSystemInfos())
            {
                var relative = folder.Relative is null ? null : folder.Relative.Length == 0 ? entry.Name : folder.Relative + "/" + entry.Name;
                var excluded = relative is not null && ignore is not null && ignore.IsExcluded(relative);
                var guest = folder.Guest.TrimEnd('/') + "/" + entry.Name;
                if (entry.LinkTarget is { } link)
                {
                    if (excluded) continue;
                    File.CreateSymbolicLink(Prepare(rootfs, guest), link);
                }
                else if (entry is DirectoryInfo)
                {
                    if (excluded && (relative is null || ignore is null || !ignore.MayIncludeInside(relative))) continue;
                    var target = SafePath.ResolveFollowing(rootfs, guest);
                    if (File.Exists(target)) LayerUnpacker.Delete(target);
                    Directory.CreateDirectory(target);
                    if (!OperatingSystem.IsWindows()) folders.Add((target, mode ?? File.GetUnixFileMode(entry.FullName)));
                    pending.Push((entry.FullName, guest, relative));
                }
                else if (!excluded)
                {
                    CopyFile(entry.FullName, rootfs, guest, mode);
                }
            }
        }

        // Folder modes last, so a read-only folder is still filled first.
        if (OperatingSystem.IsWindows()) return;
        foreach (var (device, folderMode) in folders.AsEnumerable().Reverse()) File.SetUnixFileMode(device, folderMode);
    }

    /// <summary>Makes the step's changes a layer of the image, or records that it changed no files.</summary>
    private async Task CommitAsync(Stage stage, Dictionary<string, LayerDiff.Entry> before, DockerfileInstruction instruction, string work, CancellationToken cancellationToken)
    {
        var tar = Path.Combine(work, Guid.NewGuid().ToString("N") + ".tar");
        var (digest, size, empty) = await LayerDiff.WriteAsync(stage.Rootfs, before, tar, cancellationToken).ConfigureAwait(false);
        if (empty)
        {
            stage.Remember(instruction);
            return;
        }

        var blob = engine.Store.BlobPath(digest);
        if (File.Exists(blob))
        {
            File.Delete(tar);
        }
        else
        {
            File.Move(tar, blob);
        }

        stage.Layers.Add(new Descriptor { MediaType = MediaTypes.OciLayer, Digest = digest, Size = size });
        stage.DiffIds.Add(digest);
        stage.History.Add(new ImageHistory { Created = DateTimeOffset.UtcNow, CreatedBy = FirstLine(instruction.Original), Comment = "TabStudio" });
    }

    /// <summary>Writes the image's configuration, moves the stage's files into the store and names the image.</summary>
    private async Task<StoredImage> FinishAsync(Stage stage, ImageReference reference, string key, CancellationToken cancellationToken)
    {
        var labels = new Dictionary<string, string>(stage.Run.Labels ?? new Dictionary<string, string>(), StringComparer.Ordinal) { [BuildKeyLabel] = key };
        var config = new ImageConfigFile
        {
            Architecture = stage.Platform.Architecture,
            Os = stage.Platform.Os,
            Variant = stage.Platform.Variant,
            Created = DateTimeOffset.UtcNow,
            Config = stage.Run with
            {
                Env = [.. stage.Environment.Select(e => e.Key + "=" + e.Value)],
                Labels = labels,
                OnBuild = stage.OnBuild.Count > 0 ? new List<string>(stage.OnBuild) : null,
            },
            Rootfs = new ImageRootfs { DiffIds = [.. stage.DiffIds] },
            History = [.. stage.History],
        };
        var bytes = OciJson.Write(config);
        var id = RegistryClient.Digest(bytes);
        await engine.Store.WriteBlobAsync(id, bytes, cancellationToken).ConfigureAwait(false);
        var rootfs = engine.Store.RootfsOf(id);
        if (Directory.Exists(rootfs)) LayerUnpacker.Delete(rootfs);
        Directory.CreateDirectory(Path.GetDirectoryName(rootfs)!);
        Directory.Move(stage.Rootfs, rootfs);
        var size = TreeCopy.Size(rootfs);
        await File.WriteAllTextAsync(rootfs + ".ready", size.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        return await engine.Store.RecordAsync(new StoredImage
        {
            Id = id,
            Tags = [reference.Canonical],
            Platform = stage.Platform,
            Config = config,
            Layers = [.. stage.Layers],
            Size = size,
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? text;

    private static JsonElement ParseEmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    /// <summary>A source of <c>COPY</c>: its path on the device, and its path in the context when it is from there.</summary>
    private readonly record struct Source(string Device, string? Relative);

    /// <summary>What every step of one build reads.</summary>
    private sealed record Inputs(BuildRequest Request, Dockerfile Dockerfile, string Context, DockerIgnore Ignore, List<string> Files,
        Dictionary<string, string> Globals, Dictionary<int, Stage> Stages, string Work, Action<string> Log);

    /// <summary>A stage being built: its files, its settings and its layers so far.</summary>
    private sealed class Stage(int index, string rootfs, OciPlatform platform)
    {
        public int Index { get; } = index;
        public string Rootfs { get; } = rootfs;
        public OciPlatform Platform { get; } = platform;
        public ImageRunConfig Run { get; set; } = new();
        public bool CommandSet { get; set; }
        public List<Descriptor> Layers { get; } = [];
        public List<string> DiffIds { get; } = [];
        public List<ImageHistory> History { get; } = [];
        public List<string> OnBuild { get; } = [];
        public Dictionary<string, string> Environment { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Arguments { get; } = new(StringComparer.Ordinal);
        public string WorkingDirectory => Run.WorkingDir is { Length: > 0 } folder ? folder : "/";
        public IReadOnlyList<string> Shell => Run.Shell is { Count: > 0 } shell ? shell : DefaultShell;

        /// <summary>The variables instructions expand: the stage's arguments, overridden by its environment.</summary>
        public Dictionary<string, string> Variables()
        {
            var all = new Dictionary<string, string>(Arguments, StringComparer.Ordinal);
            foreach (var (name, value) in Environment) all[name] = value;
            return all;
        }

        /// <summary>Records a step that made no layer in the image's history.</summary>
        public void Remember(DockerfileInstruction instruction) =>
            History.Add(new ImageHistory { Created = DateTimeOffset.UtcNow, CreatedBy = FirstLine(instruction.Original), Comment = "TabStudio", EmptyLayer = true });
    }
}
