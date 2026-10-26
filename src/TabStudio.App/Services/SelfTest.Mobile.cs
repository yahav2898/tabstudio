using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>On the emulator, an actual Node HTTP server offers its ephemeral port and opens in the preview.</summary>
    private static async Task<SelfTestResult> CheckListeningPortAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var folder = templates.Create(ProjectTemplates.NodeId, "ListeningPortSelfTest");
        await File.WriteAllTextAsync(Path.Combine(folder, "index.js"), """
            import http from 'node:http';
            const server = http.createServer((req, res) => res.end('TabStudio listening port self-test'));
            server.listen(0, '127.0.0.1', () => console.log(`App listening on port ${server.address().port}`));
            """, cancellationToken);
        await main.OpenWorkspaceAsync(folder);
        var workspace = main.Workspace!;
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            while (workspace.ServerNotice is null && !run.IsCompleted)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (workspace.ServerNotice is not { } notice)
            {
                return new SelfTestResult(false, "Listening port: no notice; " + workspace.Output.Text, clock.Elapsed);
            }

            notice.OpenPreview.Execute(null);
            using var http = new HttpClient();
            var text = await http.GetStringAsync(workspace.WebPreview.AppAddress, cancellationToken);
            var success = workspace.WebPreview.IsRunning && text == "TabStudio listening port self-test";
            return new SelfTestResult(success, $"Listening port: {notice.Port}, preview {(success ? "passed" : "failed")}", clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }
    }
}
