using System.Text;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Tests.Processes;

/// <summary>Collects a running program's output on background threads, for assertions.</summary>
internal sealed class ProcessOutput
{
    private readonly StringBuilder _output = new();
    private readonly StringBuilder _error = new();
    private readonly Lock _gate = new();
    private readonly Task _readers;

    public ProcessOutput(ToolchainProcess process)
    {
        _readers = Task.WhenAll(
            Task.Run(() => Pump(process.Output, _output)),
            process.Error is { } error ? Task.Run(() => Pump(error, _error)) : Task.CompletedTask);
    }

    public string Output
    {
        get
        {
            lock (_gate)
            {
                return _output.ToString();
            }
        }
    }

    public string Error
    {
        get
        {
            lock (_gate)
            {
                return _error.ToString();
            }
        }
    }

    /// <summary>Waits until the program's streams are closed and read to the end.</summary>
    public Task CompletedAsync() => _readers.WaitAsync(TimeSpan.FromSeconds(30));

    /// <summary>Waits until the output contains some text.</summary>
    public async Task WaitForAsync(string text, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (!Output.Contains(text, StringComparison.Ordinal))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"'{text}' did not appear in the output: {Output}");
            }

            await Task.Delay(20);
        }
    }

    private void Pump(Stream stream, StringBuilder target)
    {
        var buffer = new byte[4096];
        var decoder = Encoding.UTF8.GetDecoder();
        var chars = new char[4096];
        while (true)
        {
            int read;
            try
            {
                read = stream.Read(buffer, 0, buffer.Length);
            }
            catch (IOException)
            {
                break;
            }

            if (read <= 0)
            {
                break;
            }

            var count = decoder.GetChars(buffer, 0, read, chars, 0);
            lock (_gate)
            {
                target.Append(chars, 0, count);
            }
        }
    }
}
