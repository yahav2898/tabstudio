using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TabStudio.Testing;

namespace TabStudio.SourceControl.Tests;

/// <summary>
/// A Git host for tests, as GitLab or a self-hosted <c>git http-backend</c> is one: real Git's <c>upload-pack</c> and
/// <c>receive-pack</c> in stateless mode behind an <see cref="HttpMessageHandler"/>, so TabStudio's Git protocol is
/// checked against Git itself. Repositories are bare repositories in a temporary folder.
/// </summary>
internal sealed class GitServer : HttpMessageHandler
{
    /// <summary>The host the server answers for.</summary>
    public const string Host = "git.example.test";

    private readonly TempDirectory _temp = new();
    private readonly Lock _gate = new();
    private readonly List<int> _wants = [];
    private int _fetches;

    /// <summary>Where the bare repositories are.</summary>
    public string Root => _temp.Path;

    /// <summary>When set, the next fetch's answer breaks off after this many bytes, as a dropped connection does.</summary>
    public long? BreakFetchAfter { get; set; }

    /// <summary>When set, only the fetch with this number (counting from 1) breaks off, after <see cref="BreakFetchAfter"/> bytes.</summary>
    public int? BreakOnFetch { get; set; }

    /// <summary>Whether the server speaks only Git's original protocol, as older servers do.</summary>
    public bool OnlyVersion0 { get; set; }

    /// <summary>When set, pushes need these credentials, as a private repository's do.</summary>
    public (string User, string Password)? PushCredentials { get; set; }

    /// <summary>How many fetches the server answered.</summary>
    public int Fetches => Volatile.Read(ref _fetches);

    /// <summary>How many objects each fetch asked for by name.</summary>
    public IReadOnlyList<int> Wants
    {
        get
        {
            lock (_gate)
            {
                return [.. _wants];
            }
        }
    }

    /// <summary>The HTTPS address of a repository.</summary>
    public static Uri Address(string path) => new($"https://{Host}/{path}.git");

    /// <summary>Creates a bare repository whose <c>main</c> holds the files, committed by Git.</summary>
    /// <returns>The commit.</returns>
    public string CreateRepository(string path, IReadOnlyDictionary<string, string> files)
    {
        var bare = BarePath(path);
        Directory.CreateDirectory(bare);
        Git(bare, "init", "--bare", "--initial-branch=main", ".");
        Git(bare, "config", "uploadpack.allowFilter", "true");
        Git(bare, "config", "uploadpack.allowAnySHA1InWant", "true");
        Git(bare, "config", "receive.denyCurrentBranch", "ignore");
        return Commit(path, "Initial commit", files.ToDictionary(f => f.Key, f => (string?)f.Value, StringComparer.Ordinal));
    }

    /// <summary>Commits changes to <c>main</c> with Git, as another person would. A null content deletes the file.</summary>
    /// <returns>The new commit.</returns>
    public string Commit(string path, string message, IReadOnlyDictionary<string, string?> changes, string branch = "main")
    {
        var work = Path.Combine(_temp.Path, "work-" + Guid.NewGuid().ToString("N"));
        Git(_temp.Path, "clone", "--quiet", BarePath(path), work);
        Configure(work);
        if (Git(work, "branch", "--list", "-r", "origin/" + branch).Trim().Length > 0)
        {
            Git(work, "checkout", "--quiet", branch);
        }
        else
        {
            Git(work, "checkout", "--quiet", "-b", branch);
        }

        foreach (var (file, content) in changes)
        {
            var full = Path.Combine(work, file.Replace('/', Path.DirectorySeparatorChar));
            if (content is null)
            {
                Git(work, "rm", "--quiet", file);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            Git(work, "add", file);
        }

        Git(work, "commit", "--quiet", "--allow-empty", "-m", message);
        Git(work, "push", "--quiet", "origin", branch);
        var head = Git(work, "rev-parse", "HEAD").Trim();
        Directory.Delete(work, recursive: true);
        return head;
    }

    /// <summary>Moves a submodule's pin in a new commit on <c>main</c>.</summary>
    public string PinSubmodule(string path, string submodulePath, string commit)
    {
        var work = Path.Combine(_temp.Path, "work-" + Guid.NewGuid().ToString("N"));
        Git(_temp.Path, "clone", "--quiet", BarePath(path), work);
        Configure(work);
        Git(work, "update-index", "--add", "--cacheinfo", $"160000,{commit},{submodulePath}");
        Git(work, "commit", "--quiet", "-m", "Move " + submodulePath);
        Git(work, "push", "--quiet", "origin", "main");
        var head = Git(work, "rev-parse", "HEAD").Trim();
        Directory.Delete(work, recursive: true);
        return head;
    }

    /// <summary>The commit a branch points at in the server's repository.</summary>
    public string Head(string path, string branch = "main") => Git(BarePath(path), "rev-parse", "refs/heads/" + branch).Trim();

    /// <summary>The bare repository's folder.</summary>
    public string BarePath(string path) => Path.Combine(_temp.Path, path.Replace('/', Path.DirectorySeparatorChar) + ".git");

    /// <summary>Runs Git and returns what it printed.</summary>
    public static string Git(string workingDirectory, params string[] arguments) => Run(workingDirectory, null, null, arguments).Output;

    /// <summary>Sets a committer, as a person's Git has one.</summary>
    public static void Configure(string work)
    {
        Git(work, "config", "user.name", "Other Person");
        Git(work, "config", "user.email", "other@example.test");
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var path = uri.AbsolutePath.Trim('/');
        var marker = path.IndexOf(".git/", StringComparison.Ordinal);
        if (!string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase) || marker < 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var bare = BarePath(path[..marker]);
        if (!Directory.Exists(bare))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var action = path[(marker + 5)..];
        var protocol = !OnlyVersion0 && request.Headers.TryGetValues("Git-Protocol", out var values) ? values.FirstOrDefault() : null;
        var query = uri.Query;
        var service = query.Contains("service=git-receive-pack", StringComparison.Ordinal) || action == "git-receive-pack" ? "receive-pack" : "upload-pack";
        if (service == "receive-pack" && PushCredentials is { } needed)
        {
            var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{needed.User}:{needed.Password}"));
            if (request.Headers.Authorization is not { Scheme: "Basic" } authorization || authorization.Parameter != expected)
            {
                var refused = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                refused.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Basic", "realm=\"git\""));
                return refused;
            }
        }

        if (action == "info/refs" && request.Method == HttpMethod.Get)
        {
            var advertised = Run(bare, null, protocol, service, "--stateless-rpc", "--advertise-refs", ".").Bytes;
            using var body = new MemoryStream();
            if (protocol != "version=2" || service == "receive-pack")
            {
                var line = Encoding.ASCII.GetBytes($"# service=git-{service}\n");
                body.Write(Encoding.ASCII.GetBytes((line.Length + 4).ToString("x4", System.Globalization.CultureInfo.InvariantCulture)));
                body.Write(line);
                body.Write("0000"u8);
            }

            body.Write(advertised);
            return Answer(body.ToArray(), $"application/x-git-{service}-advertisement", null);
        }

        if (action == "git-" + service && request.Method == HttpMethod.Post)
        {
            var input = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            long? breakAfter = null;
            if (service == "upload-pack")
            {
                var text = Encoding.UTF8.GetString(input);
                if (text.Contains("command=fetch", StringComparison.Ordinal) || (protocol != "version=2" && text.Contains("want ", StringComparison.Ordinal)))
                {
                    var number = Interlocked.Increment(ref _fetches);
                    lock (_gate)
                    {
                        _wants.Add(text.Split('\n').Count(l => l.Length > 4 && l[4..].StartsWith("want ", StringComparison.Ordinal)));
                    }

                    if (BreakOnFetch is null || BreakOnFetch == number)
                    {
                        breakAfter = BreakFetchAfter;
                        BreakFetchAfter = null;
                    }
                }
            }

            var output = Run(bare, input, protocol, service, "--stateless-rpc", ".").Bytes;
            return Answer(output, $"application/x-git-{service}-result", breakAfter);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temp.Dispose();
        }

        base.Dispose(disposing);
    }

    private static HttpResponseMessage Answer(byte[] body, string type, long? breakAfter)
    {
        var answer = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = breakAfter is { } limit ? new StreamContent(new BrokenStream(body, limit)) : new ByteArrayContent(body),
        };
        answer.Content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return answer;
    }

    private static (string Output, byte[] Bytes) Run(string workingDirectory, byte[]? input, string? protocol, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        if (protocol is not null)
        {
            start.Environment["GIT_PROTOCOL"] = protocol;
        }

        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        if (input is not null)
        {
            process.StandardInput.BaseStream.Write(input);
            process.StandardInput.Close();
        }

        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed ({process.ExitCode}): {error.Result}");
        }

        var bytes = output.ToArray();
        return (Encoding.UTF8.GetString(bytes), bytes);
    }

    /// <summary>An answer that breaks off after a number of bytes.</summary>
    private sealed class BrokenStream(byte[] content, long limit) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => content.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= limit)
            {
                throw new IOException("The connection was reset.");
            }

            var read = (int)Math.Min(Math.Min(count, content.Length - _position), limit - _position);
            Array.Copy(content, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
