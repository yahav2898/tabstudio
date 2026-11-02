using System.Diagnostics;
using TabStudio.Remote;
using TabStudio.Testing;

namespace TabStudio.Remote.Tests;

public sealed class PublishingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Activation_switches_atomically_and_restores_previous_release_after_restart_failure(bool fail)
    {
        if (OperatingSystem.IsWindows()) { return; }
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "app with 'quotes'");
        Directory.CreateDirectory(root);
        var previous = Path.Combine(root, "old"); var release = Path.Combine(root, "new");
        Directory.CreateDirectory(previous); Directory.CreateDirectory(release);
        Directory.CreateSymbolicLink(Path.Combine(root, "current"), previous);
        var bin = Path.Combine(temp.Path, "bin"); Directory.CreateDirectory(bin);
        var sudo = temp.Write("bin/sudo", "#!/bin/sh\n[ \"$1\" = '-n' ] && shift\nexec \"$@\"\n");
        var systemctl = temp.Write("bin/systemctl", "#!/bin/sh\nif [ -f \"$FAIL_ONCE\" ]; then rm -- \"$FAIL_ONCE\"; exit 1; fi\nexit 0\n");
        File.SetUnixFileMode(sudo, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.SetUnixFileMode(systemctl, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var flag = Path.Combine(temp.Path, "fail-once"); if (fail) { File.WriteAllText(flag, "1"); }
        var start = new ProcessStartInfo("sh") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("-c"); start.ArgumentList.Add(ServerPublisher.ActivationScript(root, release, "hello.service"));
        start.Environment["PATH"] = bin + ":" + Environment.GetEnvironmentVariable("PATH");
        start.Environment["FAIL_ONCE"] = flag;
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(fail ? 1 : 0, process.ExitCode);
        Assert.Equal(fail ? previous : release, new DirectoryInfo(Path.Combine(root, "current")).LinkTarget);
    }

    [Theory]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite, 644)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead, 644)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, 755)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead, 664)]
    public void A_published_file_is_readable_by_the_service_and_its_mode_is_written_as_SSH_NET_reads_it(UnixFileMode local, int expected)
    {
        // Android creates files 0600, which threw; 0644 became 0420. SSH.NET reads the digits of the number as octal.
        Assert.Equal((short)expected, ServerPublisher.PublishedMode(local));
    }

    [SshFact]
    public async Task Upload_stages_publish_output_and_failed_activation_preserves_the_current_release()
    {
        using var temp = new TempDirectory();
        temp.Write("HelloWeb.dll", "a publish artifact"); temp.Write("wwwroot/index.html", "Hello from the tablet");
        using var connection = await SshTestServer.ConnectAsync();
        var root = SshTestServer.Root + "/publish-" + Guid.NewGuid().ToString("N");
        using (var setup = connection.Ssh.RunCommand("mkdir -p " + SshRuntime.Quote(root + "/previous") + " && ln -s " + SshRuntime.Quote(root + "/previous") + " " + SshRuntime.Quote(root + "/current"))) { Assert.Equal(0, setup.ExitStatus); }
        // The container has no systemd unit/sudo policy for this intentionally absent service.
        var profile = new ServerPublishProfile(SshTestServer.Host.Name, root, "tabstudio-intentionally-absent.service");
        await Assert.ThrowsAsync<IOException>(() => ServerPublisher.UploadAndRestartAsync(connection, temp.Path, profile));
        using var current = connection.Ssh.RunCommand("readlink " + SshRuntime.Quote(root + "/current"));
        Assert.Equal(root + "/previous", current.Result.Trim());
        var release = Assert.Single(connection.Sftp.ListDirectory(root + "/releases"), f => f.IsDirectory && f.Name is not "." and not "..");
        Assert.Equal("Hello from the tablet", connection.Sftp.ReadAllText(release.FullName + "/wwwroot/index.html"));
    }

    [Fact]
    public async Task Content_search_uses_literal_queries_skips_generated_folders_and_returns_navigable_locations()
    {
        using var temp = new TempDirectory();
        temp.Write("src/Program.cs", "line one\n// needle $(not-a-command)\n");
        temp.Write("node_modules/skip.txt", "needle");
        var hits = await RemoteSearch.FindAsync(temp.Path, "needle $(not-a-command)");
        var hit = Assert.Single(hits);
        Assert.Equal(Path.Combine(temp.Path, "src", "Program.cs"), hit.FilePath);
        Assert.Equal(2, hit.Line); Assert.Equal(4, hit.Column);
    }

    [Fact]
    public async Task Content_search_stops_at_its_limit_across_folders()
    {
        using var temp = new TempDirectory();
        for (var i = 0; i < 5; i++) temp.Write($"a{i}/many.txt", string.Concat(Enumerable.Repeat("needle\n", 150)));
        var hits = await RemoteSearch.FindAsync(temp.Path, "needle");
        Assert.Equal(RemoteSearch.MaximumHits, hits.Count);
    }
}
