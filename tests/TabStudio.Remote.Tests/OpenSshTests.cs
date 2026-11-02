using System.Text;
using TabStudio.Remote;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Remote.Tests;

public sealed class OpenSshTests
{
    [SshFact]
    public async Task Unknown_and_changed_host_keys_are_rejected_before_authentication()
    {
        var unknown = await Assert.ThrowsAsync<SshHostKeyException>(() => SshConnection.ConnectAsync(SshTestServer.Host, SshTestServer.Identity(), null));
        Assert.False(unknown.Changed);
        Assert.Equal(SshTestServer.Fingerprint, unknown.Fingerprint);
        var changed = await Assert.ThrowsAsync<SshHostKeyException>(() => SshConnection.ConnectAsync(SshTestServer.Host, SshTestServer.Identity(), "SHA256:wrong"));
        Assert.True(changed.Changed);
    }

    [SshFact]
    public async Task Sftp_cache_edits_conflicts_symlinks_and_path_mapping_are_real()
    {
        using var temp = new TempDirectory();
        using var connection = await SshTestServer.ConnectAsync();
        var root = SshTestServer.Root + "/files-" + Guid.NewGuid().ToString("N");
        connection.Sftp.CreateDirectory(root);
        using (var content = new MemoryStream("original\n"u8.ToArray())) { connection.Sftp.UploadFile(content, root + "/Program.cs"); }
        using (var command = connection.Ssh.RunCommand("ln -s /etc/passwd " + SshRuntime.Quote(root + "/outside"))) { Assert.Equal(0, command.ExitStatus); }
        using var workspace = new RemoteWorkspace(connection, root, temp.Path);
        await workspace.RefreshAsync();
        var local = Path.Combine(temp.Path, "Program.cs");
        Assert.Equal("original\n", await File.ReadAllTextAsync(local));
        Assert.False(File.Exists(Path.Combine(temp.Path, "outside")));
        Assert.Null(workspace.ToLocalPath(root + "/../outside"));
        Assert.Throws<IOException>(() => workspace.ToRemotePath(Path.GetTempPath()));
        Assert.Equal(local, workspace.ToLocalPath("Program.cs"));
        await File.WriteAllTextAsync(local, "edited\n");
        await workspace.SaveAsync(local);
        Assert.Equal("edited\n", connection.Sftp.ReadAllText(root + "/Program.cs"));
        connection.Sftp.WriteAllText(root + "/Program.cs", "concurrent edit\n");
        await File.WriteAllTextAsync(local, "my second edit\n");
        await Assert.ThrowsAsync<IOException>(() => workspace.SaveAsync(local));
        Assert.Equal("concurrent edit\n", connection.Sftp.ReadAllText(root + "/Program.cs"));
        Assert.Equal("my second edit\n", await File.ReadAllTextAsync(local));
        Assert.DoesNotContain(connection.Sftp.ListDirectory(root), f => f.Name.Contains(".tabstudio-", StringComparison.Ordinal));
        await workspace.ReloadFileAsync(local);
        Assert.Equal("concurrent edit\n", await File.ReadAllTextAsync(local));
        await File.WriteAllTextAsync(local, "resolved\n");
        await workspace.SaveAsync(local);
        Assert.Equal("resolved\n", connection.Sftp.ReadAllText(root + "/Program.cs"));
    }

    [SshFact]
    public async Task Sftp_save_creates_new_files_and_folders_and_refuses_files_deleted_on_the_server()
    {
        using var temp = new TempDirectory();
        using var connection = await SshTestServer.ConnectAsync();
        var root = SshTestServer.Root + "/new-" + Guid.NewGuid().ToString("N");
        connection.Sftp.CreateDirectory(root);
        using (var content = new MemoryStream("original\n"u8.ToArray())) { connection.Sftp.UploadFile(content, root + "/Program.cs"); }
        using var workspace = new RemoteWorkspace(connection, root, temp.Path);
        await workspace.RefreshAsync();
        var added = Path.Combine(temp.Path, "Models", "Data", "Item.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(added)!);
        await File.WriteAllTextAsync(added, "class Item {}\n");
        await workspace.SaveAsync(added);
        Assert.Equal("class Item {}\n", connection.Sftp.ReadAllText(root + "/Models/Data/Item.cs"));
        await File.WriteAllTextAsync(added, "class Item { int Id; }\n");
        await workspace.SaveAsync(added);
        Assert.Equal("class Item { int Id; }\n", connection.Sftp.ReadAllText(root + "/Models/Data/Item.cs"));
        connection.Sftp.DeleteFile(root + "/Program.cs");
        var local = Path.Combine(temp.Path, "Program.cs");
        await File.WriteAllTextAsync(local, "edited\n");
        await Assert.ThrowsAsync<IOException>(() => workspace.SaveAsync(local));
        Assert.False(connection.Sftp.Exists(root + "/Program.cs"));
    }

    [SshFact]
    public async Task Device_generated_signer_authenticates_to_OpenSSH()
    {
        using var temp = new TempDirectory();
        var keys = new FileSshKeyStore(temp.Path);
        using var identity = keys.GetOrCreate("generated");
        using var connection = await SshTestServer.ConnectAsync();
        using (var add = connection.Ssh.RunCommand("printf '%s\\n' " + SshRuntime.Quote(identity.PublicKey) + " >> " + SshRuntime.Quote(SshTestServer.AuthorizedKeys))) { Assert.Equal(0, add.ExitStatus); }
        using var generated = await SshConnection.ConnectAsync(SshTestServer.Host, keys.GetOrCreate("generated"), SshTestServer.Fingerprint);
        using var result = generated.Ssh.RunCommand("printf generated-device-key");
        Assert.Equal("generated-device-key", result.Result);
    }

    [SshFact]
    public async Task Commands_stream_before_completion_and_report_nonzero_exit()
    {
        using var temp = new TempDirectory();
        using var connection = await SshTestServer.ConnectAsync();
        using var workspace = new RemoteWorkspace(connection, SshTestServer.Root, temp.Path);
        var runtime = new SshRuntime(workspace);
        using var process = runtime.Start(new ProcessStartSpec { Program = "sh", Arguments = ["-c", "printf 'first\\n'; sleep 2; printf 'error\\n' >&2; exit 7"], WorkingDirectory = temp.Path });
        using var reader = new StreamReader(process.Output);
        Assert.Equal("first", await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(process.Completion.IsCompleted);
        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(7, exit.ExitCode);
        Assert.Equal("error\n", await new StreamReader(process.Error!).ReadToEndAsync());
    }

    [SshFact]
    public async Task Commands_accept_stdin_EOF_and_closing_input_after_exit_is_idempotent()
    {
        using var temp = new TempDirectory();
        using var connection = await SshTestServer.ConnectAsync();
        using var workspace = new RemoteWorkspace(connection, SshTestServer.Root, temp.Path);
        var runtime = new SshRuntime(workspace);
        using (var exited = runtime.Start(new ProcessStartSpec { Program = "printf", Arguments = ["finished\\n"], WorkingDirectory = temp.Path }))
        {
            Assert.Equal(0, (await exited.Completion.WaitAsync(TimeSpan.FromSeconds(10))).ExitCode);
            exited.CloseInput();
            exited.CloseInput();
            Assert.Equal("finished\n", await new StreamReader(exited.Output).ReadToEndAsync());
        }
        using var cat = runtime.Start(new ProcessStartSpec { Program = "cat", WorkingDirectory = temp.Path });
        const string input = "tablet stdin: שלום\n";
        await cat.Input.WriteAsync(Encoding.UTF8.GetBytes(input));
        await cat.Input.FlushAsync();
        cat.CloseInput();
        cat.CloseInput();
        Assert.Equal(0, (await cat.Completion.WaitAsync(TimeSpan.FromSeconds(10))).ExitCode);
        Assert.Equal(input, await new StreamReader(cat.Output).ReadToEndAsync());
    }

    [SshFact]
    public async Task Interactive_shell_supports_input_resizing_and_interrupts()
    {
        using var temp = new TempDirectory();
        using var connection = await SshTestServer.ConnectAsync();
        using var workspace = new RemoteWorkspace(connection, SshTestServer.Root, temp.Path);
        var runtime = new SshRuntime(workspace);
        using var process = runtime.Start(runtime.Shell(temp.Path, new TerminalSize(24, 80)));
        process.Resize(new TerminalSize(32, 100));
        var text = new StringBuilder();
        var bytes = new byte[4096];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        async Task ReadUntilResultAsync()
        {
            while (!text.ToString().Contains("shell-42", StringComparison.Ordinal))
            {
                var count = await process.Output.ReadAsync(bytes, timeout.Token);
                if (count == 0) { break; }
                text.Append(Encoding.UTF8.GetString(bytes, 0, count));
            }
        }
        // Read first, as the Terminal panel does. A pending read must not serialize and block input writes.
        var received = ReadUntilResultAsync();
        await process.Input.WriteAsync(Encoding.UTF8.GetBytes("printf 'shell-%s\\n' $((6*7))\n")).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await process.Input.FlushAsync();
        await received.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains("shell-42", text.ToString(), StringComparison.Ordinal);
        process.Stop();
        Assert.True((await process.Completion.WaitAsync(TimeSpan.FromSeconds(10))).WasStopped);
    }

    [SshFact]
    public async Task Local_forward_reaches_remote_loopback_HTTP_and_closes_when_disposed()
    {
        using var connection = await SshTestServer.ConnectAsync();
        using var serve = connection.Ssh.CreateCommand("python3 -m http.server 38761 --bind 127.0.0.1 --directory " + SshRuntime.Quote(SshTestServer.Root));
        var running = serve.ExecuteAsync(CancellationToken.None);
        using var forward = new PortForward(connection, 38761);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        string? body = null;
        for (var i = 0; i < 30 && body is null; i++)
        {
            try { body = await http.GetStringAsync(forward.Address); }
            catch (HttpRequestException) { await Task.Delay(100); }
        }
        Assert.Contains("Directory listing", body, StringComparison.Ordinal);
        forward.Dispose();
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetStringAsync(forward.Address));
        serve.CancelAsync();
        try { await running; } catch (OperationCanceledException) { }
    }
}
