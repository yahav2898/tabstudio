using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.LanguageServers.DebugAdapters.Protocol;

/// <summary>
/// Source-generated JSON for the Debug Adapter Protocol, so nothing depends on reflection and the client works with
/// trimming on Android. Properties are camelCase, and properties without a value are left out.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(InitializeArgumentsDto))]
[JsonSerializable(typeof(CapabilitiesDto))]
[JsonSerializable(typeof(SetBreakpointsArgumentsDto))]
[JsonSerializable(typeof(SetBreakpointsBodyDto))]
[JsonSerializable(typeof(SetExceptionBreakpointsArgumentsDto))]
[JsonSerializable(typeof(ThreadArgumentsDto))]
[JsonSerializable(typeof(ThreadsBodyDto))]
[JsonSerializable(typeof(StackTraceArgumentsDto))]
[JsonSerializable(typeof(StackTraceBodyDto))]
[JsonSerializable(typeof(ScopesArgumentsDto))]
[JsonSerializable(typeof(ScopesBodyDto))]
[JsonSerializable(typeof(VariablesArgumentsDto))]
[JsonSerializable(typeof(VariablesBodyDto))]
[JsonSerializable(typeof(EvaluateArgumentsDto))]
[JsonSerializable(typeof(EvaluateBodyDto))]
[JsonSerializable(typeof(SetVariableArgumentsDto))]
[JsonSerializable(typeof(SetExpressionArgumentsDto))]
[JsonSerializable(typeof(SetValueBodyDto))]
[JsonSerializable(typeof(ExceptionInfoBodyDto))]
[JsonSerializable(typeof(DisconnectArgumentsDto))]
[JsonSerializable(typeof(TerminateArgumentsDto))]
[JsonSerializable(typeof(StoppedEventDto))]
[JsonSerializable(typeof(ContinuedEventDto))]
[JsonSerializable(typeof(ExitedEventDto))]
[JsonSerializable(typeof(OutputEventDto))]
[JsonSerializable(typeof(ThreadEventDto))]
[JsonSerializable(typeof(BreakpointEventDto))]
[JsonSerializable(typeof(ProcessEventDto))]
[JsonSerializable(typeof(StartDebuggingArgumentsDto))]
internal sealed partial class DapJsonContext : JsonSerializerContext
{
}
