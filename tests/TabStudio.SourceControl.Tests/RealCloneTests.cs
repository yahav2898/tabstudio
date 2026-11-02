using System.Security.Cryptography;
using System.Text.Json.Nodes;
using TabStudio.SourceControl.GitHub;
using TabStudio.Testing;

namespace TabStudio.SourceControl.Tests;

/// <summary>
/// TabStudio's own download of real public repositories, pinned in <c>build/ci/real-repos.json</c>
/// (<c>cloneRepositories</c> and <c>lfsObjects</c>), from github.com: submodules arrive at their pinned commits and Git
/// LFS objects come from GitHub's Git LFS server. They run when <c>TABSTUDIO_REAL_CLONES=1</c>, as the libraries job
/// sets, with <c>TABSTUDIO_GITHUB_TOKEN</c> for the API's request limit.
/// </summary>
public sealed class RealCloneTests
{
    private static bool IsEnabled => Environment.GetEnvironmentVariable("TABSTUDIO_REAL_CLONES") == "1";

    [Fact]
    public async Task Pinned_repositories_download_with_their_submodules()
    {
        if (!IsEnabled)
        {
            return;
        }

        using var http = new HttpClient();
        var client = new GitHubClient(http, Environment.GetEnvironmentVariable("TABSTUDIO_GITHUB_TOKEN"));
        foreach (var repository in Manifest()["cloneRepositories"]!.AsArray())
        {
            using var temp = new TempDirectory();
            var folder = Path.Combine(temp.Path, repository!["name"]!.GetValue<string>());
            var sync = await GitHubSync.CloneAsync(
                client,
                repository["owner"]!.GetValue<string>(),
                repository["repository"]!.GetValue<string>(),
                repository["sha"]!.GetValue<string>(),
                folder);

            foreach (var path in repository["expect"]!.AsArray().Select(p => p!.GetValue<string>()))
            {
                Assert.True(File.Exists(Path.Combine(folder, path)), $"{path} did not arrive. Notes: {string.Join(" | ", sync.CloneNotes)}");
            }

            foreach (var (path, sha) in repository["submodules"]?.AsObject() ?? new JsonObject())
            {
                Assert.Equal(sha!.GetValue<string>(), SyncMetadata.Load(Path.Combine(folder, path))?.Link.BaseCommit);
            }

            Assert.Empty((await sync.GetStatusAsync()).Changes);
        }
    }

    [Fact]
    public async Task Git_LFS_objects_of_public_repositories_download_without_a_token()
    {
        if (!IsEnabled)
        {
            return;
        }

        using var http = new HttpClient();
        var client = new GitHubClient(http);
        foreach (var item in Manifest()["lfsObjects"]!.AsArray())
        {
            var oid = item!["oid"]!.GetValue<string>();
            var size = item["size"]!.GetValue<long>();
            var links = await client.GetLfsDownloadLinksAsync(item["owner"]!.GetValue<string>(), item["repository"]!.GetValue<string>(), [(oid, size)]);
            var link = Assert.Single(links);
            Assert.Null(link.Error);

            using var content = new MemoryStream();
            await client.DownloadLfsObjectAsync(link, content);
            Assert.Equal(size, content.Length);
            Assert.Equal(oid, Convert.ToHexStringLower(SHA256.HashData(content.ToArray())));
        }
    }

    private static JsonNode Manifest()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, "build", "ci", "real-repos.json");
            if (File.Exists(path))
            {
                return JsonNode.Parse(File.ReadAllText(path))!;
            }
        }

        throw new InvalidOperationException("build/ci/real-repos.json was not found above the test.");
    }
}
