using System.Security.Cryptography;
using TabStudio.Testing;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Tests.Fakes;

namespace TabStudio.Toolchains.Tests.Downloads;

/// <summary>Downloads that survive a weak connection and never keep a damaged file.</summary>
public sealed class DownloaderTests : IDisposable
{
    private static readonly Uri Address = new("https://downloads.example.org/toolchain.tar.gz");

    private readonly FakeHttpHandler _server = new();
    private readonly HttpClient _client;
    private readonly Downloader _downloader;
    private readonly TempDirectory _temp = new();
    private readonly byte[] _content = RandomNumberGenerator.GetBytes(300_000);

    public DownloaderTests()
    {
        _client = _server.CreateClient();
        _downloader = new Downloader(_client) { RetryDelay = TimeSpan.FromMilliseconds(10) };
        _server.Add(Address, _content);
    }

    private string Destination => _temp.Resolve("downloads/toolchain.tar.gz");

    [Fact]
    public async Task Downloads_and_verifies_a_file()
    {
        var reports = new List<DownloadProgress>();

        await _downloader.DownloadAsync(Request(), new SynchronousProgress<DownloadProgress>(reports.Add));

        Assert.Equal(_content, await File.ReadAllBytesAsync(Destination));
        Assert.False(File.Exists(Destination + ".part"));
        Assert.Equal(new DownloadProgress(_content.Length, _content.Length), reports[^1]);
        Assert.True(reports.Count > 1);
    }

    [Fact]
    public async Task Continues_after_the_connection_breaks()
    {
        _server.BreakAfter(Address, 100_000);

        await _downloader.DownloadAsync(Request());

        Assert.Equal(_content, await File.ReadAllBytesAsync(Destination));
        Assert.Equal([$"GET {Address}", $"GET {Address} (100000-)"], _server.Requests);
    }

    [Fact]
    public async Task Continues_a_download_an_earlier_attempt_left_behind()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
        await File.WriteAllBytesAsync(Destination + ".part", _content[..120_000]);

        await _downloader.DownloadAsync(Request());

        Assert.Equal(_content, await File.ReadAllBytesAsync(Destination));
        Assert.Equal([$"GET {Address} (120000-)"], _server.Requests);
    }

    [Fact]
    public async Task Keeps_what_arrived_when_the_download_fails()
    {
        _server.BreakAfter(Address, 50_000);
        var downloader = new Downloader(_client) { Retries = 0 };

        await Assert.ThrowsAsync<DownloadException>(() => downloader.DownloadAsync(Request()));

        Assert.Equal(50_000, new FileInfo(Destination + ".part").Length);
        await _downloader.DownloadAsync(Request());
        Assert.Equal(_content, await File.ReadAllBytesAsync(Destination));
    }

    [Fact]
    public async Task Deletes_a_file_with_the_wrong_hash()
    {
        var request = Request() with { Sha256 = new string('0', 64) };

        var error = await Assert.ThrowsAsync<DownloadException>(() => _downloader.DownloadAsync(request));

        Assert.Equal(DownloadFailure.IntegrityFailed, error.Failure);
        Assert.Contains("SHA-256", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Destination));
        Assert.False(File.Exists(Destination + ".part"));
    }

    [Fact]
    public async Task Runs_the_request_s_own_verification()
    {
        var request = Request() with { Verify = _ => "it is not a package." };

        var error = await Assert.ThrowsAsync<DownloadException>(() => _downloader.DownloadAsync(request));

        Assert.Equal(DownloadFailure.IntegrityFailed, error.Failure);
        Assert.Contains("it is not a package.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Skips_a_file_that_is_already_complete()
    {
        await _downloader.DownloadAsync(Request());
        _server.Requests.Clear();

        await _downloader.DownloadAsync(Request());

        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task Says_when_the_device_is_offline()
    {
        _server.Offline = true;

        var error = await Assert.ThrowsAsync<DownloadException>(() => _downloader.DownloadAsync(Request()));

        Assert.Equal(DownloadFailure.NoNetwork, error.Failure);
        Assert.Contains("offline", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Says_when_the_file_is_gone()
    {
        var request = Request() with { Address = new Uri("https://downloads.example.org/missing.tar.gz") };

        var error = await Assert.ThrowsAsync<DownloadException>(() => _downloader.DownloadAsync(request));

        Assert.Equal(DownloadFailure.NotFound, error.Failure);
    }

    [Fact]
    public async Task Cancelling_stops_the_download()
    {
        _server.Latency = TimeSpan.FromSeconds(30);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _downloader.DownloadAsync(Request(), null, cancellation.Token));
    }

    [Fact]
    public async Task A_server_that_never_answers_is_given_up_on_instead_of_waited_for()
    {
        // The stall limit covered the body only: a connection that never sent headers held the install forever.
        _server.Latency = TimeSpan.FromMinutes(5);
        var downloader = new Downloader(_client) { Retries = 0, StallTimeout = TimeSpan.FromMilliseconds(200) };
        var clock = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAsync<DownloadException>(() => downloader.DownloadAsync(Request()));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), clock.Elapsed.ToString());
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        _temp.Dispose();
    }

    private DownloadRequest Request() => new()
    {
        Address = Address,
        Destination = Destination,
        Name = "the toolchain",
        Size = _content.Length,
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(_content)),
    };
}

/// <summary>An <see cref="IProgress{T}"/> that reports on the calling thread, so tests see every report in order.</summary>
internal sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
