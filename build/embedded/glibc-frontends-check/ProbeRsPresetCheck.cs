using System.Text.Json;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

/// <summary>The production launcher must override automatic unsafe CLI presets, without opening a physical probe.</summary>
internal static class ProbeRsPresetCheck
{
    private static readonly JsonSerializerOptions EvidenceJson = new() { WriteIndented = true };
    public static async Task VerifyAsync(IToolchainRuntime runtime, string project, string evidencePath, CancellationToken cancellationToken)
    {
        var home = Path.Combine(project, "preset-original-home-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        var projectConfig = Path.Combine(project, ".probe-rs.json");
        var previous = File.Exists(projectConfig) ? await File.ReadAllBytesAsync(projectConfig, cancellationToken) : null;
        const string sentinel = "TABSTUDIO_PRESET_MUST_NOT_BE_USED.yaml";
        var hostile = JsonSerializer.Serialize(new
        {
            presets = new Dictionary<string, object>
            {
                ["default"] = new Dictionary<string, object>
                {
                    ["chip-description-path"] = Path.Combine(project, sentinel),
                    ["allow-erase-all"] = true, ["cycle-power"] = true,
                },
            },
        });
        var before = await DigestAsync();
        try
        {
            await File.WriteAllTextAsync(projectConfig, hostile, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(home, ".probe-rs.json"), hostile, cancellationToken);
            // USB VID zero is reserved. No physical probe can match this selector.
            // The operation is a read, and no flash/erase command is requested.
            var command = new ProcessStartSpec
            {
                Program = "probe-rs", WorkingDirectory = project,
                Arguments = ["read", "b8", "0x08000000", "1", "--chip", "STM32F401CCUx", "--probe", "0000:0000", "--non-interactive"],
            };
            var environment = new Dictionary<string, string?>(ProbeTargetInfoReader.SafeAttachEnvironment())
            {
                ["HOME"] = home, [ProbeRsSafeLaunch.Marker] = null,
            };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var ordinary = await CommandRunner.RunAsync(runtime, command with { Environment = environment }, null, deadline.Token);
            if (ordinary.Succeeded || !ordinary.Output.Contains(sentinel, StringComparison.Ordinal))
                throw new InvalidOperationException("The unchanged CLI did not establish the hostile automatic-preset baseline.");
            environment[ProbeRsSafeLaunch.Marker] = "1";
            var managed = await CommandRunner.RunAsync(runtime, command with { Environment = environment }, null, deadline.Token);
            if (managed.Succeeded || managed.Output.Contains(sentinel, StringComparison.Ordinal)
                || managed.Output.Contains("Config preset", StringComparison.Ordinal)
                || !managed.Output.Contains("probe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The production managed launcher did not bypass the hostile default preset before the impossible-probe failure: " + managed.Output);
            }
            var after = await DigestAsync();
            if (before != after) throw new InvalidOperationException("The preset launcher changed the official probe-rs executable.");
            await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(new
            {
                upstreamCommit = "48f5e4d53c690a1d40c2454033c6f785b4f4f95c", officialBinarySha256 = before,
                hostileProjectAndHomePresetAppliedWithoutMarker = true, managedEmptyNoncePresetSelected = true,
                physicalProbeRequested = false, firmwareWriteRequested = false, hardwareVerified = false,
                ordinaryExit = ordinary.Exit.ExitCode, managedExit = managed.Exit.ExitCode,
                managedOutput = managed.Output,
            }, EvidenceJson), cancellationToken);
            Console.WriteLine("PROBE_RS_MANAGED_NONCE_PRESET_PASSED binaryUnchanged=true physicalProbe=false");
        }
        finally
        {
            if (previous is null) File.Delete(projectConfig);
            else await File.WriteAllBytesAsync(projectConfig, previous, CancellationToken.None);
            Directory.Delete(home, true);
        }

        async Task<string> DigestAsync()
        {
            var result = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
            {
                Program = "python3", Arguments = ["-c", "import hashlib;print(hashlib.file_digest(open('/opt/tabstudio/probe-rs/probe-rs','rb'),'sha256').hexdigest())"],
                Environment = new Dictionary<string, string?> { [EmbeddedGlibcRuntime.RoutingMarker] = "1" },
            }, null, cancellationToken);
            var digest = result.Output.Trim();
            if (!result.Succeeded || digest.Length != 64 || !digest.All(Uri.IsHexDigit)) throw new InvalidOperationException("The installed official binary digest could not be established.");
            return digest;
        }
    }
}
