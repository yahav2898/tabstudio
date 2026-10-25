using TabStudio.Agent.Anthropic;

namespace TabStudio.Agent.Tests;

public sealed class SketchToXamlTests
{
    [Fact]
    public async Task Sends_the_sketch_as_an_image_and_returns_the_xaml_and_notes()
    {
        var claude = new FakeClaude().Text("```xml\n<StackPanel>\n  <TextBox x:Name=\"Email\" />\n</StackPanel>\n```\nI read the label as Email.");
        var sketch = new SketchToXaml(new AnthropicClient(new HttpClient(claude), "sk-test"), "claude-test");

        var result = await sketch.ConvertAsync(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, XamlDialect.Avalonia, "a login form");

        Assert.Equal("<StackPanel>\n  <TextBox x:Name=\"Email\" />\n</StackPanel>", result.Xaml);
        Assert.Equal("I read the label as Email.", result.Notes);
        var content = claude.Requests.Single()["messages"]![0]!["content"]!.AsArray();
        Assert.Equal("image", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("image/png", content[0]!["source"]!["media_type"]!.GetValue<string>());
        Assert.Equal("iVBORw==", content[0]!["source"]!["data"]!.GetValue<string>());
        Assert.Contains("Avalonia", content[1]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("a login form", content[1]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<Grid />", "<Grid />", "")]
    [InlineData("Here it is:\n```xaml\n<Grid/>\n```", "<Grid/>", "Here it is:")]
    public void Answers_are_parsed(string answer, string xaml, string notes)
    {
        var result = SketchToXaml.Parse(answer);

        Assert.Equal(xaml, result.Xaml);
        Assert.Equal(notes, result.Notes);
    }

    [Fact]
    public void An_answer_without_xaml_is_an_error() =>
        Assert.Throws<AnthropicException>(() => SketchToXaml.Parse("I cannot read this sketch."));
}
