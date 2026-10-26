using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Remote;
using TabStudio.Remote.Tests;
using TabStudio.Testing;

namespace TabStudio.App.Tests.RemoteSsh;

public sealed class RemoteSshTests
{
    [AvaloniaFact]
    public async Task Remote_config_import_device_key_and_profile_validation_work_in_the_App()
    {
        using var folder = new TempDirectory();
        var services = new RemoteSshServices(new AppPaths(folder.Path));
        using var model = new RemoteExplorerViewModel(services)
        {
            ConfigText = "Host development\n HostName 192.0.2.10\n User deploy\n Port 2222",
        };
        await model.ImportConfigCommand.ExecuteAsync(null);
        Assert.Single(model.Hosts);
        Assert.Equal("development", model.SelectedHost!.Name);
        await model.GenerateKeyCommand.ExecuteAsync(null);
        Assert.StartsWith("ssh-rsa ", model.PublicKey, StringComparison.Ordinal);
        model.PreviewPort = "invalid";
        Assert.Throws<ArgumentException>(() => model.ReadRunProfile());
        Assert.False(model.IsBusy);
    }

    [AvaloniaFact]
    public async Task Remote_cache_reopened_offline_cannot_run_locally_or_mark_a_failed_save_clean()
    {
        using var fixture = new WorkspaceFixture();
        var root = fixture.Paths.Projects;
        Directory.CreateDirectory(Path.Combine(root, ".tabstudio"));
        await File.WriteAllTextAsync(Path.Combine(root, ".tabstudio", "remote-workspace.txt"), "development");
        var path = Path.Combine(root, "note.txt");
        await File.WriteAllTextAsync(path, "before");
        await fixture.Workspace.LoadAsync(root);
        var document = await fixture.Workspace.OpenDocumentAsync(path);
        document!.Document.Text = "unsaved remote edit";
        Assert.False(fixture.Workspace.RunCommand.CanExecute(null));
        Assert.False(fixture.Workspace.BuildCommand.CanExecute(null));
        await fixture.Workspace.SaveCommand.ExecuteAsync(null);
        Assert.True(document.IsDirty);
        Assert.Contains("Reconnect", fixture.Workspace.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Real_remote_folder_opens_edits_saves_builds_navigates_errors_and_uses_the_remote_terminal()
    {
        if (!SshTestServer.Available) { return; } // CI starts the required OpenSSH container in the App job.
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        using var services = AppServices.Create(paths);
        var remoteServices = services.GetRequiredService<RemoteSshServices>();
        remoteServices.Hosts.Save([SshTestServer.Host]);
        remoteServices.Hosts.Trust(SshTestServer.Host, SshTestServer.Fingerprint);
        using var server = await SshTestServer.ConnectAsync();
        var root = SshTestServer.Root + "/app-" + Guid.NewGuid().ToString("N");
        server.Sftp.CreateDirectory(root);
        server.Sftp.WriteAllText(root + "/Program.cs", "// Remote source\nConsole.WriteLine(\"before\");\n");
        var main = services.GetRequiredService<MainViewModel>();
        await main.OpenWorkspaceAsync(paths.Projects);
        var workspace = main.Workspace!;
        await workspace.OpenRemoteFolderAsync(SshTestServer.Host, root);
        Assert.True(workspace.IsRemoteWorkspace);
        Assert.Contains(workspace.Explorer.Rows, r => r.Node.Name == "Program.cs");
        var document = await workspace.OpenDocumentAsync(Path.Combine(workspace.Workspace!.RootDirectory, "Program.cs"));
        document!.Document.Text = "// Saved from tablet\nConsole.WriteLine(\"after\");\n";
        await workspace.SaveCommand.ExecuteAsync(null);
        Assert.False(document.IsDirty);
        Assert.Contains("after", server.Sftp.ReadAllText(root + "/Program.cs"), StringComparison.Ordinal);

        workspace.RemoteExplorer.BuildScript = "printf 'Program.cs(2,3): error CS1002: remote diagnostic\\n'; exit 1";
        workspace.RemoteExplorer.PreviewPort = string.Empty;
        await workspace.BuildCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("remote diagnostic", StringComparison.Ordinal));
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains(workspace.ErrorList.Items, r => r.Diagnostic.Location?.FilePath == document.FilePath);
        workspace.RemoteExplorer.BuildScript = "printf 'Built on remote host\\n'";
        workspace.RemoteExplorer.RunScript = "printf 'remote-run-42\\n'";
        await workspace.RunCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Output.Text.Contains("remote-run-42", StringComparison.Ordinal));
        workspace.ShowTerminalCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Terminal.IsRunning);
        workspace.Terminal.SendText("printf 'terminal-%s\\n' $((6*7))\r");
        await UiTestHelpers.WaitUntilAsync(() => workspace.Terminal.Emulator.ScreenText.Contains("terminal-42", StringComparison.Ordinal));
        server.Sftp.WriteAllText(root + "/Program.cs", "// other developer\n");
        document.Document.Text = "// my conflicting change\n";
        await workspace.SaveCommand.ExecuteAsync(null);
        Assert.True(document.IsDirty);
        Assert.Contains("remote file changed", workspace.StatusText, StringComparison.OrdinalIgnoreCase);
        workspace.RemoteExplorer.DisconnectHostCommand.Execute(null);
        Assert.False(workspace.RunCommand.CanExecute(null));
        await workspace.OpenRemoteFolderAsync(SshTestServer.Host, root);
        Assert.Same(document, workspace.ActiveDocument);
        Assert.True(document.IsDirty);
        var reload = workspace.RemoteExplorer.ReloadFileCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen);
        workspace.Prompt.ConfirmCommand.Execute(null);
        await reload;
        var resolved = workspace.ActiveDocument!;
        Assert.False(resolved.IsDirty);
        Assert.Contains("other developer", resolved.CurrentText, StringComparison.Ordinal);
        resolved.Document.Text = "// conflict resolved\n";
        await workspace.SaveCommand.ExecuteAsync(null);
        Assert.False(resolved.IsDirty);
        Assert.Contains("conflict resolved", server.Sftp.ReadAllText(root + "/Program.cs"), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Local_AspNet_publish_builds_real_output_uploads_it_and_rolls_back_a_failed_service_restart()
    {
        if (!SshTestServer.Available) { return; }
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var project = Path.Combine(paths.Projects, "PublishWeb");
        Directory.CreateDirectory(project);
        await File.WriteAllTextAsync(Path.Combine(project, "PublishWeb.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(project, "Program.cs"), "var app = WebApplication.CreateBuilder(args).Build(); app.MapGet(\"/\", () => \"published from tablet\"); app.Run();");
        using var services = AppServices.Create(paths);
        var remoteServices = services.GetRequiredService<RemoteSshServices>();
        remoteServices.Hosts.Save([SshTestServer.Host]);
        remoteServices.Hosts.Trust(SshTestServer.Host, SshTestServer.Fingerprint);
        using var server = await SshTestServer.ConnectAsync();
        var destination = SshTestServer.Root + "/app-publish-" + Guid.NewGuid().ToString("N");
        using (var setup = server.Ssh.RunCommand("mkdir -p " + SshRuntime.Quote(destination + "/previous") + " && ln -s " + SshRuntime.Quote(destination + "/previous") + " " + SshRuntime.Quote(destination + "/current"))) { Assert.Equal(0, setup.ExitStatus); }
        new ServerPublishProfile(SshTestServer.Host.Name, destination, "tabstudio-intentionally-absent.service", "linux-x64").Save(project);
        var main = services.GetRequiredService<MainViewModel>();
        await main.OpenWorkspaceAsync(project);
        var workspace = main.Workspace!;
        var publish = workspace.PublishToServerCommand.ExecuteAsync(null);
        foreach (var title in new[] { "Publish to server", "Publish destination", "systemd service", "Server runtime", "Publish to " + SshTestServer.Host.Name + "?" })
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen && workspace.Prompt.Title == title, TimeSpan.FromSeconds(30), title);
            workspace.Prompt.ConfirmCommand.Execute(null);
        }
        await publish.WaitAsync(TimeSpan.FromMinutes(3));
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains("previous current link was restored", workspace.StatusText, StringComparison.Ordinal);
        var release = Assert.Single(server.Sftp.ListDirectory(destination + "/releases").Where(f => f.IsDirectory && f.Name is not "." and not ".."));
        Assert.True(server.Sftp.Exists(release.FullName + "/PublishWeb.dll"));
        using var current = server.Ssh.RunCommand("readlink " + SshRuntime.Quote(destination + "/current"));
        Assert.Equal(destination + "/previous", current.Result.Trim());
    }

    [AvaloniaFact]
    public async Task Screenshot_of_Remote_Explorer_in_the_IDE()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        using var services = AppServices.Create(paths);
        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 850 };
        window.Show();
        try
        {
            await main.OpenWorkspaceAsync(paths.Projects);
            var workspace = main.Workspace!;
            var model = workspace.RemoteExplorer;
            model.Alias = "Development server"; model.HostName = "dev.example.com"; model.User = "deploy";
            model.SaveHostCommand.Execute(null);
            model.RemoteFolder = "/srv/projects/HelloWeb";
            model.Status = "Choose a host, install the device public key, then open a remote folder.";
            workspace.ShowRemoteExplorerCommand.Execute(null);
            await UiTestHelpers.WaitUntilAsync(() => true);
            UiTestHelpers.SaveScreenshot(window, "50-remote-explorer");
        }
        finally { window.Close(); }
    }
}
