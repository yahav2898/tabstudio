using System.Text;

namespace TabStudio.Toolchains.Embedded.Debugging;

/// <summary>Streams UTF-8 RTT logs with bounded chunks and callback backpressure; never decodes binary defmt.</summary>
public static class EmbeddedRttTextReader
{
    /// <summary>Reads strict UTF-8 text without taking ownership of the channel stream.</summary>
    /// <param name="stream">Connected loopback RTT up-channel.</param>
    /// <param name="append">Awaited consumer; UI consumers can prevent unbounded queued log messages.</param>
    /// <param name="cancellationToken">Debug session lifetime.</param>
    public static async Task CopyAsync(Stream stream, Func<string, Task> append, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(append);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        char[] chunk = new char[2048];
        while (await reader.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false) is var count && count > 0)
        {
            await append(new string(chunk, 0, count)).ConfigureAwait(false);
        }
    }
}
