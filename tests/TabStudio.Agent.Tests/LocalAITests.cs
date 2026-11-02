using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using TabStudio.Agent.Anthropic;
using TabStudio.Agent.Local;
using TabStudio.Testing;
using TabStudio.Toolchains.Downloads;

namespace TabStudio.Agent.Tests;

public sealed class LocalAITests
{
    [Theory]
    [InlineData("test.cs")]
    [InlineData("test.py")]
    [InlineData("test.html")]
    [InlineData("test.rs")]
    [InlineData("test.unknown")]
    public void Fim_uses_the_model_tokens_and_unsaved_neighbors_for_every_language(string path)
    {
        var prompt = FimPromptBuilder.Build(path, "beforeafter", 6, [new("other.txt", "unsaved neighbor"), new(path, "must not duplicate")]);
        Assert.Contains("<|fim_prefix|>before<|fim_suffix|>after<|fim_middle|>", prompt.Text, StringComparison.Ordinal);
        Assert.Contains("unsaved neighbor", prompt.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("must not duplicate", prompt.Text, StringComparison.Ordinal);
        Assert.Contains("<|endoftext|>", prompt.Stops);
    }

    [Fact]
    public void Fim_bounds_Unicode_context_without_splitting_surrogate_pairs_and_keeps_the_caret_surroundings()
    {
        var text = string.Concat(Enumerable.Repeat("😀שלום", 4000)) + "beforeafter";
        var prompt = FimPromptBuilder.Build("test.cs", text, text.Length - 5, Enumerable.Range(0, 30).Select(i => new OpenFileContext(i + ".cs", text)));
        Assert.InRange(Encoding.UTF8.GetByteCount(prompt.Text), 1, 3000);
        Assert.DoesNotContain("\uFFFD", Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(prompt.Text)), StringComparison.Ordinal);
        Assert.Contains("before<|fim_suffix|>after", prompt.Text, StringComparison.Ordinal);
        Assert.Equal("result", FimPromptBuilder.Clean("result<|fim_suffix|>junk", prompt.Stops, "after"));
        Assert.Throws<ArgumentOutOfRangeException>(() => FimPromptBuilder.Build("a", "x", 2));
    }

    [Fact]
    public async Task Completion_protocol_is_compatible_with_a_fake_llama_server()
    {
        var server = new FakeServer();
        using var http = new HttpClient(server);
        using var client = new LoopbackCompletionClient(new Uri("http://127.0.0.1:18791"), server);
        var result = await client.GenerateAsync("<|fim_prefix|>return ", 32);
        Assert.Equal("42;", result.Text);
        Assert.Equal(3, result.Tokens);
        Assert.Equal(17.5, result.TokensPerSecond);
        Assert.Equal("/completion", server.Path);
        Assert.Equal(32, server.Request!["n_predict"]!.GetValue<int>());
        Assert.False(server.Request["stream"]!.GetValue<bool>());
        Assert.Throws<ArgumentException>(() => new LoopbackCompletionClient(new Uri("https://example.com")));
        Assert.Throws<ArgumentException>(() => new LoopbackCompletionClient(new Uri("http://localhost:80")));
    }

    [Fact]
    public async Task Models_require_license_acceptance_and_are_only_installed_after_size_and_hash_match()
    {
        using var temp = new TempDirectory();
        var content = Encoding.UTF8.GetBytes("GGUF verified fake model");
        using var http = new HttpClient(new BytesServer(content));
        using var manager = new ModelManager(temp.Path, http);
        var model = Model(content);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DownloadAsync(model, false));
        Assert.False(manager.IsInstalled(model));
        await manager.DownloadAsync(model, true);
        await manager.VerifyAsync(model);
        Assert.True(manager.IsInstalled(model));
        await File.WriteAllTextAsync(manager.PathFor(model), new string('x', content.Length));
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.VerifyAsync(model));
        await manager.DeleteAsync(model);
        var mismatch = await Assert.ThrowsAsync<DownloadException>(() => manager.DownloadAsync(model with { Sha256 = new string('0', 64) }, true));
        Assert.Equal(DownloadFailure.IntegrityFailed, mismatch.Failure);
        Assert.False(File.Exists(manager.PathFor(model)));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.part"));
        await manager.DownloadAsync(model, true);
        await manager.DeleteAsync(model);
        Assert.False(manager.IsInstalled(model));
    }

    [Fact]
    public async Task Cancellation_keeps_what_arrived_and_the_next_download_continues_from_it()
    {
        using var temp = new TempDirectory();
        var content = RandomNumberGenerator.GetBytes(300000);
        var server = new ResumableServer(content, ResumableServer.Ending.Complete);
        using var http = new HttpClient(server);
        using var cancellation = new CancellationTokenSource();
        using var manager = new ModelManager(temp.Path, http) { RetryDelay = TimeSpan.Zero };
        var model = Model(content);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.DownloadAsync(model, true, new CancelProgress(cancellation), cancellation.Token));
        Assert.False(manager.IsInstalled(model));
        var kept = new FileInfo(manager.PartialPathFor(model)).Length;
        Assert.InRange(kept, 1, content.Length - 1);

        await manager.DownloadAsync(model, true);

        Assert.Equal($"bytes={kept}-", server.Ranges[^1]);
        await manager.VerifyAsync(model);
        Assert.False(File.Exists(manager.PartialPathFor(model)));
    }

    [Fact]
    public async Task A_broken_connection_is_resumed_with_a_range_request_not_restarted()
    {
        using var temp = new TempDirectory();
        var content = RandomNumberGenerator.GetBytes(200000);
        var server = new ResumableServer(content, ResumableServer.Ending.Break, cut: 70000);
        using var http = new HttpClient(server);
        using var manager = new ModelManager(temp.Path, http) { Retries = 0, RetryDelay = TimeSpan.Zero };
        var model = Model(content);

        var broken = await Assert.ThrowsAsync<DownloadException>(() => manager.DownloadAsync(model, true));
        Assert.Equal(DownloadFailure.Interrupted, broken.Failure);
        Assert.Equal(70000, new FileInfo(manager.PartialPathFor(model)).Length);

        await manager.DownloadAsync(model, true);

        Assert.Equal(new string?[] { null, "bytes=70000-" }, server.Ranges);
        await manager.VerifyAsync(model);
    }

    [Fact]
    public async Task A_stalled_download_is_detected_and_continued_from_the_last_byte()
    {
        using var temp = new TempDirectory();
        var content = RandomNumberGenerator.GetBytes(200000);
        var server = new ResumableServer(content, ResumableServer.Ending.Stall, cut: 50000);
        using var http = new HttpClient(server) { Timeout = Timeout.InfiniteTimeSpan };
        using var manager = new ModelManager(temp.Path, http) { StallTimeout = TimeSpan.FromMilliseconds(300), Retries = 1, RetryDelay = TimeSpan.Zero };
        var model = Model(content);

        await manager.DownloadAsync(model, true).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(new string?[] { null, "bytes=50000-" }, server.Ranges);
        await manager.VerifyAsync(model);
    }

    [Fact]
    public async Task A_model_larger_than_its_pinned_size_is_refused_before_it_fills_storage()
    {
        using var temp = new TempDirectory();
        var content = Encoding.UTF8.GetBytes("GGUF pinned size");
        using var http = new HttpClient(new BytesServer([.. content, .. new byte[100000]]));
        using var manager = new ModelManager(temp.Path, http);
        var model = Model(content);

        var error = await Assert.ThrowsAsync<DownloadException>(() => manager.DownloadAsync(model, true));

        Assert.Equal(DownloadFailure.IntegrityFailed, error.Failure);
        Assert.False(manager.IsInstalled(model));
        Assert.False(File.Exists(manager.PartialPathFor(model)));
    }

    [Fact]
    public async Task Local_agent_changes_use_the_existing_proposal_approval_flow_with_no_cloud_or_MCP()
    {
        using var temp = new TempDirectory();
        temp.Write("test.txt", "before");
        var generator = new FakeGenerator("""{"tool":"edit_file","arguments":{"path":"test.txt","old_text":"before","new_text":"after"}}""", "Proposed the change.");
        var session = new AgentSession(new LocalAgentClient(generator), new FolderWorkspace(temp), allowedTools: LocalAgentClient.AllowedTools);
        await session.SendAsync("Fix this file and generate a test.");
        Assert.Equal("before", File.ReadAllText(temp.Resolve("test.txt")));
        Assert.Single(session.Changes.Pending);
        await session.ApproveAllAsync();
        Assert.Equal("after", File.ReadAllText(temp.Resolve("test.txt")));
        Assert.All(generator.Prompts, prompt =>
        {
            Assert.DoesNotContain("delete_file", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("run_tests", prompt, StringComparison.Ordinal);
            Assert.StartsWith("<|im_start|>system", prompt, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Local_agent_rejects_unavailable_tools_and_images_without_executing_them()
    {
        using var temp = new TempDirectory();
        var session = new AgentSession(new LocalAgentClient(new FakeGenerator("""{"tool":"delete_file","arguments":{"path":"file"}}""")), new FolderWorkspace(temp), allowedTools: LocalAgentClient.AllowedTools);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Delete"));
        Assert.Empty(session.Changes.Pending);
        var client = new LocalAgentClient(new FakeGenerator());
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(new MessageRequest("local", null, [new ChatMessage(MessageRole.User, [new ImageBlock("image/png", new byte[] { 1 })])], [])));
    }

    [Theory]
    [InlineData(true, 100, true, true)]
    [InlineData(false, 15, false, true)]
    [InlineData(false, 16, false, false)]
    [InlineData(false, 5, true, false)]
    [InlineData(false, -1, false, false)]
    public void Completion_pauses_when_hot_or_on_low_battery(bool hot, int battery, bool charging, bool paused) => Assert.Equal(paused, InferencePolicy.PauseReason(hot, battery, charging) is not null);

    [Fact]
    public void Catalog_pins_hashes_versions_sizes_and_the_correct_individual_licenses()
    {
        Assert.Equal(3, ModelCatalog.All.Count);
        Assert.All(ModelCatalog.All, model =>
        {
            Assert.Equal(64, model.Sha256.Length);
            Assert.Contains("/resolve/", model.Url, StringComparison.Ordinal);
            Assert.DoesNotContain("/main/", model.Url, StringComparison.Ordinal);
            Assert.NotEmpty(model.LicenseText);
            Assert.True(model.RamBytes > model.Size);
        });
        Assert.Equal("Qwen Research License", ModelCatalog.All[1].License);
    }

    private static LocalModel Model(byte[] bytes) => new("test-model", "Test", "https://huggingface.co/test/model.gguf", Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length, 1000, "MIT", "https://huggingface.co/test/LICENSE", true);
    private sealed class CancelProgress(CancellationTokenSource cancellation) : IProgress<long> { public void Report(long value) { if (value > 0) cancellation.Cancel(); } }

    /// <summary>Serves a file whose first response ends early, by breaking or stalling, and honours Range requests.</summary>
    private sealed class ResumableServer(byte[] bytes, ResumableServer.Ending ending, int? cut = null) : HttpMessageHandler
    {
        public enum Ending { Complete, Break, Stall }

        public List<string?> Ranges { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.Single();
            lock (Ranges) Ranges.Add(request.Headers.Range?.ToString());
            if (range?.From is not { } from)
            {
                var first = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new EndingStream(bytes[..(cut ?? bytes.Length)], ending)) };
                first.Content.Headers.ContentLength = bytes.Length;
                return Task.FromResult(first);
            }

            var rest = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes[(int)from..]) };
            rest.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, bytes.Length - 1, bytes.Length);
            return Task.FromResult(rest);
        }
    }

    private sealed class EndingStream(byte[] data, ResumableServer.Ending ending) : Stream
    {
        private int _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_position < data.Length)
            {
                var count = Math.Min(buffer.Length, Math.Min(16384, data.Length - _position));
                data.AsMemory(_position, count).CopyTo(buffer);
                _position += count;
                return count;
            }

            if (ending == ResumableServer.Ending.Stall) await Task.Delay(Timeout.Infinite, cancellationToken);
            if (ending == ResumableServer.Ending.Break) throw new IOException("The connection was reset by the peer.");
            return 0;
        }
    }
    private sealed class BytesServer(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }
    private sealed class FakeServer : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public JsonObject? Request { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            Request = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"content":"42;","tokens_predicted":3,"timings":{"predicted_per_second":17.5}}""") };
        }
    }
    private sealed class FakeGenerator(params string[] replies) : ILocalTextGenerator
    {
        private int _next;
        public List<string> Prompts { get; } = [];
        public Task<LocalCompletion> GenerateAsync(string prompt, int maxTokens, Action<string>? onText = null, CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            var result = replies[_next++];
            onText?.Invoke(result);
            return Task.FromResult(new LocalCompletion(result, 12, 15));
        }
    }
}

public sealed class LocalNativeFactAttribute : FactAttribute
{
    public LocalNativeFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TABSTUDIO_LOCAL_AI_MODEL"))) Skip = "Set TABSTUDIO_LOCAL_AI_MODEL and TABSTUDIO_LOCAL_AI_LIBRARY to run real inference.";
    }
}

public sealed class LocalNativeTests
{
    [LocalNativeFact]
    public async Task Tiny_GGUF_generates_real_tokens_and_can_be_canceled_and_reused()
    {
        using var generator = new NativeTextGenerator(Environment.GetEnvironmentVariable("TABSTUDIO_LOCAL_AI_MODEL")!, context: 512);
        var text = new StringBuilder();
        var result = await generator.GenerateAsync("Once upon a time", 16, piece => text.Append(piece));
        Assert.True(result.Tokens > 0);
        Assert.True(result.TokensPerSecond > 0);
        Assert.Equal(result.Text, text.ToString());
        Assert.NotEmpty(result.Text);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generator.GenerateAsync("Once upon a time", 64, _ => cancellation.Cancel(), cancellation.Token));
        Assert.True((await generator.GenerateAsync("Once upon a time", 8)).Tokens > 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAsync(new string('x', 10000), 64));
    }
}
