using System.Diagnostics;
using System.Security.Cryptography;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>The outcome of an application upload and its verification.</summary>
/// <param name="FirmwareSha256">Digest of the file used by every upload step.</param>
/// <param name="Elapsed">Total elapsed time.</param>
/// <param name="StepsCompleted">Number of successful upstream tool invocations.</param>
/// <param name="Verified">Target verification completed; false means transport completion only.</param>
public sealed record EmbeddedFlashResult(string FirmwareSha256, TimeSpan Elapsed, int StepsCompleted, bool Verified = true);

/// <summary>Executes validated application upload plans with cancellation, streaming logs and content-stability checks.</summary>
public static class EmbeddedFlashExecutor
{
    /// <summary>Runs a plan. The caller owns and retains the permissioned USB connection until this task completes.</summary>
    /// <param name="runtime">Existing Toolchain Store runtime.</param>
    /// <param name="plan">Validated upload plan.</param>
    /// <param name="bridge">Optional wrapper that passes exactly one device's FD to each child.</param>
    /// <param name="output">Receives tool output; reporters should marshal to the UI thread if necessary.</param>
    /// <param name="prepareBootloader">Performs a required 1200-baud touch and waits for the selected board's re-enumeration.</param>
    /// <param name="cancellationToken">Stops the active tool and prevents further steps.</param>
    public static async Task<EmbeddedFlashResult> ExecuteAsync(IToolchainRuntime runtime, EmbeddedFlashPlan plan,
        Func<ProcessStartSpec, ProcessStartSpec>? bridge = null, IProgress<string>? output = null,
        Func<CancellationToken, Task>? prepareBootloader = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Steps.Count == 0 || plan.FirmwarePath is null)
        {
            throw new ArgumentException("A complete firmware plan is required.", nameof(plan));
        }
        foreach (var step in plan.Steps)
        {
            if (!runtime.HasCommand(step.Program))
            {
                throw new InvalidOperationException($"Install {step.Program} from the Toolchain Store before flashing.");
            }
        }
        var digest = await DigestAsync(plan.FirmwarePath, cancellationToken).ConfigureAwait(false);
        if (plan.FirmwareSha256 is { } expected && !string.Equals(expected, digest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Firmware changed after the safe upload plan was created. Build and validate it again.");
        }
        async Task ValidateInputsAsync()
        {
            foreach (var input in plan.FirmwareInputs)
            {
                if (await DigestAsync(input.Path, cancellationToken).ConfigureAwait(false) != input.Sha256)
                {
                    throw new InvalidDataException("An ESP image or build/security metadata changed after validation: " + input.Path + ". Further operations were stopped.");
                }
            }
        }
        await ValidateInputsAsync().ConfigureAwait(false);
        foreach (var input in plan.FirmwareInputs)
        {
            output?.Report("Validated input SHA-256: " + input.Sha256 + " " + input.Path);
        }
        if (plan.Touch1200Baud)
        {
            if (prepareBootloader is null)
            {
                throw new InvalidOperationException("This board needs a 1200-baud touch and bootloader device re-selection before upload.");
            }
            await prepareBootloader(cancellationToken).ConfigureAwait(false);
        }
        if (plan.ReadbackPath is { } readback)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(readback)!);
        }
        var elapsed = Stopwatch.StartNew();
        var completed = 0;
        try
        {
            foreach (var step in plan.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ValidateInputsAsync().ConfigureAwait(false);
                if (await DigestAsync(plan.FirmwarePath, cancellationToken).ConfigureAwait(false) != digest)
                {
                    throw new InvalidDataException("Firmware changed during upload. Further operations were stopped.");
                }
                var spec = bridge?.Invoke(step) ?? step;
                output?.Report(spec.CommandLine);
                using var process = runtime.Start(spec);
                var stdout = PumpAsync(process.Output, output, cancellationToken);
                var stderr = process.Error is { } error ? PumpAsync(error, output, cancellationToken) : Task.CompletedTask;
                var exit = await process.WaitAsync(cancellationToken).ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (exit.ExitCode != 0 || exit.WasStopped)
                {
                    throw new IOException($"{step.Program} failed with exit code {exit.ExitCode}; no further steps were run.");
                }
                completed++;
            }
            if (plan.ReadbackPath is { } verify)
            {
                var result = await FirmwareBackup.CompareAsync(plan.FirmwarePath, verify, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!result.Identical)
                {
                    throw new InvalidDataException("Programmed flash readback differs from the firmware. The target was not reset or started.");
                }
            }
            if (await DigestAsync(plan.FirmwarePath, cancellationToken).ConfigureAwait(false) != digest)
            {
                throw new InvalidDataException("Firmware changed while an upload tool was running; success cannot be attributed to the validated input.");
            }
            await ValidateInputsAsync().ConfigureAwait(false);
            if (plan.FirmwareInputs.Count > 0)
            {
                digest = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', plan.FirmwareInputs.Select(input => input.Sha256)))));
            }
            return new(digest, elapsed.Elapsed, completed, plan.Verified);
        }
        finally
        {
            if (plan.ReadbackPath is { } temporary)
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task<string> DigestAsync(string path, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false));
    }

    private static async Task PumpAsync(Stream stream, IProgress<string>? output, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false) is var read && read > 0)
        {
            output?.Report(new string(buffer, 0, read));
        }
    }
}
