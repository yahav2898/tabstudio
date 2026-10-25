using System.Text;
using TabStudio.Toolchains.Processes.Unix;

namespace TabStudio.Toolchains.Tests.Processes;

/// <summary>
/// The streams of programs' pipes: asynchronous reads wait on a thread of the stream's own, so quiet programs, such as
/// language servers between requests, hold no thread of the thread pool.
/// </summary>
public sealed class FileDescriptorStreamTests
{
    [Fact]
    public async Task Waiting_reads_hold_no_thread_pool_thread()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // More quiet pipes than the thread pool starts with threads, many times over.
        const int Pipes = 64;
        var pipes = Enumerable.Range(0, Pipes).Select(_ => Pipe()).ToList();
        try
        {
            var reads = pipes.Select(p => p.Reader.ReadAsync(new byte[16]).AsTask()).ToList();

            Assert.Equal(42, await Task.Run(() => 42).WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.All(reads, r => Assert.False(r.IsCompleted));

            foreach (var (_, writer) in pipes)
            {
                writer.Write("x"u8);
            }

            Assert.All(await Task.WhenAll(reads).WaitAsync(TimeSpan.FromSeconds(20)), read => Assert.Equal(1, read));
        }
        finally
        {
            foreach (var (reader, writer) in pipes)
            {
                writer.Dispose();
                reader.Dispose();
            }
        }
    }

    [Fact]
    public async Task Reads_get_what_was_written_in_order_and_zero_at_the_end()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var (reader, writer) = Pipe();
        using (reader)
        {
            using (writer)
            {
                writer.Write("hello, "u8);
                var first = new byte[64];
                var count = await reader.ReadAsync(first).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                var second = new byte[64];
                var pending = reader.ReadAsync(second, 0, second.Length);
                writer.Write("tablet"u8);
                var more = await pending.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Equal("hello, ", Encoding.UTF8.GetString(first, 0, count));
                Assert.Equal("tablet", Encoding.UTF8.GetString(second, 0, more));
            }

            // The writer is closed: the pipe has ended.
            Assert.Equal(0, await reader.ReadAsync(new byte[8]).AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Fact]
    public async Task A_cancelled_read_does_not_start_and_a_disposed_stream_reads_nothing()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var (reader, writer) = Pipe();
        using (writer)
        {
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(new byte[8], cancelled.Token).AsTask());

            reader.Dispose();
            Assert.Equal(0, await reader.ReadAsync(new byte[8]).AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    private static (FileDescriptorStream Reader, FileDescriptorStream Writer) Pipe()
    {
        UnixNative.EnsureLoaded();
        Span<int> fds = stackalloc int[2];
        Assert.Equal(0, UnixNative.pipe(fds));
        return (new FileDescriptorStream(fds[0], canRead: true), new FileDescriptorStream(fds[1], canRead: false));
    }
}
