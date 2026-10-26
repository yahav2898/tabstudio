using System.Text.Json;
using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Hosting;

public sealed partial class LanguageServerDocument
{
    /// <summary>Runs a server extension command, such as JDT's command that starts its Java DAP server.</summary>
    /// <param name="command">The server's command identifier.</param>
    /// <param name="arguments">Its arguments, or an empty array.</param>
    /// <param name="cancellationToken">Cancels initialization or the command.</param>
    /// <returns>The command's result.</returns>
    /// <exception cref="InvalidOperationException">The language server is not available.</exception>
    public async Task<JsonElement> ExecuteServerCommandAsync(string command, JsonArray arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(arguments);
        var connection = await _server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The language server is not available.");
        return await connection.SendRequestAsync("workspace/executeCommand", new JsonObject
        {
            ["command"] = command, ["arguments"] = arguments.DeepClone(),
        }, cancellationToken).ConfigureAwait(false);
    }
}
