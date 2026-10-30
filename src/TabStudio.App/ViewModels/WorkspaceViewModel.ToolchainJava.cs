using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.JsonRpc;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private async Task<(RunPlan Plan, OutputProcessResult Result)> BuildJavaOrToolchainAsync(ToolchainProject project, RunPlan plan, CancellationToken cancellationToken)
    {
        foreach (var tool in project.Toolchain.ProjectTools.Where(t => t.Command == plan.Build!.Program && File.Exists(Path.Combine(project.Directory, t.Manifest))).Take(1))
        {
            if (_toolchains.Runtime.Resolve(tool.Command) is not null)
            {
                continue;
            }

            if (!_toolchains.Manager.CanInstall)
            {
                var message = $"This project needs {tool.Command}. Install it on this computer so it is on PATH.";
                Output.WriteLine(message, OutputKind.Failure);
                return (plan, new(new ProcessExit(1, null, false), [message]));
            }

            Output.WriteLine($"Installing {tool.Command} for {tool.Manifest}...", OutputKind.Info);
            var install = await OutputProcess.RunAsync(_toolchains.Runtime, new ProcessStartSpec
            {
                Program = "apk", Arguments = ["add", "--no-cache", .. tool.Packages], WorkingDirectory = project.Directory,
            }, Output, null, cancellationToken);
            if (!install.Succeeded)
            {
                return (plan, install);
            }
        }

        var built = await OutputProcess.RunAsync(_toolchains.Runtime, plan.Build!, Output, null, cancellationToken, toolchain: project.Toolchain.Id);
        if (built.Succeeded && project.Toolchain.Id is "java" or "kotlin" && plan.EntryPoint == "build.gradle")
        {
            var classpath = Path.Combine(project.Directory, ".tabstudio", "java-classpath.txt");
            if (File.Exists(classpath))
            {
                plan = plan with { Run = plan.Run with { Arguments = ["-cp", File.ReadAllText(classpath).Trim(), .. plan.Run.Arguments.Skip(2)] } };
            }
        }

        return (plan, built);
    }

    private async Task<LaunchConfiguration?> PrepareJavaDebugAsync(ToolchainProject project, LaunchConfiguration configuration)
    {
        var source = JavaProject.MainSource(project.Directory) ?? ActiveDocument?.FilePath;
        if (source is null || await OpenDocumentAsync(source) is not { LanguageServer: { } server })
        {
            State = IdeState.Failed;
            StatusText = "Java debugging needs Java from Tools > Toolchain Store";
            return null;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var port = await server.ExecuteServerCommandAsync("vscode.java.startDebugSession", [], timeout.Token);
            if (!port.TryGetInt32(out var number) || number is < 1 or > 65535)
            {
                throw new InvalidOperationException("JDT did not return a Java debug adapter port.");
            }

            var properties = (JsonObject)configuration.Properties.DeepClone();
            properties["debugServer"] = number;
            return configuration with { Properties = properties };
        }
        catch (Exception ex) when (ex is JsonRpcException or InvalidOperationException or OperationCanceledException)
        {
            var detail = server.ServerMessage is { Length: > 0 } message ? $" {server.ServerName}: {message}" : string.Empty;
            // ex.Message ends with its own period ("The connection closed.."); the next step comes before the details.
            Output.WriteLine($"Java debugging could not start: {ex.Message.TrimEnd('.')}. If it keeps happening, update Java in Tools > Toolchain Store and press F5 again.{detail} Language Servers output has the details.", OutputKind.Failure);
            State = IdeState.Failed;
            StatusText = "Java debugger did not start";
            return null;
        }
    }
}
