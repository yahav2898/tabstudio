using System.Globalization;
using System.Text;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>Creates non-destructive raw flash reads. It never unlocks a device or changes its readout protection.</summary>
public static class EmbeddedReadbackPlanner
{
    /// <summary>Reads one explicit application interval to a BIN file using a supported upstream tool.</summary>
    public static ProcessStartSpec Plan(EmbeddedFlashRequest request, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ApplicationFlash.Count != 1)
        {
            throw new InvalidOperationException("Select exactly one application-flash interval for a raw backup.");
        }
        FlashSafety.ValidateFirmware(request.ApplicationFlash, request.ApplicationFlash);
        var range = request.ApplicationFlash[0];
        if (range.Length > 128 * 1024 * 1024)
        {
            throw new InvalidOperationException("A flash-backup interval cannot exceed 128 MiB.");
        }
        if (request.BaudRate is < 300 or > 4_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Baud rate must be 300..4000000.");
        }
        var output = Path.GetFullPath(outputPath);
        var start = "0x" + range.Start.ToString("X", CultureInfo.InvariantCulture);
        var length = range.Length.ToString(CultureInfo.InvariantCulture);
        var args = new List<string>();
        string program;
        switch (request.Protocol)
        {
            case EmbeddedFlashProtocol.ProbeRs:
                if (string.IsNullOrWhiteSpace(request.Target) || !request.Target.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')
                    || string.IsNullOrWhiteSpace(request.ProbeSelector) || request.ProbeSelector.Any(char.IsControl))
                {
                    throw new ArgumentException("Select an exact target and one permissioned probe before backing up flash.", nameof(request));
                }
                program = "probe-rs";
                args.AddRange(["read", "b8", start, length, "--output", output, "--format", "binary",
                    "--chip", request.Target, "--probe", request.ProbeSelector, "--non-interactive"]);
                break;
            case EmbeddedFlashProtocol.OpenOcd:
                ValidateConfig(request.InterfaceConfig);
                ValidateConfig(request.TargetConfig);
                program = "openocd";
                args.AddRange(["-f", request.InterfaceConfig!, "-f", request.TargetConfig!]);
                if (request.ProbeSelector is { } serial)
                {
                    args.AddRange(["-c", "adapter serial " + TclWord(serial)]);
                }
                args.AddRange(["-c", "init; halt; dump_image " + TclWord(output) + " " + start + " " + length + "; shutdown"]);
                break;
            case EmbeddedFlashProtocol.Stm32Flash:
                ValidateSerial(request.SerialPort);
                program = "stm32flash";
                args.AddRange(["-b", request.BaudRate.ToString(CultureInfo.InvariantCulture), "-S", start + ":" + length, "-r", output, request.SerialPort!]);
                break;
            case EmbeddedFlashProtocol.Stm32Dfu:
                if (request.UsbSelector is not { Length: 9 } id || id[4] != ':' || !id.Where((_, index) => index != 4).All(Uri.IsHexDigit) || request.DfuAlternate < 0)
                {
                    throw new ArgumentException("An exact DFU VID:PID and alternate are required.", nameof(request));
                }
                program = "dfu-util";
                args.AddRange(["-d", id, "-a", request.DfuAlternate.ToString(CultureInfo.InvariantCulture), "-s", start + ":" + length, "-U", output]);
                break;
            default:
                throw new NotSupportedException("Raw application backup is implemented for probe-rs, OpenOCD, stm32flash and readback-capable STM32 DFU. Protection is never disabled.");
        }
        return new()
        {
            Program = program, Arguments = args, WorkingDirectory = Path.GetFullPath(request.WorkingDirectory),
            Environment = program == "probe-rs" ? ProbeTargetInfoReader.SafeAttachEnvironment() : new Dictionary<string, string?>(),
        };
    }

    private static void ValidateConfig(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '-' or '.')) || path.Split('/').Contains("..", StringComparer.Ordinal))
        {
            throw new ArgumentException("Select the installed OpenOCD configuration for the actual probe/target.", nameof(path));
        }
    }

    private static void ValidateSerial(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || path.Any(char.IsControl))
        {
            throw new ArgumentException("An absolute serial bridge path is required.", nameof(path));
        }
    }

    private static string TclWord(string value)
    {
        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("Control characters are forbidden in tool arguments.", nameof(value));
        }
        var result = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c is '\\' or '"' or '$' or '[' or ']')
            {
                result.Append('\\');
            }
            result.Append(c);
        }
        return result.Append('"').ToString();
    }
}
