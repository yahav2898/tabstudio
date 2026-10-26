using TabStudio.SourceControl.GitHub;
using YamlDotNet.RepresentationModel;

namespace TabStudio.SourceControl.Tests;

public sealed class IosWorkflowGeneratorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Generates_valid_dispatchable_YAML_with_signing_cleanup_and_an_IPA_artifact(bool capacitor)
    {
        var yaml = IosWorkflowGenerator.Generate(new IosWorkflowOptions(capacitor));
        Assert.True(WorkflowFile.TryParse(yaml, out var definition, out var error), error);
        Assert.True(definition!.CanDispatch);
        Assert.Equal(WorkflowInputType.Boolean, Assert.Single(definition.Inputs).Type);
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var jobs = (YamlMappingNode)root.Children[new YamlScalarNode("jobs")];
        var job = (YamlMappingNode)jobs.Children[new YamlScalarNode("ios")];
        Assert.Equal("macos-15", ((YamlScalarNode)job.Children[new YamlScalarNode("runs-on")]).Value);
        var steps = (YamlSequenceNode)job.Children[new YamlScalarNode("steps")];
        Assert.Contains(steps.Children.OfType<YamlMappingNode>(), s => s.Children.TryGetValue(new YamlScalarNode("uses"), out var value) && ((YamlScalarNode)value).Value == "actions/upload-artifact@v4");
        Assert.Contains("if: always()", yaml, StringComparison.Ordinal);
        Assert.Contains("security delete-keychain", yaml, StringComparison.Ordinal);
        Assert.Equal(capacitor, yaml.Contains("npx --no-install cap sync ios", StringComparison.Ordinal));
        foreach (var secret in IosWorkflowGenerator.Secrets)
        {
            Assert.Contains("${{ secrets." + secret.Name + " }}", yaml, StringComparison.Ordinal);
            Assert.Contains(secret.Name, IosWorkflowGenerator.SetupGuide(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/tmp/project")]
    [InlineData("C:\\project")]
    [InlineData("app\nrun: evil")]
    public void Refuses_paths_outside_the_repository(string directory) => Assert.Throws<ArgumentException>(() => IosWorkflowGenerator.Generate(new IosWorkflowOptions(true, directory)));

    [Fact]
    public void Project_names_are_environment_values_instead_of_executable_shell_source()
    {
        var yaml = IosWorkflowGenerator.Generate(new IosWorkflowOptions(false, "apps/a';echo something;#", "My App.xcodeproj", "$(echo example)"));
        Assert.Contains("cd \"$PROJECT_DIRECTORY\"", yaml, StringComparison.Ordinal);
        Assert.Contains("-scheme \"$XCODE_SCHEME\"", yaml, StringComparison.Ordinal);
        Assert.Contains("XCODE_SCHEME: \"$(echo example)\"", yaml, StringComparison.Ordinal);
    }
}
