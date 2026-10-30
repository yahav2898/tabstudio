using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabStudio.App.Services;
using TabStudio.SourceControl;

namespace TabStudio.App.Tests;

/// <summary>
/// An in-memory GitHub: repositories with blobs, trees, commits and branches, served through the REST
/// endpoints TabStudio uses, to an <see cref="HttpClient"/> without a network. A copy of the source control
/// tests' fake, with helpers for the IDE's GitHub account.
/// </summary>
/// <remarks>
/// Blob hashes are real Git hashes. Tree and commit hashes are made up but stable, which is all the client
/// relies on.
/// </remarks>
internal sealed partial class FakeGitHub : HttpMessageHandler
{
    public const string Login = "tester";

    /// <summary>The token the fake accepts.</summary>
    public const string Token = "test-token";

    /// <summary>The fake's API address.</summary>
    public static readonly Uri ApiAddress = new("https://api.github.test/");

    /// <summary>The app's GitHub account, talking to this fake.</summary>
    public GitHubAccount CreateAccount(AppPaths paths) => new(paths, new HttpClient(this, disposeHandler: false), ApiAddress);

    private readonly ConcurrentDictionary<string, Repo> _repos = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _requests = new();
    private readonly Lock _gate = new();
    private int _counter;

    /// <summary>Every request as "METHOD path?query", in order.</summary>
    public IReadOnlyList<string> Requests => [.. _requests];

    /// <summary>When set, recursive tree reads are reported as truncated, as GitHub does for huge trees.</summary>
    public bool TruncateRecursiveTrees { get; set; }

    /// <summary>Paths the zip archive leaves out, like files with the export-ignore attribute.</summary>
    public HashSet<string> ArchiveOmits { get; } = new(StringComparer.Ordinal);

    /// <summary>Paths whose content the zip archive changes, like files with the export-subst attribute.</summary>
    public HashSet<string> ArchiveAlters { get; } = new(StringComparer.Ordinal);

    /// <summary>Called before a branch moves; lets a test push a competing commit at the worst moment.</summary>
    public Action? BeforeBranchUpdate { get; set; }

    /// <summary>Awaited before each request is answered; lets a test act while an operation is half done.</summary>
    public Func<Task>? BeforeRequest { get; set; }

    /// <summary>A client of this fake, signed in or not.</summary>
    public SourceControl.GitHub.GitHubClient CreateClient(bool signedIn = true) =>
        new(new HttpClient(this, disposeHandler: false), signedIn ? Token : null, ApiAddress);

    /// <summary>Creates a repository whose default branch holds the files.</summary>
    public FakeGitHub AddRepository(string name, IReadOnlyDictionary<string, string> files, string defaultBranch = "main", bool isPrivate = false)
    {
        var repo = new Repo(Login, name, defaultBranch, isPrivate);
        _repos[$"{Login}/{name}"] = repo;
        var tree = StoreTree(repo, files.ToDictionary(f => f.Key, f => (Mode: "100644", Sha: StoreBlob(repo, Encoding.UTF8.GetBytes(f.Value))), StringComparer.Ordinal));
        repo.Branches[defaultBranch] = StoreCommit(repo, tree, "Initial commit", []);
        return this;
    }

    /// <summary>Pushes a commit to a branch, as another person would. A null content deletes the file.</summary>
    public string Push(string name, string branch, string message, IReadOnlyDictionary<string, string?> changes)
    {
        lock (_gate)
        {
            var repo = _repos[$"{Login}/{name}"];
            var head = repo.Branches[branch];
            var files = new Dictionary<string, (string Mode, string Sha)>(repo.Trees[repo.Commits[head].Tree], StringComparer.Ordinal);
            foreach (var (path, content) in changes)
            {
                if (content is null)
                {
                    files.Remove(path);
                }
                else
                {
                    files[path] = ("100644", StoreBlob(repo, Encoding.UTF8.GetBytes(content)));
                }
            }

            var commit = StoreCommit(repo, StoreTree(repo, files), message, [head]);
            repo.Branches[branch] = commit;
            return commit;
        }
    }

    /// <summary>Creates a tag-like extra branch-less commit name: returns the commit at the tip of a branch.</summary>
    public string Head(string name, string branch) => _repos[$"{Login}/{name}"].Branches[branch];

    /// <summary>Whether a branch exists.</summary>
    public bool HasBranch(string name, string branch) => _repos[$"{Login}/{name}"].Branches.ContainsKey(branch);

    /// <summary>The files at the tip of a branch, with their text.</summary>
    public IReadOnlyDictionary<string, string> Files(string name, string branch)
    {
        var repo = _repos[$"{Login}/{name}"];
        return repo.Trees[repo.Commits[repo.Branches[branch]].Tree]
            .ToDictionary(e => e.Key, e => Encoding.UTF8.GetString(repo.Blobs[e.Value.Sha]), StringComparer.Ordinal);
    }

    /// <summary>The message of the commit at the tip of a branch.</summary>
    public string HeadMessage(string name, string branch)
    {
        var repo = _repos[$"{Login}/{name}"];
        return repo.Commits[repo.Branches[branch]].Message;
    }

    /// <summary>The parents of the commit at the tip of a branch.</summary>
    public IReadOnlyList<string> HeadParents(string name, string branch)
    {
        var repo = _repos[$"{Login}/{name}"];
        return repo.Commits[repo.Branches[branch]].Parents;
    }

    /// <summary>The pull requests opened in a repository.</summary>
    public IReadOnlyList<(string Title, string Head, string Base)> PullRequests(string name) =>
        [.. _repos[$"{Login}/{name}"].PullRequests.Select(p => (p.Title, p.Head, p.Base))];

    /// <summary>Whether a repository exists.</summary>
    public bool HasRepository(string name) => _repos.ContainsKey($"{Login}/{name}");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var path = string.Join('/', uri.AbsolutePath.Trim('/').Split('/').Select(Uri.UnescapeDataString));
        _requests.Enqueue($"{request.Method} {path}{uri.Query}");
        if (BeforeRequest is { } beforeRequest)
        {
            await beforeRequest().ConfigureAwait(false);
        }

        if (HandleDownload(request) is { } download)
        {
            return download;
        }

        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (request.Headers.Authorization is { } authorization && authorization.Parameter != Token)
        {
            return Error(HttpStatusCode.Unauthorized, "Bad credentials");
        }

        var signedIn = request.Headers.Authorization is not null;
        HttpResponseMessage response;
        lock (_gate)
        {
            response = Handle(request.Method.Method, path, Query(uri), body, signedIn);
        }

        return await ApplyETagAsync(request, response).ConfigureAwait(false);
    }

    private HttpResponseMessage Handle(string method, string path, Dictionary<string, string> query, JsonNode? body, bool signedIn)
    {
        if (method != "GET" && !signedIn)
        {
            return Error(HttpStatusCode.Unauthorized, "Requires authentication");
        }

        if (path == "user")
        {
            return signedIn ? Json(new JsonObject { ["login"] = Login, ["name"] = "Test User" }) : Error(HttpStatusCode.Unauthorized, "Requires authentication");
        }

        if (path == "user/repos" && method == "POST")
        {
            var name = body!["name"]!.GetValue<string>();
            if (HasRepository(name))
            {
                return Error(HttpStatusCode.UnprocessableEntity, "name already exists on this account");
            }

            AddRepository(name, new Dictionary<string, string> { ["README.md"] = $"# {name}\n" }, isPrivate: body["private"]!.GetValue<bool>());
            return Json(RepositoryJson(_repos[$"{Login}/{name}"]), HttpStatusCode.Created);
        }

        if (HandleOutsideRepositories(method, path, query, body) is { } answer)
        {
            return answer;
        }

        var parts = path.Split('/');
        if (parts.Length < 3 || parts[0] != "repos" || !_repos.TryGetValue($"{parts[1]}/{parts[2]}", out var repo))
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        if (repo.IsPrivate && !signedIn)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var rest = string.Join('/', parts.Skip(3));
        if (HandleActions(method, rest, query, body, repo) is { } actions)
        {
            return actions;
        }

        return (method, rest) switch
        {
            ("GET", "") => Json(WithMergeSettings(repo, RepositoryJson(repo))),
            ("GET", "branches") => Json(new JsonArray([.. repo.Branches.OrderBy(b => b.Key, StringComparer.Ordinal).Select(b => (JsonNode)new JsonObject
            {
                ["name"] = b.Key,
                ["commit"] = new JsonObject { ["sha"] = b.Value },
                ["protected"] = false,
            })])),
            ("GET", _) when rest.StartsWith("git/ref/heads/", StringComparison.Ordinal) => GetRef(repo, rest["git/ref/heads/".Length..]),
            ("PATCH", _) when rest.StartsWith("git/refs/heads/", StringComparison.Ordinal) => UpdateRef(repo, rest["git/refs/heads/".Length..], body!),
            ("POST", "git/refs") => CreateRef(repo, body!),
            ("GET", _) when rest.StartsWith("git/commits/", StringComparison.Ordinal) => GitCommit(repo, rest["git/commits/".Length..]),
            ("POST", "git/commits") => CreateCommit(repo, body!),
            ("GET", _) when rest.StartsWith("git/trees/", StringComparison.Ordinal) => GetTree(repo, rest["git/trees/".Length..], query.ContainsKey("recursive")),
            ("POST", "git/trees") => CreateTree(repo, body!),
            ("GET", _) when rest.StartsWith("git/blobs/", StringComparison.Ordinal) => GetBlob(repo, rest["git/blobs/".Length..]),
            ("POST", "git/blobs") => Json(new JsonObject { ["sha"] = StoreBlob(repo, Convert.FromBase64String(body!["content"]!.GetValue<string>())) }, HttpStatusCode.Created),
            ("GET", "commits") => History(repo, query["sha"], int.Parse(query["per_page"], System.Globalization.CultureInfo.InvariantCulture)),
            ("GET", _) when rest.StartsWith("commits/", StringComparison.Ordinal) => ResolveCommit(repo, rest["commits/".Length..]),
            ("GET", _) when rest.StartsWith("compare/", StringComparison.Ordinal) => Compare(repo, rest["compare/".Length..]),
            ("GET", _) when rest.StartsWith("zipball/", StringComparison.Ordinal) => Archive(repo, rest["zipball/".Length..]),
            ("POST", "pulls") => CreatePullRequest(repo, body!),
            ("GET", "pulls") => ListPullRequests(repo, query),
            _ => HandlePullRequests(repo, method, rest, query, body) ?? Error(HttpStatusCode.NotFound, "Not Found"),
        };
    }

    private static HttpResponseMessage GetRef(Repo repo, string branch) =>
        repo.Branches.TryGetValue(branch, out var sha)
            ? Json(RefJson(branch, sha))
            : Error(HttpStatusCode.NotFound, "Not Found");

    private HttpResponseMessage UpdateRef(Repo repo, string branch, JsonNode body)
    {
        BeforeBranchUpdate?.Invoke();
        BeforeBranchUpdate = null;
        if (!repo.Branches.TryGetValue(branch, out var current))
        {
            return Error(HttpStatusCode.UnprocessableEntity, "Reference does not exist");
        }

        var sha = body["sha"]!.GetValue<string>();
        var force = body["force"]?.GetValue<bool>() ?? false;
        if (!force && !Ancestors(repo, sha).Contains(current))
        {
            return Error(HttpStatusCode.UnprocessableEntity, "Update is not a fast forward");
        }

        repo.Branches[branch] = sha;
        return Json(RefJson(branch, sha));
    }

    private static HttpResponseMessage CreateRef(Repo repo, JsonNode body)
    {
        var name = body["ref"]!.GetValue<string>()["refs/heads/".Length..];
        if (repo.Branches.ContainsKey(name))
        {
            return Error(HttpStatusCode.UnprocessableEntity, "Reference already exists");
        }

        repo.Branches[name] = body["sha"]!.GetValue<string>();
        return Json(RefJson(name, repo.Branches[name]), HttpStatusCode.Created);
    }

    private static HttpResponseMessage GitCommit(Repo repo, string sha) =>
        repo.Commits.TryGetValue(sha, out var commit) ? Json(GitCommitJson(commit)) : Error(HttpStatusCode.NotFound, "Not Found");

    private HttpResponseMessage CreateCommit(Repo repo, JsonNode body)
    {
        var tree = body["tree"]!.GetValue<string>();
        var parents = body["parents"]!.AsArray().Select(p => p!.GetValue<string>()).ToList();
        if (!repo.Trees.ContainsKey(tree) || parents.Any(p => !repo.Commits.ContainsKey(p)))
        {
            return Error(HttpStatusCode.UnprocessableEntity, "Tree or parent SHA does not exist");
        }

        var sha = StoreCommit(repo, tree, body["message"]!.GetValue<string>(), parents);
        return Json(GitCommitJson(repo.Commits[sha]), HttpStatusCode.Created);
    }

    private HttpResponseMessage GetTree(Repo repo, string sha, bool recursive)
    {
        if (!repo.Trees.TryGetValue(sha, out var files))
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var entries = new JsonArray();
        if (recursive)
        {
            var folders = files.Keys.SelectMany(FoldersOf).Distinct(StringComparer.Ordinal);
            foreach (var folder in folders)
            {
                entries.Add(new JsonObject { ["path"] = folder, ["mode"] = "040000", ["type"] = "tree", ["sha"] = SubtreeSha(repo, files, folder) });
            }

            foreach (var (path, file) in files.Take(TruncateRecursiveTrees ? files.Count / 2 : files.Count))
            {
                entries.Add(EntryJson(repo, path, file));
            }
        }
        else
        {
            foreach (var group in files.GroupBy(f => f.Key.Split('/')[0], StringComparer.Ordinal))
            {
                if (group.Any(f => f.Key.Contains('/', StringComparison.Ordinal)))
                {
                    entries.Add(new JsonObject { ["path"] = group.Key, ["mode"] = "040000", ["type"] = "tree", ["sha"] = SubtreeSha(repo, files, group.Key) });
                }
                else
                {
                    var file = group.Single();
                    entries.Add(EntryJson(repo, file.Key, file.Value));
                }
            }
        }

        return Json(new JsonObject { ["sha"] = sha, ["tree"] = entries, ["truncated"] = recursive && TruncateRecursiveTrees });
    }

    private static HttpResponseMessage CreateTree(Repo repo, JsonNode body)
    {
        var baseTree = body["base_tree"]?.GetValue<string>();
        var files = baseTree is null
            ? new Dictionary<string, (string Mode, string Sha)>(StringComparer.Ordinal)
            : new Dictionary<string, (string Mode, string Sha)>(repo.Trees[baseTree], StringComparer.Ordinal);
        foreach (var entry in body["tree"]!.AsArray())
        {
            var path = entry!["path"]!.GetValue<string>();
            var sha = entry["sha"]?.GetValue<string>();
            if (sha is null)
            {
                files.Remove(path);
            }
            else if (!repo.Blobs.ContainsKey(sha))
            {
                return Error(HttpStatusCode.UnprocessableEntity, "BadObjectState");
            }
            else
            {
                files[path] = (entry["mode"]!.GetValue<string>(), sha);
            }
        }

        return Json(new JsonObject { ["sha"] = StoreTree(repo, files) }, HttpStatusCode.Created);
    }

    private static HttpResponseMessage GetBlob(Repo repo, string sha) =>
        repo.Blobs.TryGetValue(sha, out var content)
            ? Json(new JsonObject { ["sha"] = sha, ["content"] = Convert.ToBase64String(content), ["encoding"] = "base64", ["size"] = content.Length })
            : Error(HttpStatusCode.NotFound, "Not Found");

    private static HttpResponseMessage History(Repo repo, string reference, int count)
    {
        var list = new JsonArray();
        for (var sha = Resolve(repo, reference); sha is not null && list.Count < count; sha = repo.Commits[sha].Parents is [var parent, ..] ? parent : null)
        {
            list.Add(RepositoryCommitJson(repo.Commits[sha]));
        }

        return Json(list);
    }

    private static HttpResponseMessage ResolveCommit(Repo repo, string reference) =>
        Resolve(repo, reference) is { } sha ? Json(RepositoryCommitJson(repo.Commits[sha])) : Error(HttpStatusCode.UnprocessableEntity, $"No commit found for SHA: {reference}");

    private static HttpResponseMessage Compare(Repo repo, string range)
    {
        var ends = range.Split("...");
        var baseSha = Resolve(repo, ends[0]);
        var headSha = Resolve(repo, ends[1]);
        if (baseSha is null || headSha is null)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var baseAncestors = Ancestors(repo, baseSha);
        var headAncestors = Ancestors(repo, headSha);
        var ahead = headAncestors.Count(c => !baseAncestors.Contains(c));
        var behind = baseAncestors.Count(c => !headAncestors.Contains(c));
        var status = (ahead, behind) switch
        {
            (0, 0) => "identical",
            (_, 0) => "ahead",
            (0, _) => "behind",
            _ => "diverged",
        };
        return Json(new JsonObject { ["status"] = status, ["ahead_by"] = ahead, ["behind_by"] = behind, ["merge_base_commit"] = new JsonObject { ["sha"] = MergeBase(repo, baseSha, headSha) } });
    }

    private HttpResponseMessage Archive(Repo repo, string reference)
    {
        if (Resolve(repo, reference) is not { } sha)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var top = $"{repo.Owner}-{repo.Name}-{sha[..7]}/";
            zip.CreateEntry(top);
            foreach (var (path, file) in repo.Trees[repo.Commits[sha].Tree])
            {
                if (ArchiveOmits.Contains(path))
                {
                    continue;
                }

                var entry = zip.CreateEntry(top + path);
                using var output = entry.Open();
                var content = repo.Blobs[file.Sha];
                output.Write(ArchiveAlters.Contains(path) ? [.. content, .. "// substituted\n"u8] : content);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(stream.ToArray()) };
    }

    private static string StoreBlob(Repo repo, byte[] content)
    {
        var sha = GitObjects.BlobHash(content);
        repo.Blobs[sha] = content;
        return sha;
    }

    private static string StoreTree(Repo repo, Dictionary<string, (string Mode, string Sha)> files)
    {
        var text = string.Join('\n', files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Value.Mode} {f.Key} {f.Value.Sha}"));
        var sha = Hash("tree\n" + text);
        repo.Trees[sha] = files;
        return sha;
    }

    private string StoreCommit(Repo repo, string tree, string message, IReadOnlyList<string> parents)
    {
        var sha = Hash($"commit {tree} {string.Join(',', parents)} {message} {Interlocked.Increment(ref _counter)}");
        repo.Commits[sha] = new Commit(sha, tree, message, parents);
        return sha;
    }

    private static string SubtreeSha(Repo repo, Dictionary<string, (string Mode, string Sha)> files, string folder)
    {
        var prefix = folder + "/";
        return StoreTree(repo, files.Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal)).ToDictionary(f => f.Key[prefix.Length..], f => f.Value, StringComparer.Ordinal));
    }

    private static IEnumerable<string> FoldersOf(string path)
    {
        var parts = path.Split('/');
        for (var i = 1; i < parts.Length; i++)
        {
            yield return string.Join('/', parts.Take(i));
        }
    }

    private static string? Resolve(Repo repo, string reference)
    {
        if (repo.Branches.TryGetValue(reference, out var branch))
        {
            return branch;
        }

        var matches = repo.Commits.Keys.Where(k => k.StartsWith(reference, StringComparison.Ordinal)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static HashSet<string> Ancestors(Repo repo, string sha)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([sha]);
        while (pending.TryPop(out var next))
        {
            if (seen.Add(next) && repo.Commits.TryGetValue(next, out var commit))
            {
                foreach (var parent in commit.Parents)
                {
                    pending.Push(parent);
                }
            }
        }

        return seen;
    }

    private static JsonObject RepositoryJson(Repo repo) => new()
    {
        ["name"] = repo.Name,
        ["full_name"] = $"{repo.Owner}/{repo.Name}",
        ["owner"] = new JsonObject { ["login"] = repo.Owner },
        ["default_branch"] = repo.DefaultBranch,
        ["private"] = repo.IsPrivate,
        ["html_url"] = $"https://github.test/{repo.Owner}/{repo.Name}",
        ["permissions"] = new JsonObject { ["push"] = true },
    };

    private static JsonObject RefJson(string branch, string sha) => new()
    {
        ["ref"] = $"refs/heads/{branch}",
        ["object"] = new JsonObject { ["sha"] = sha, ["type"] = "commit" },
    };

    private static JsonObject EntryJson(Repo repo, string path, (string Mode, string Sha) file) => new()
    {
        ["path"] = path,
        ["mode"] = file.Mode,
        ["type"] = "blob",
        ["sha"] = file.Sha,
        ["size"] = repo.Blobs[file.Sha].Length,
    };

    private static JsonObject Author() => new() { ["name"] = "Test User", ["email"] = "tester@example.test", ["date"] = "2026-10-25T08:00:00Z" };

    private static JsonObject GitCommitJson(Commit commit) => new()
    {
        ["sha"] = commit.Sha,
        ["tree"] = new JsonObject { ["sha"] = commit.Tree },
        ["message"] = commit.Message,
        ["parents"] = new JsonArray([.. commit.Parents.Select(p => (JsonNode)new JsonObject { ["sha"] = p })]),
        ["author"] = Author(),
    };

    private static JsonObject RepositoryCommitJson(Commit commit) => new()
    {
        ["sha"] = commit.Sha,
        ["commit"] = new JsonObject { ["tree"] = new JsonObject { ["sha"] = commit.Tree }, ["message"] = commit.Message, ["author"] = Author() },
        ["parents"] = new JsonArray([.. commit.Parents.Select(p => (JsonNode)new JsonObject { ["sha"] = p })]),
    };

    private static Dictionary<string, string> Query(Uri uri) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : string.Empty, StringComparer.Ordinal);

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..40];

    private static HttpResponseMessage Json(JsonNode node, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode status, string message) =>
        Json(new JsonObject { ["message"] = message, ["documentation_url"] = "https://docs.github.com/rest" }, status);

    private sealed record Commit(string Sha, string Tree, string Message, IReadOnlyList<string> Parents);

    private sealed class Repo(string owner, string name, string defaultBranch, bool isPrivate)
    {
        public string Owner { get; } = owner;

        public string Name { get; } = name;

        public string DefaultBranch { get; } = defaultBranch;

        public bool IsPrivate { get; } = isPrivate;

        public Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Dictionary<string, (string Mode, string Sha)>> Trees { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Commit> Commits { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Branches { get; } = new(StringComparer.Ordinal);

        public List<PullRequest> PullRequests { get; } = [];

        public MergeSettingsRecord Settings { get; set; } = new(AllowMerge: true, AllowSquash: true, AllowRebase: true, DeleteBranchOnMerge: false);
    }
}
