using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TabStudio.SourceControl.GitHub;

/// <summary>The kind of value a <c>workflow_dispatch</c> input takes.</summary>
public enum WorkflowInputType
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary>One of a list of options.</summary>
    Choice,

    /// <summary>True or false.</summary>
    Boolean,

    /// <summary>A number.</summary>
    Number,

    /// <summary>The name of one of the repository's deployment environments.</summary>
    Environment,
}

/// <summary>An input of a workflow that can be run by hand, as its <c>workflow_dispatch</c> trigger declares it.</summary>
/// <param name="Name">The input's name, which the workflow reads as <c>inputs.name</c>.</param>
/// <param name="Description">What it is for, if the file says.</param>
/// <param name="IsRequired">Whether a value must be given.</param>
/// <param name="Default">The value used when none is given, if any.</param>
/// <param name="Type">The kind of value.</param>
/// <param name="Options">The choices of a <see cref="WorkflowInputType.Choice"/> input.</param>
public sealed record WorkflowDispatchInput(string Name, string? Description, bool IsRequired, string? Default, WorkflowInputType Type, IReadOnlyList<string> Options);

/// <summary>What a workflow file says about running it by hand.</summary>
/// <param name="Name">The workflow's <c>name:</c>, if it has one.</param>
/// <param name="CanDispatch">Whether it has a <c>workflow_dispatch</c> trigger, so it can be run by hand.</param>
/// <param name="Inputs">The inputs it asks for when run by hand, in the file's order.</param>
public sealed record WorkflowDefinition(string? Name, bool CanDispatch, IReadOnlyList<WorkflowDispatchInput> Inputs);

/// <summary>Reads a GitHub Actions workflow file's triggers and its <c>workflow_dispatch</c> inputs.</summary>
public static class WorkflowFile
{
    private const string DispatchTrigger = "workflow_dispatch";

    /// <summary>Reads a workflow file.</summary>
    /// <param name="yaml">The file's text.</param>
    /// <param name="definition">What the file says about running it by hand.</param>
    /// <param name="error">Why the file could not be read, for the user.</param>
    /// <returns><see langword="false"/> when the text is not valid YAML or not a workflow.</returns>
    public static bool TryParse(string yaml, [NotNullWhen(true)] out WorkflowDefinition? definition, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        definition = null;
        error = null;
        var stream = new YamlStream();
        try
        {
            using var reader = new StringReader(yaml);
            stream.Load(reader);
        }
        catch (YamlException ex)
        {
            error = $"The workflow file is not valid YAML (line {ex.Start.Line}): {ex.Message}";
            return false;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            error = "The workflow file has no triggers.";
            return false;
        }

        var name = Scalar(Find(root, "name"));
        // YAML 1.1 tools read a bare "on" key as true; GitHub reads it as "on". Accept both spellings.
        var triggers = Find(root, "on") ?? Find(root, "true");
        YamlNode? dispatch = triggers switch
        {
            YamlScalarNode scalar when scalar.Value == DispatchTrigger => new YamlMappingNode(),
            YamlSequenceNode sequence when sequence.Children.Any(c => Scalar(c) == DispatchTrigger) => new YamlMappingNode(),
            YamlMappingNode mapping when mapping.Children.Any(c => Scalar(c.Key) == DispatchTrigger) => Find(mapping, DispatchTrigger) ?? new YamlMappingNode(),
            _ => null,
        };

        definition = new WorkflowDefinition(string.IsNullOrWhiteSpace(name) ? null : name, dispatch is not null, dispatch is null ? [] : ReadInputs(dispatch));
        return true;
    }

    private static List<WorkflowDispatchInput> ReadInputs(YamlNode dispatch)
    {
        var inputs = new List<WorkflowDispatchInput>();
        if (dispatch is not YamlMappingNode mapping || Find(mapping, "inputs") is not YamlMappingNode declared)
        {
            return inputs;
        }

        foreach (var (key, value) in declared.Children)
        {
            if (Scalar(key) is not { Length: > 0 } inputName)
            {
                continue;
            }

            var input = value as YamlMappingNode;
            var type = Scalar(input is null ? null : Find(input, "type")) switch
            {
                "choice" => WorkflowInputType.Choice,
                "boolean" => WorkflowInputType.Boolean,
                "number" => WorkflowInputType.Number,
                "environment" => WorkflowInputType.Environment,
                _ => WorkflowInputType.Text,
            };
            IReadOnlyList<string> options = input is not null && Find(input, "options") is YamlSequenceNode list
                ? [.. list.Children.Select(Scalar).OfType<string>()]
                : [];
            inputs.Add(new WorkflowDispatchInput(
                inputName,
                input is null ? null : NullIfEmpty(Scalar(Find(input, "description"))),
                input is not null && IsTrue(Scalar(Find(input, "required"))),
                input is null ? null : Scalar(Find(input, "default")),
                type,
                options));
        }

        return inputs;
    }

    private static YamlNode? Find(YamlMappingNode mapping, string key)
    {
        foreach (var (name, value) in mapping.Children)
        {
            if (Scalar(name) == key)
            {
                return value;
            }
        }

        return null;
    }

    private static string? Scalar(YamlNode? node) => (node as YamlScalarNode)?.Value;

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
