namespace TabStudio.Remote;

/// <summary>Publishes into a new release directory, switches current atomically, and restarts an existing systemd unit.</summary>
public static class ServerPublisher
{
    public static async Task UploadAndRestartAsync(SshConnection connection, string publishDirectory, ServerPublishProfile profile, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        profile.Validate();
        if (!Directory.Exists(publishDirectory)) { throw new DirectoryNotFoundException(publishDirectory); }
        var root = RemoteWorkspace.Normalize(profile.RemoteDirectory);
        var release = root + "/releases/" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..12];
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        var files = Directory.EnumerateFiles(publishDirectory, "*", options).ToArray();
        if (files.Length == 0) { throw new IOException("dotnet publish produced no files. Nothing was uploaded."); }
        await Task.Run(() =>
        {
            void MakeDirectory(string path)
            {
                var parent = path[..path.LastIndexOf('/')];
                if (parent.Length > 0 && !connection.Sftp.Exists(parent)) { MakeDirectory(parent); }
                if (!connection.Sftp.Exists(path)) { connection.Sftp.CreateDirectory(path); }
            }
            MakeDirectory(release);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(publishDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
                var target = release + "/" + relative;
                MakeDirectory(target[..target.LastIndexOf('/')]);
                progress?.Report("Uploading " + relative);
                using var input = File.OpenRead(file);
                connection.Sftp.UploadFile(input, target, canOverride: false);
                if (!OperatingSystem.IsWindows())
                {
                    var mode = File.GetUnixFileMode(file);
                    connection.Sftp.ChangePermissions(target, (short)((int)mode & 0x1ff));
                }
            }
        }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Activating release and restarting " + profile.ServiceName);
        // The unit must use <destination>/current. The prior release stays on disk for rollback.
        var script = ActivationScript(root, release, profile.ServiceName);
        using var command = connection.Ssh.CreateCommand(script);
        await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        if (command.ExitStatus != 0) { throw new IOException("The service did not restart. The previous current link was restored. " + command.Error); }
        progress?.Report("Published " + release);
    }

    public static string ActivationScript(string root, string release, string service)
    {
        new ServerPublishProfile("server", root, service).Validate();
        var qRoot = SshRuntime.Quote(RemoteWorkspace.Normalize(root));
        return "set -eu; cd " + qRoot + "; "
            + "if [ -e current ] && [ ! -L current ]; then echo 'current must be a symlink' >&2; exit 1; fi; "
            + "old=$(readlink current || true); temp=.current-" + Guid.NewGuid().ToString("N") + "; "
            + "ln -s -- " + SshRuntime.Quote(release) + " \"$temp\"; mv -Tf -- \"$temp\" current; "
            + "if sudo -n systemctl restart -- " + SshRuntime.Quote(service) + " && systemctl is-active --quiet -- " + SshRuntime.Quote(service) + "; then exit 0; fi; "
            + "if [ -n \"$old\" ]; then ln -s -- \"$old\" \"$temp\"; mv -Tf -- \"$temp\" current; sudo -n systemctl restart -- " + SshRuntime.Quote(service) + " || true; else rm -- current; fi; exit 1";
    }
}
