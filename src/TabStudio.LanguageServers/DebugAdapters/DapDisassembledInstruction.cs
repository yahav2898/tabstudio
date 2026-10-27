namespace TabStudio.LanguageServers.DebugAdapters;

/// <summary>One instruction returned by DAP disassemble, with validated numeric address and optional native source location.</summary>
/// <param name="Address">Absolute instruction address.</param>
/// <param name="Instruction">Adapter's assembly text.</param>
/// <param name="Bytes">Optional raw instruction bytes in the adapter's display format.</param>
/// <param name="Symbol">Optional resolved symbol.</param>
/// <param name="SourcePath">Native file path; inherited when the protocol omits an unchanged source file.</param>
/// <param name="Line">One-based line, when available.</param>
/// <param name="Column">One-based column, when available.</param>
/// <param name="IsInvalid">True for protocol filler representing unavailable instructions.</param>
public sealed record DapDisassembledInstruction(ulong Address, string Instruction, string? Bytes, string? Symbol,
    string? SourcePath, int? Line, int? Column, bool IsInvalid);
