using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.App.Services;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;
using TabStudio.Toolchains.Runtimes;

// Compiles actual app-created starter files through the production tablet installer and PRoot runtime.
// This proves compiler/package/template compatibility. It never opens a USB device or flashes hardware.
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() },
};
var groups = JsonSerializer.Deserialize<Dictionary<string, string[]>>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "groups.json")))!;
var canonical = ProjectTemplates.CanonicalEmbeddedTemplates.Where(template => template.Tags.Contains("PlatformIO")).Select(template => template.Id[9..]).ToHashSet(StringComparer.Ordinal);
var covered = groups.Values.SelectMany(ids => ids).ToArray();
if (covered.Length != covered.Distinct(StringComparer.Ordinal).Count() || !canonical.SetEquals(covered))
{
    throw new InvalidDataException("Every canonical PlatformIO starter must occur in exactly one qualification group.");
}
if (args.Length == 3 && args[0] == "generate")
{
    var generationRoot = Path.GetFullPath(args[1]);
    foreach (var id in Selected(args[2]))
    {
        var project = Path.Combine(generationRoot, id);
        ProjectTemplates.CreateForVerification("embedded-" + id, project, "Firmware");
        Console.WriteLine(id + ": " + project);
    }
    return 0;
}
if (args.Length is not (5 or 6))
{
    throw new ArgumentException("Supply PRoot, its loader, a private work directory, evidence directory, group and optional auto/native package mode; or generate DIRECTORY GROUP.");
}
var proot = Path.GetFullPath(args[0]);
var loader = Path.GetFullPath(args[1]);
var work = Path.GetFullPath(args[2]);
var output = Path.GetFullPath(args[3]);
var group = args[4];
var mode = args.Length == 6 ? args[5] : "auto";
if (mode is not ("auto" or "native" or "boxed-x86_64")) throw new ArgumentException("Unknown host package mode.");
Directory.CreateDirectory(work);
Directory.CreateDirectory(output);
var projects = Path.Combine(work, "projects");
var cache = Path.Combine(work, "download-cache");
Directory.CreateDirectory(projects);
Directory.CreateDirectory(cache);
var selected = Selected(group);
var templates = new Dictionary<string, string>(StringComparer.Ordinal);
var files = new Dictionary<string, object>(StringComparer.Ordinal);
foreach (var id in selected)
{
    var project = Path.Combine(projects, id);
    templates.Add(id, ProjectTemplates.CreateForVerification("embedded-" + id, project, "Firmware"));
    files[id] = Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
        .Select(path => new { path = Path.GetRelativePath(project, path), bytes = new FileInfo(path).Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) }).ToArray();
}
await File.WriteAllTextAsync(Path.Combine(output, "canonical-files.json"), JsonSerializer.Serialize(files, json));
var native = Path.Combine(work, "native");
Directory.CreateDirectory(native);
foreach (var (source, name) in new[] { (proot, "libproot.so"), (loader, "libproot-loader.so") })
{
    var path = Path.Combine(native, name);
    if (!File.Exists(path)) File.CreateSymbolicLink(path, source);
}
var platform = new ToolchainPlatform
{
    NativeLibraryDirectory = native, Architecture = ToolchainPlatform.CurrentArchitecture, AndroidApiLevel = 35,
    SharedFolders = [projects, cache], DnsServers = ReadDns,
};
using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
http.DefaultRequestHeaders.UserAgent.ParseAdd("TabStudio-Canonical-Embedded-Templates/1.0");
using var installer = new EmbeddedGlibcToolchainInstaller(new HostToolchainInstaller(new HostRuntime()),
    Path.Combine(work, "sdk"), platform, new Downloader(http), [projects, cache]);
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(100));
var cancellation = deadline.Token;
var toolchain = ToolchainCatalog.Default.Find("platformio")!;
var started = Stopwatch.StartNew();
var results = new List<object>();
var errors = new List<string>();
try
{
    await installer.InstallAsync(toolchain, new InstallOutput(Path.Combine(output, "installation.log")), cancellation);
    if (!installer.IsToolchainReady("platformio")) throw new InvalidOperationException("The production Store readiness check did not accept PlatformIO.");
    if (mode is "auto" or "boxed-x86_64")
    {
        await installer.PrepareBox64Async(Console.WriteLine, cancellation);
    }
    foreach (var (id, path) in templates)
    {
        var platformPackage = File.ReadLines(Path.Combine(path, "platformio.ini"))
            .Single(line => line.StartsWith("platform = ", StringComparison.Ordinal))[11..].Trim();
        var logPath = Path.Combine(output, id + ".log");
        await using var log = new StreamWriter(logPath);
        var watch = Stopwatch.StartNew();
        try
        {
            var project = EmbeddedProjectDetector.Read(path) ?? throw new InvalidOperationException("The canonical project was not detected.");
            var plan = EmbeddedBuildPlanner.Plan(project, new() { Jobs = 2, HostPackageMode = mode });
            if (project.Kind != EmbeddedProjectKind.PlatformIO || plan.Upload.Count != 0)
                throw new InvalidOperationException("Qualification must build PlatformIO firmware and never upload it.");
            foreach (var command in plan.Preparation.Concat(plan.Build))
            {
                var environment = new Dictionary<string, string?>(command.Environment)
                {
                    // Fresh per-job cache: Core verifies registry SHA-256 on first download; installed choices remain project-private.
                    ["PLATFORMIO_CACHE_DIR"] = cache,
                };
                var actual = command with { Environment = environment };
                await log.WriteLineAsync("> " + actual.CommandLine);
                var result = await CommandRunner.RunAsync(installer.Runtime, actual, line =>
                {
                    log.WriteLine(line);
                    log.Flush();
                    Console.WriteLine("[" + id + "] " + line);
                }, cancellation);
                if (!result.Succeeded) throw new InvalidOperationException(actual.Program + " failed: " + result.Tail(15));
                var diagnostics = new ProblemMatcher(toolchain).FindErrors(result.Output.Split('\n'), _ => true);
                if (diagnostics.Count != 0) throw new InvalidOperationException("The production Error List parser found " + diagnostics.Count + " build errors.");
            }
            var elf = Path.Combine(path, ".pio", "build", id, "firmware.elf");
            var bytes = await File.ReadAllBytesAsync(elf, cancellation);
            if (bytes.Length < 52 || !bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0x7f, 69, 76, 70 }))
                throw new InvalidDataException("The actual build did not produce ELF firmware.");
            var ranges = FirmwareImageInspector.Elf(bytes);
            if (ranges.Count == 0) throw new InvalidDataException("The actual ELF contains no initialized load bytes.");
            File.Copy(elf, Path.Combine(output, id + ".elf"), overwrite: true);
            var uploadReady = false;
            var profilePath = Path.Combine(path, ".tabstudio", "embedded-flash.json");
            string? uploadVerification = null;
            if (File.Exists(profilePath))
            {
                var request = JsonSerializer.Deserialize<EmbeddedFlashRequest>(await File.ReadAllTextAsync(profilePath, cancellation), json)!;
                request = request with
                {
                    WorkingDirectory = path, FirmwarePath = Path.GetFullPath(request.FirmwarePath, path),
                    EspFlasherArguments = request.EspFlasherArguments is { } metadata ? Path.GetFullPath(metadata, path) : null,
                    // Dry-plan with a clearly synthetic selected ST-Link. No USB connection is opened.
                    ProbeSelector = request.Protocol == EmbeddedFlashProtocol.ProbeRs ? "0483:3748:CI_DRY_PLAN_ONLY" : request.ProbeSelector,
                };
                var upload = EmbeddedFlashPlanner.Plan(request); // Dry plan only: parsed real image and actual post-build metadata.
                uploadReady = upload.Steps.Count > 0;
                uploadVerification = upload.Verification;
            }
            var stackFiles = Directory.EnumerateFiles(Path.Combine(path, ".pio"), "*.su", SearchOption.AllDirectories).ToArray();
            var stack = stackFiles.SelectMany(file => StackAnalyzer.ParseGccStackUsage(File.ReadAllText(file))).ToArray();
            var stackRows = stack.Length;
            if (stackRows == 0) throw new InvalidDataException("The actual app build emitted no parseable function stack usage.");
            await File.WriteAllTextAsync(Path.Combine(output, id + "-stack-usage.json"), JsonSerializer.Serialize(stack, json), cancellation);
            var manifests = PackageManifests(path);
            await File.WriteAllTextAsync(Path.Combine(output, id + "-package-manifests.json"), JsonSerializer.Serialize(manifests, json), cancellation);
            results.Add(new
            {
                id, platformPackage, passed = true, seconds = watch.Elapsed.TotalSeconds, firmwareBytes = bytes.Length,
                firmwareSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), ranges, uploadPlanReady = uploadReady,
                uploadVerification, stackFiles = stackFiles.Length, stackRows,
                platformioIni = await File.ReadAllTextAsync(Path.Combine(path, "platformio.ini"), cancellation),
            });
            Console.WriteLine("CANONICAL_TEMPLATE_BUILT " + id + " " + platform.Architecture);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or JsonException or NotSupportedException or ArgumentException)
        {
            errors.Add(id + ": " + error.Message);
            results.Add(new { id, platformPackage, passed = false, error = error.Message, seconds = watch.Elapsed.TotalSeconds });
            await log.WriteLineAsync(error.ToString());
            Console.Error.WriteLine("CANONICAL_TEMPLATE_FAILED " + id + ": " + error.Message);
        }
    }
    if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
    Console.WriteLine("CANONICAL_TEMPLATE_GROUP_PASSED " + group + " " + platform.Architecture);
    return 0;
}
catch (Exception error)
{
    errors.Add(error.Message);
    Console.Error.WriteLine(error);
    return 1;
}
finally
{
    await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new
    {
        architecture = platform.Architecture, group, hostPackageMode = mode, platformioCoreVersion = toolchain.PinnedVersion, hardwareVerified = false, androidVerified = false,
        runtime = "Production EmbeddedGlibcToolchainInstaller / PRoot", elapsedSeconds = started.Elapsed.TotalSeconds,
        passed = errors.Count == 0, templates = results, errors,
    }, json));
}

string[] Selected(string name) => name == "all" ? covered : groups.TryGetValue(name, out var ids) ? ids : throw new ArgumentException("Unknown template group: " + name);

static IReadOnlyList<string> ReadDns() => File.ReadLines("/etc/resolv.conf")
    .Where(line => line.StartsWith("nameserver ", StringComparison.Ordinal))
    .Select(line => line[11..].Trim()).Where(value => value.Length > 0).ToArray();

static object[] PackageManifests(string project)
{
    var core = Path.Combine(project, ".tabstudio/platformio-boxed");
    if (!Directory.Exists(core)) return [];
    var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
    return Directory.EnumerateFiles(core, "*", options)
        .Where(file => Path.GetFileName(file) is "package.json" or "platform.json" or ".piopm")
        .Order(StringComparer.Ordinal).Select(file =>
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length > 1024 * 1024) throw new InvalidDataException("Package manifest exceeds 1 MiB.");
            return (object)new
            {
                path = Path.GetRelativePath(core, file), sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                manifest = JsonSerializer.Deserialize<JsonElement>(bytes),
            };
        }).ToArray();
}

sealed class InstallOutput(string path) : IProgress<InstallProgress>
{
    private readonly object _gate = new();
    public void Report(InstallProgress value)
    {
        lock (_gate)
        {
            File.AppendAllText(path, value.Stage + ": " + value.Message + "\n");
            Console.WriteLine(value.Stage + ": " + value.Message);
        }
    }
}
