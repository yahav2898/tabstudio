using System.Text.Json.Serialization;

namespace TabStudio.Live.Files;

/// <summary>A file of a shared workspace: its path relative to the workspace's folder, its length and its SHA-256.</summary>
/// <param name="Path">The path, with '/' between folders.</param>
/// <param name="Length">The length in bytes.</param>
/// <param name="Hash">The SHA-256 of its bytes, in hexadecimal.</param>
public sealed record LiveFileEntry(
    [property: JsonPropertyName("p")] string Path,
    [property: JsonPropertyName("n")] long Length,
    [property: JsonPropertyName("h")] string Hash);
