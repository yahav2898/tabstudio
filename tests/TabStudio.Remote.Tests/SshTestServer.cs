using Renci.SshNet;
using TabStudio.Remote;

namespace TabStudio.Remote.Tests;

/// <summary>The same real OpenSSH endpoint is used by library and App tests. CI always supplies it.</summary>
public static class SshTestServer
{
    public static bool Available => Environment.GetEnvironmentVariable("TABSTUDIO_SSH_HOST") is { Length: > 0 };
    public static SshHost Host => new("CI OpenSSH", Environment.GetEnvironmentVariable("TABSTUDIO_SSH_HOST")!, Environment.GetEnvironmentVariable("TABSTUDIO_SSH_USER")!, int.Parse(Environment.GetEnvironmentVariable("TABSTUDIO_SSH_PORT") ?? "2222", System.Globalization.CultureInfo.InvariantCulture), Environment.GetEnvironmentVariable("TABSTUDIO_SSH_IDENTITY"));
    public static string Fingerprint => Environment.GetEnvironmentVariable("TABSTUDIO_SSH_FINGERPRINT")!;
    public static string Root => Environment.GetEnvironmentVariable("TABSTUDIO_SSH_ROOT") ?? "/home/developer/workspaces";
    public static string AuthorizedKeys => Environment.GetEnvironmentVariable("TABSTUDIO_SSH_AUTHORIZED_KEYS") ?? "/home/developer/.ssh/authorized_keys";
    public static SshIdentity Identity()
    {
        var file = new PrivateKeyFile(Host.IdentityFile!);
        return new SshIdentity(file, string.Empty, file);
    }
    public static Task<SshConnection> ConnectAsync() => SshConnection.ConnectAsync(Host, Identity(), Fingerprint);
}

public sealed class SshFactAttribute : FactAttribute
{
    public SshFactAttribute() { if (!SshTestServer.Available) { Skip = "Set TABSTUDIO_SSH_* or run build/ci/start-ssh-server.sh to exercise the real OpenSSH server."; } }
}
