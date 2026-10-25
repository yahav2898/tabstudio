using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.LanguageServers.Tests.Hosting;

/// <summary>The initialize payload accepted by strict language servers such as gopls.</summary>
public sealed class ClientCapabilitiesTests
{
    [Fact]
    public void Completion_item_kinds_are_an_array_of_protocol_numbers()
    {
        var capabilities = ClientCapabilities.Create();
        var values = Assert.IsType<JsonArray>(capabilities["textDocument"]!["completion"]!["completionItemKind"]!["valueSet"]);
        Assert.Equal(Enumerable.Range(1, 25), values.Select(v => v!.GetValue<int>()));
    }
}
