using TabStudio.Agent.Anthropic;
using TabStudio.Testing;

namespace TabStudio.Agent.Tests;

/// <summary>A request can carry pictures, such as the screen a layout problem shows on, which Claude sees before the text.</summary>
public sealed class AgentSessionImageTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Pictures_go_to_Claude_before_the_request_s_text()
    {
        var claude = new FakeClaude().Text("The button is too small; I made it 44 high.");
        var session = new AgentSession(new AnthropicClient(new HttpClient(claude), "sk-test"), new FolderWorkspace(_temp));
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

        var result = await session.SendAsync("Fix the touch target", [new ImageBlock("image/png", png)]);

        Assert.Equal(AgentTurnEnd.Completed, result.End);
        var content = claude.Requests.Single()["messages"]!.AsArray()[0]!["content"]!.AsArray();
        Assert.Equal(2, content.Count);
        Assert.Equal("image", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("image/png", content[0]!["source"]!["media_type"]!.GetValue<string>());
        Assert.Equal(Convert.ToBase64String(png), content[0]!["source"]!["data"]!.GetValue<string>());
        Assert.Equal("Fix the touch target", content[1]!["text"]!.GetValue<string>());
        Assert.IsType<ImageBlock>(session.History[0].Content[0]);
    }

    public void Dispose() => _temp.Dispose();
}
