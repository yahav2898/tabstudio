using TabStudio.Compilation.Running;

namespace TabStudio.Compilation.Tests;

public sealed class ProcessConsoleTests
{
    [Fact]
    public async Task The_console_has_one_owner_at_a_time()
    {
        var first = await ProcessConsole.AcquireAsync();
        var second = ProcessConsole.AcquireAsync();
        using var output = new StringWriter();
        try
        {
            first.Redirect(output, output, TextReader.Null);
            Console.Write("only the first owner writes here");
            await Task.Delay(100);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            first.Dispose();
        }

        using var lease = await second.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal("only the first owner writes here", output.ToString());
    }

    [Fact]
    public async Task Disposing_a_lease_twice_releases_it_once()
    {
        var lease = await ProcessConsole.AcquireAsync();
        lease.Dispose();
        lease.Dispose();

        using var next = await ProcessConsole.AcquireAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var another = ProcessConsole.AcquireAsync();
        await Task.Delay(100);
        Assert.False(another.IsCompleted);
        next.Dispose();
        (await another.WaitAsync(TimeSpan.FromSeconds(30))).Dispose();
    }
}
