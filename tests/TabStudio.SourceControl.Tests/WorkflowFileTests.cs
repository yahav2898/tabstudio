using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl.Tests;

/// <summary>Reading workflow_dispatch triggers and inputs from workflow files.</summary>
public sealed class WorkflowFileTests
{
    [Theory]
    [InlineData("on: workflow_dispatch\njobs: {}\n")]
    [InlineData("on: [push, workflow_dispatch]\n")]
    [InlineData("on:\n  - push\n  - workflow_dispatch\n")]
    [InlineData("on:\n  push:\n  workflow_dispatch:\n")]
    [InlineData("\"on\":\n  workflow_dispatch: {}\n")]
    [InlineData("true:\n  workflow_dispatch:\n")]
    public void Every_way_of_writing_the_trigger_is_understood(string yaml)
    {
        Assert.True(WorkflowFile.TryParse(yaml, out var definition, out _));
        Assert.True(definition.CanDispatch);
        Assert.Empty(definition.Inputs);
    }

    [Theory]
    [InlineData("on: push\n")]
    [InlineData("on: [push, pull_request]\n")]
    [InlineData("on:\n  pull_request:\n    branches: [main]\n")]
    [InlineData("name: Nightly\non:\n  schedule:\n    - cron: '0 3 * * *'\n")]
    public void A_workflow_without_the_trigger_cannot_be_run_by_hand(string yaml)
    {
        Assert.True(WorkflowFile.TryParse(yaml, out var definition, out _));
        Assert.False(definition.CanDispatch);
    }

    [Fact]
    public void Inputs_of_every_type_are_read_in_order()
    {
        const string Yaml = """
            name: Release
            on:
              workflow_dispatch:
                inputs:
                  version:
                    description: "The version to publish, such as 1.2.0"
                    required: true
                  channel:
                    type: choice
                    options:
                      - stable
                      - beta
                    default: beta
                  draft:
                    type: boolean
                    default: true
                  retries:
                    type: number
                    default: 3
                  target:
                    type: environment
                  notes:
            """;

        Assert.True(WorkflowFile.TryParse(Yaml, out var definition, out _));

        Assert.Equal("Release", definition.Name);
        Assert.Equal(new[] { "version", "channel", "draft", "retries", "target", "notes" }, definition.Inputs.Select(i => i.Name));
        var version = definition.Inputs[0];
        Assert.Equal(("The version to publish, such as 1.2.0", true, WorkflowInputType.Text, (string?)null), (version.Description, version.IsRequired, version.Type, version.Default));
        Assert.Equal(WorkflowInputType.Choice, definition.Inputs[1].Type);
        Assert.Equal(new[] { "stable", "beta" }, definition.Inputs[1].Options);
        Assert.Equal("beta", definition.Inputs[1].Default);
        Assert.Equal((WorkflowInputType.Boolean, "true"), (definition.Inputs[2].Type, definition.Inputs[2].Default));
        Assert.Equal((WorkflowInputType.Number, "3"), (definition.Inputs[3].Type, definition.Inputs[3].Default));
        Assert.Equal(WorkflowInputType.Environment, definition.Inputs[4].Type);
        Assert.Equal((WorkflowInputType.Text, false), (definition.Inputs[5].Type, definition.Inputs[5].IsRequired));
    }

    [Fact]
    public void Invalid_YAML_is_reported_with_its_line()
    {
        Assert.False(WorkflowFile.TryParse("on:\n  workflow_dispatch:\n    inputs: [\n", out _, out var error));
        Assert.Contains("not valid YAML", error, StringComparison.Ordinal);
        Assert.Contains("line", error, StringComparison.Ordinal);
    }
}

/// <summary>Reading compiler messages from CI annotations and log lines.</summary>
public sealed class CompilerMessageTests
{
    [Fact]
    public void An_MSBuild_line_gives_its_file_position_code_and_message()
    {
        var message = CompilerMessage.Parse("src/App/Program.cs(12,34): error CS0103: The name 'x' does not exist in the current context [/home/runner/work/app/src/App/App.csproj]");

        Assert.Equal(("CS0103", "The name 'x' does not exist in the current context", "error"), (message.Code, message.Message, message.Severity));
        Assert.Equal("src/App/Program.cs", message.File);
        Assert.Equal(12, message.Line);
        Assert.Equal(34, message.Column);
    }

    [Theory]
    [InlineData("error CS0103: The name 'x' does not exist", "CS0103", "The name 'x' does not exist")]
    [InlineData("warning CA1859: Change type of variable", "CA1859", "Change type of variable")]
    [InlineData("CS1002: ; expected", "CS1002", "; expected")]
    [InlineData("Program.cs(3): warning NU1903: Package has a known vulnerability", "NU1903", "Package has a known vulnerability")]
    [InlineData("src/main.c:4:9: error: 'y' undeclared [-Werror=implicit-function-declaration]", "-Werror=implicit-function-declaration", "'y' undeclared")]
    [InlineData("Use the collection directly. (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1826)", "CA1826", "Use the collection directly. (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1826)")]
    [InlineData("Process completed with exit code 1.", null, "Process completed with exit code 1.")]
    public void Codes_are_found_in_each_format(string text, string? code, string expected)
    {
        var message = CompilerMessage.Parse(text);

        Assert.Equal(code, message.Code);
        Assert.Equal(expected, message.Message);
        Assert.Equal(text, message.Original);
    }

    [Fact]
    public void An_annotation_prefers_a_compiler_line_in_its_details_and_a_code_in_its_title()
    {
        var fromDetails = CompilerMessage.FromAnnotation(null, "Build failed", "src/A.cs(1,2): error CS0029: Cannot convert type");
        Assert.Equal(("CS0029", "Cannot convert type", "Build failed"), (fromDetails.Code, fromDetails.Message, fromDetails.Original));

        var fromTitle = CompilerMessage.FromAnnotation("CS8618", "Non-nullable field must contain a value", null);
        Assert.Equal(("CS8618", "Non-nullable field must contain a value"), (fromTitle.Code, fromTitle.Message));
    }
}
