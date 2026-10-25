using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TabStudio.Compilation;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Link.Language;

/// <summary>
/// The JSON contract of the language service's messages: source-generated, so nothing depends on reflection, and
/// without the computed properties of the Compilation records (such as <see cref="BuildDiagnostic.FileName"/>),
/// which are worked out again on arrival.
/// </summary>
internal static class LinkLanguageJson
{
    /// <summary>The options every language message is written and read with.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>The contract of a type.</summary>
    public static JsonTypeInfo<T> Info<T>() => (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

    /// <summary>A value as a message body.</summary>
    public static JsonElement ToJson<T>(T value) => JsonSerializer.SerializeToElement(value, Info<T>());

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(LinkLanguageJsonContext.Default.Options)
        {
            TypeInfoResolver = LinkLanguageJsonContext.Default.WithAddedModifier(SkipComputedProperties),
        };
        options.MakeReadOnly();
        return options;
    }

    /// <summary>Leaves out properties that are neither set nor passed to a constructor: they are computed.</summary>
    private static void SkipComputedProperties(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var i = info.Properties.Count - 1; i >= 0; i--)
        {
            var property = info.Properties[i];
            if (property.Set is null && property.AssociatedParameter is null)
            {
                info.Properties.RemoveAt(i);
            }
        }
    }
}

/// <summary>Source-generated JSON for every language message.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(PrepareWorkspaceRequest))]
[JsonSerializable(typeof(PrepareWorkspaceResponse))]
[JsonSerializable(typeof(UploadFilesRequest))]
[JsonSerializable(typeof(LoadWorkspaceRequest))]
[JsonSerializable(typeof(LoadWorkspaceResponse))]
[JsonSerializable(typeof(TextChange))]
[JsonSerializable(typeof(FileRequest))]
[JsonSerializable(typeof(ProjectRequest))]
[JsonSerializable(typeof(PositionRequest))]
[JsonSerializable(typeof(CompletionListRequest))]
[JsonSerializable(typeof(CompletionChangeRequest))]
[JsonSerializable(typeof(CompletionDescriptionRequest))]
[JsonSerializable(typeof(RenameRequest))]
[JsonSerializable(typeof(CodeActionsRequest))]
[JsonSerializable(typeof(CodeActionEditsRequest))]
[JsonSerializable(typeof(OptimizationSetting))]
[JsonSerializable(typeof(ActiveProjectsSetting))]
[JsonSerializable(typeof(BuildResponse))]
[JsonSerializable(typeof(DebugBuildResponse))]
[JsonSerializable(typeof(LinkRestoreOutcome))]
[JsonSerializable(typeof(LinkHostStatus))]
[JsonSerializable(typeof(IReadOnlyList<BuildDiagnostic>))]
[JsonSerializable(typeof(IReadOnlyList<ClassifiedRange>))]
[JsonSerializable(typeof(IReadOnlyList<TextEdit>))]
[JsonSerializable(typeof(CompletionResult))]
[JsonSerializable(typeof(CompletionCommit))]
[JsonSerializable(typeof(IReadOnlyList<TaggedRun>))]
[JsonSerializable(typeof(Optional<QuickInfoResult>))]
[JsonSerializable(typeof(Optional<SignatureHelpResult>))]
[JsonSerializable(typeof(Optional<DefinitionResult>))]
[JsonSerializable(typeof(IReadOnlyList<DefinitionResult>))]
[JsonSerializable(typeof(IReadOnlyList<ReferenceItem>))]
[JsonSerializable(typeof(IReadOnlyList<DocumentHighlight>))]
[JsonSerializable(typeof(RenameTarget))]
[JsonSerializable(typeof(RenameResult))]
[JsonSerializable(typeof(IReadOnlyList<CodeActionItem>))]
[JsonSerializable(typeof(IReadOnlyList<DocumentEdit>))]
[JsonSerializable(typeof(IReadOnlyList<DocumentSymbol>))]
internal sealed partial class LinkLanguageJsonContext : JsonSerializerContext
{
}
