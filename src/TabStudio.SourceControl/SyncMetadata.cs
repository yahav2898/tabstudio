using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.SourceControl;

/// <summary>The GitHub repository and branch a folder is linked to, and the commit it was last synced with.</summary>
/// <param name="Owner">The repository owner.</param>
/// <param name="Repository">The repository name.</param>
/// <param name="Branch">The branch the folder follows, or <see langword="null"/> when it holds a tag or commit instead.</param>
/// <param name="BaseCommit">The commit the folder's files were last synced with.</param>
public sealed record GitHubLink(string Owner, string Repository, string? Branch, string BaseCommit)
{
    /// <summary>Whether the folder holds a tag or commit rather than following a branch.</summary>
    public bool IsDetached => Branch is null;

    /// <summary>"owner/repository".</summary>
    public string FullName => $"{Owner}/{Repository}";

    /// <summary>The repository's web page.</summary>
    public Uri WebUri => new($"https://github.com/{Owner}/{Repository}");
}

/// <summary>A file in the last synced commit.</summary>
/// <param name="Sha">Its Git blob hash.</param>
/// <param name="Mode">Its Git file mode, such as <c>100644</c>.</param>
public sealed record TrackedFile(string Sha, string Mode);

/// <summary>
/// What TabStudio remembers about a linked folder, stored in <c>.tabstudio/sync.json</c> inside it: the link,
/// and every file of the last synced commit with its hash.
/// </summary>
/// <param name="Link">The repository and branch.</param>
/// <param name="Files">The files of <see cref="GitHubLink.BaseCommit"/>, by path relative to the root with <c>/</c> separators.</param>
public sealed record SyncMetadata(GitHubLink Link, IReadOnlyDictionary<string, TrackedFile> Files)
{
    /// <summary>Files a pull could not merge, until they are committed, discarded or marked as resolved.</summary>
    public IReadOnlyList<string> Conflicts { get; init; } = [];

    /// <summary>The folder inside a linked folder where TabStudio keeps its sync data. Never committed.</summary>
    public const string FolderName = ".tabstudio";

    private const string FileName = "sync.json";

    /// <summary>Whether a folder is linked to GitHub.</summary>
    /// <param name="root">The folder.</param>
    public static bool Exists(string root) => File.Exists(PathFor(root));

    /// <summary>Reads the metadata of a linked folder.</summary>
    /// <param name="root">The folder.</param>
    /// <returns>The metadata, or <see langword="null"/> when the folder is not linked or the file is damaged.</returns>
    public static SyncMetadata? Load(string root)
    {
        try
        {
            using var stream = File.OpenRead(PathFor(root));
            return JsonSerializer.Deserialize(stream, SyncJsonContext.Default.SyncMetadata);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Writes the metadata atomically.</summary>
    /// <param name="root">The linked folder.</param>
    public void Save(string root)
    {
        var path = PathFor(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        using (var stream = File.Create(temporary))
        {
            JsonSerializer.Serialize(stream, this, SyncJsonContext.Default.SyncMetadata);
        }

        File.Move(temporary, path, overwrite: true);
    }

    private static string PathFor(string root) => Path.Combine(root, FolderName, FileName);
}

/// <summary>Source-generated JSON for the sync metadata.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SyncMetadata))]
internal sealed partial class SyncJsonContext : JsonSerializerContext
{
}
