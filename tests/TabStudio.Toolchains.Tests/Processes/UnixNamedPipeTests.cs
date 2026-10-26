using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Tests.Processes;

/// <summary>Real Unix FIFOs used to answer native debuggees without writing into DAP's stdin.</summary>
public sealed class UnixNamedPipeTests
{
    [Fact]
    public void A_private_FIFO_preserves_console_input_and_reports_creation_errors()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var folder = new TempDirectory();
        var path = folder.Resolve("stdin");
        UnixNamedPipe.Create(path);
        using var pipe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1, useAsync: false);
        var answer = Encoding.UTF8.GetBytes("Ada λ\n");
        pipe.Write(answer);
        pipe.Flush();
        var received = new byte[answer.Length];
        pipe.ReadExactly(received);

        Assert.Equal(answer, received);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        Assert.Throws<IOException>(() => UnixNamedPipe.Create(path));
    }
}
