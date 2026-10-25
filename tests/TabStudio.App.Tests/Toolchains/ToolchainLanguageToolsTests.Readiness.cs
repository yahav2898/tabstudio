using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainLanguageToolsTests
{
    private static async Task<LspCompletionList> AwaitCompletionsAsync(LanguageServerDocument server, int offset, Func<LspCompletionItem, bool> contains, CancellationToken cancellationToken)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < end)
        {
            var result = await server.GetCompletionsAsync(offset, LspCompletionTriggerKind.Invoked, null, cancellationToken);
            if (result is not null && result.Items.Any(contains))
            {
                return result;
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new Xunit.Sdk.XunitException("The language server did not finish indexing the template for completion.");
    }
}
