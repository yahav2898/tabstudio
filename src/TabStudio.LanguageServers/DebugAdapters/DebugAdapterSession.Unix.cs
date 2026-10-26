using System.Net.Sockets;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.LanguageServers.DebugAdapters;

public sealed partial class DebugAdapterSession
{
    private string? _unixSocketDirectory;

    private static string CreateUnixSocketDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new DebugAdapterException("Unix socket adapters require a Unix host.");
        }

        var directory = Path.Combine(Path.GetTempPath(), "ts-dap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return directory;
    }

    private static IServerProcess StartAdapterProcess(IServerProcessLauncher launcher, ServerStartInfo start, string? socketDirectory)
    {
        try
        {
            return launcher.Start(start);
        }
        catch
        {
            RemoveUnixSocketDirectory(socketDirectory);
            throw;
        }
    }

    private static async Task<Socket> ConnectToUnixSocketAsync(string path, TimeSpan timeout, IServerProcess process, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken).ConfigureAwait(false);
                return socket;
            }
            catch (SocketException) when (DateTime.UtcNow < deadline && !process.Exited.IsCompleted)
            {
                socket.Dispose();
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }

    private static void RemoveUnixSocketDirectory(string? directory)
    {
        if (directory is null)
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A failed start or a second cleanup may have already removed the socket.
        }
    }
}
