using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TabStudio.Core.Diagnostics;
using TabStudio.Toolchains.Microchip;
using TabStudio.WindowsBridge.Builds;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>Uses MPLAB's generator, make and IPE; no approximation of compiler options.</summary>
internal static class MplabBuilder
{
    public static bool IsDescriptor(string path) => string.Equals(Path.GetFileName(path), "configurations.xml", StringComparison.OrdinalIgnoreCase)
        && string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "nbproject", StringComparison.OrdinalIgnoreCase);

    public static async Task<BuildOutcome> BuildAsync(BuildRequest request, string descriptor, BridgePathMapper paths, MplabTools? tools, Action<string> output, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        var project = MplabProject.Load(descriptor);
        var configuration = project.Configurations.FirstOrDefault(c => c.Name == request.Configuration)
            ?? (request.Configuration is "Debug" or "Release" ? project.Configurations.FirstOrDefault(c => c.Name == "default") ?? project.Configurations[0] : null)
            ?? throw new BridgeBuildException("Unknown MPLAB configuration: " + request.Configuration);
        var name = Path.GetFileName(project.Directory);
        var diagnostics = new List<BuildDiagnostic>();
        var gate = new Lock();
        void Line(string line)
        {
            output(paths.ToClientText(line));
            foreach (var item in PicCompilerDiagnostic.Parse(line))
            {
                var message = new MsBuildMessage(item.File, item.Line, item.Column, item.Severity, "XC", item.Message, name);
                lock (gate) diagnostics.Add(paths.ToDiagnostic(message, project.Directory, name));
            }
        }

        try
        {
            if (tools?.Make is null || tools.Generator is null) throw new BridgeBuildException("Install MPLAB X IDE and the project's XC compiler/device packs on the paired PC. No Microchip software is downloaded by the Bridge.");
            // .bat is the vendor's only Windows generator entrypoint. Refuse cmd
            // metacharacters rather than let a project path become shell syntax.
            if ((tools.Generator + project.Directory + configuration.Name).Any(c => "\"%!&|<>^\r\n".Contains(c, StringComparison.Ordinal)))
                throw new BridgeBuildException("MPLAB's batch generator requires project/tool paths without shell metacharacters.");
            var generate = $"\"{tools.Generator}\" \"{project.Directory}@{configuration.Name}\"";
            var environment = new Dictionary<string, string?> { ["PATH"] = Path.GetDirectoryName(tools.Make) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH") };
            var code = await BridgeBuilder.RunAsync("cmd.exe", [], project.Directory, Line, token, environment, "/d /s /c \"" + generate + "\"").ConfigureAwait(false);
            if (code != 0) throw new BridgeBuildException($"MPLAB's makefile generator exited {code}. See the Output panel.");
            var file = "nbproject/Makefile-" + configuration.Name + ".mk";
            if (!File.Exists(Path.Combine(project.Directory, file))) throw new BridgeBuildException("MPLAB did not generate the selected configuration's makefile.");
            code = await BridgeBuilder.RunAsync(tools.Make, ["-f", file, "SUBPROJECTS=", ".build-conf", "CND_CONF=" + configuration.Name, "TYPE_IMAGE=PRODUCTION"], project.Directory, Line, token, environment).ConfigureAwait(false);
            if (code != 0) throw new BridgeBuildException($"MPLAB build exited {code}. See the Output panel.");
            var directory = Path.Combine(project.Directory, "dist", configuration.Name, "production");
            var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.hex") : [];
            if (files.Length != 1) throw new BridgeBuildException("The MPLAB build did not produce one unambiguous production HEX image.");
            var receipt = Path.Combine(project.Directory, ".tabstudio", "pic-bridge-build.json");
            Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
            var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(files[0], token).ConfigureAwait(false)));
            await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new Dictionary<string, string> { ["device"] = configuration.Device, ["configuration"] = configuration.Name, ["firmware"] = files[0], ["sha256"] = hash }), token).ConfigureAwait(false);
            lock (gate)
            {
                return new BuildOutcome(diagnostics.All(d => d.Severity != DiagnosticSeverityLevel.Error), diagnostics.ToArray(), clock.Elapsed, "MPLAB X / " + configuration.Compiler,
                    new BuiltProgram(paths.ToClient(files[0]), null, IsDotNet: false));
            }
        }
        catch (Exception error) when (error is BridgeBuildException or BridgeProcessException or IOException)
        {
            Line("MPLAB: error: " + error.Message);
            lock (gate)
            {
                diagnostics.Add(new BuildDiagnostic("TS3003", DiagnosticSeverityLevel.Error, error.Message, null, name));
                return new BuildOutcome(false, diagnostics.ToArray(), clock.Elapsed, "MPLAB X", null);
            }
        }
    }

    public static async Task<BuildOutcome> ProgramAsync(PicProgramRequest request, BridgePathMapper paths, MplabTools? tools, Action<string> output, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var descriptor = paths.ToHost(request.ProjectPath) ?? throw new BridgeBuildException("The PIC project is outside the synchronized workspace.");
            var project = MplabProject.Load(descriptor);
            if (!project.Configurations.Any(c => c.Device.Equals(request.Device, StringComparison.OrdinalIgnoreCase))) throw new BridgeBuildException("The programming device does not match any MPLAB project configuration.");
            var firmware = paths.ToHost(request.FirmwarePath) ?? throw new BridgeBuildException("The HEX image is outside the synchronized workspace.");
            if (!firmware.EndsWith(".hex", StringComparison.OrdinalIgnoreCase) || !File.Exists(firmware)) throw new BridgeBuildException("Build the firmware on the Windows Bridge before programming.");
            using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(project.Directory, ".tabstudio", "pic-bridge-build.json"), token).ConfigureAwait(false));
            var built = receipt.RootElement;
            var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(firmware, token).ConfigureAwait(false)));
            if (!string.Equals(built.GetProperty("device").GetString(), request.Device, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(built.GetProperty("firmware").GetString(), firmware, StringComparison.OrdinalIgnoreCase)
                || built.GetProperty("sha256").GetString() != hash)
            {
                throw new BridgeBuildException("The image or device differs from the Bridge build receipt. Build the selected configuration again before programming.");
            }
            if (tools?.Ipe is null || (tools.Ipe.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && tools.Java is null)) throw new BridgeBuildException("Install MPLAB IPE (including its command-line tools and Java runtime) on the paired PC.");
            var command = tools.Ipe.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ? tools.Java! : tools.Ipe;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromMinutes(5));
            var code = await BridgeBuilder.RunAsync(command, tools.ProgrammingArguments(request.Device, firmware, request.Tool, request.Serial), project.Directory,
                line => output(paths.ToClientText(line)), deadline.Token).ConfigureAwait(false);
            if (code != 0) throw new BridgeBuildException($"ipecmd exited {code}; programming/verification failed.");
            return new BuildOutcome(true, [], clock.Elapsed, "MPLAB IPE", null);
        }
        catch (Exception error) when (error is BridgeBuildException or BridgeProcessException or IOException or JsonException or KeyNotFoundException)
        {
            output("ipecmd: error: " + error.Message);
            return new BuildOutcome(false, [new BuildDiagnostic("TS3004", DiagnosticSeverityLevel.Error, error.Message, null)], clock.Elapsed, "MPLAB IPE", null);
        }
    }
}
