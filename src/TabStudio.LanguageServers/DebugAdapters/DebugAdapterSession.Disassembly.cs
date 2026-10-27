using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.DebugAdapters;

public sealed partial class DebugAdapterSession
{
    /// <summary>Requests a bounded static disassembly using the actual DAP request. Callers must keep the target halted for the whole operation.</summary>
    /// <param name="address">Unsigned absolute start address.</param>
    /// <param name="instructionCount">Number of instruction rows, from 1 to 256.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<DapDisassembledInstruction>> DisassembleAsync(ulong address, int instructionCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(instructionCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instructionCount, 256);
        if (!Capabilities.Disassemble) throw new DebugAdapterException("This adapter does not advertise DAP disassemble. Use an embedded GDB/probe-rs adapter build with disassembly support.");
        var body = await _connection.SendRequestAsync("disassemble", new JsonObject
        {
            ["memoryReference"] = "0x" + address.ToString("X", CultureInfo.InvariantCulture),
            ["instructionCount"] = instructionCount, ["resolveSymbols"] = true,
        }, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!body.TryGetProperty("instructions", out var instructions) || instructions.ValueKind != JsonValueKind.Array || instructions.GetArrayLength() != instructionCount)
                throw new DebugAdapterException("The adapter did not return the requested number of instruction rows; partial disassembly is not shown.");
            var result = new List<DapDisassembledInstruction>(instructionCount);
            string? source = null;
            ulong? previous = null;
            foreach (var item in instructions.EnumerateArray())
            {
                var textAddress = RequiredInstructionText(item, "address", 128);
                var hexadecimal = textAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                if (!ulong.TryParse(hexadecimal ? textAddress.AsSpan(2) : textAddress.AsSpan(),
                    hexadecimal ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                {
                    throw new DebugAdapterException("The adapter returned an instruction with a non-numeric or overflowing address.");
                }
                var hint = OptionalInstructionText(item, "presentationHint", 32);
                if (hint is not (null or "normal" or "invalid")) throw new DebugAdapterException("Unknown disassembly presentation hint.");
                var invalid = hint == "invalid";
                if (!invalid)
                {
                    if (previous is { } earlier && value <= earlier) throw new DebugAdapterException("Valid instruction addresses are not strictly increasing.");
                    previous = value;
                }
                if (item.TryGetProperty("location", out var location))
                {
                    source = location.ValueKind == JsonValueKind.Null ? null : OptionalInstructionText(location, "path", 4096);
                    if (source is not null && (source.Any(char.IsControl) || source.Contains("://", StringComparison.Ordinal)
                        || source.StartsWith("\\\\", StringComparison.Ordinal) || source.StartsWith("//", StringComparison.Ordinal)))
                    {
                        throw new DebugAdapterException("The adapter did not provide a valid native source-file path.");
                    }
                }
                var line = InstructionPosition(item, "line");
                var column = InstructionPosition(item, "column");
                result.Add(new(value, RequiredInstructionText(item, "instruction", 8192),
                    OptionalInstructionText(item, "instructionBytes", 1024), OptionalInstructionText(item, "symbol", 8192),
                    source, line, column, invalid));
            }
            return result;
        }
        catch (Exception error) when (error is InvalidOperationException or FormatException or OverflowException)
        {
            throw new DebugAdapterException("The adapter returned malformed disassembly/source data.", error);
        }
    }

    private static string RequiredInstructionText(JsonElement item, string name, int maximum) =>
        OptionalInstructionText(item, name, maximum) ?? throw new DebugAdapterException("Disassembly response is missing " + name + ".");

    private static string? OptionalInstructionText(JsonElement item, string name, int maximum)
    {
        if (!item.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return null;
        if (property.ValueKind != JsonValueKind.String || property.GetString() is not { } text || text.Length > maximum ||
            text.Any(character => char.IsControl(character) && character != '\t'))
        {
            throw new DebugAdapterException("Disassembly contains invalid or oversized " + name + ".");
        }
        return text;
    }

    private static int? InstructionPosition(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return null;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var position) || position < 1)
            throw new DebugAdapterException("Disassembly source locations must use positive bounded one-based positions.");
        return position;
    }
}
