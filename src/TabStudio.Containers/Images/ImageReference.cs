using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace TabStudio.Containers.Images;

/// <summary>
/// A parsed image name such as <c>postgres:17-alpine</c>, <c>ghcr.io/owner/app:1.2</c> or
/// <c>quay.io/org/img@sha256:…</c>, normalized as Docker does: no registry means Docker Hub, and a one-part Docker
/// Hub name is in <c>library/</c>.
/// </summary>
/// <param name="Registry">The registry's host, and port if any, as written (<c>docker.io</c> for Docker Hub).</param>
/// <param name="Repository">The repository path, such as <c>library/postgres</c>.</param>
/// <param name="Tag">The tag; <c>latest</c> when neither a tag nor a digest is given.</param>
/// <param name="Digest">The pinned manifest digest, if any.</param>
public sealed record ImageReference(string Registry, string Repository, string? Tag, string? Digest)
{
    private static readonly SearchValues<char> DigestCharacters = SearchValues.Create("0123456789abcdef");
    /// <summary>The registry name Docker uses for Docker Hub.</summary>
    public const string DockerHub = "docker.io";

    /// <summary>The host that serves Docker Hub's registry API.</summary>
    public string ApiHost => Registry is DockerHub or "index.docker.io" ? "registry-1.docker.io" : Registry;

    /// <summary>The tag or digest the registry is asked for.</summary>
    public string ManifestReference => Digest ?? Tag ?? "latest";

    /// <summary>Whether the registry is served over plain HTTP: a loopback registry, as Docker allows.</summary>
    public bool IsInsecure => Uri.TryCreate($"https://{Registry}", UriKind.Absolute, out var address) && address.IsLoopback;

    /// <summary>The fully qualified name, such as <c>docker.io/library/postgres:17-alpine</c>.</summary>
    public string Canonical => $"{Registry}/{Repository}{(Tag is null ? "" : ":" + Tag)}{(Digest is null ? "" : "@" + Digest)}";

    /// <summary>The name without tag or digest, as people write it: <c>postgres</c>, <c>ghcr.io/owner/app</c>.</summary>
    public string Name => Registry == DockerHub
        ? Repository.StartsWith("library/", StringComparison.Ordinal) ? Repository["library/".Length..] : Repository
        : $"{Registry}/{Repository}";

    /// <summary>The short name people write, such as <c>postgres:17-alpine</c>.</summary>
    public string Familiar => $"{Name}{(Tag is null ? "" : ":" + Tag)}{(Digest is null ? "" : "@" + Digest)}";

    /// <summary>This name with another tag and no digest.</summary>
    public ImageReference WithTag(string tag) => this with { Tag = tag, Digest = null };

    /// <inheritdoc />
    public override string ToString() => Familiar;

    /// <summary>Parses an image name.</summary>
    /// <exception cref="FormatException">It is not a valid image name.</exception>
    public static ImageReference Parse(string text) =>
        TryParse(text, out var reference, out var error) ? reference : throw new FormatException(error);

    /// <summary>Parses an image name, or says why it is not one.</summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out ImageReference? reference, [NotNullWhen(false)] out string? error)
    {
        reference = null;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            error = "An image name is empty.";
            return false;
        }

        string? digest = null;
        var at = text.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0)
        {
            digest = text[(at + 1)..];
            text = text[..at];
            if (!IsDigest(digest))
            {
                error = $"'{digest}' is not an image digest (sha256:<64 hex digits>).";
                return false;
            }
        }

        var slash = text.IndexOf('/', StringComparison.Ordinal);
        string registry;
        string rest;
        if (slash > 0 && (text[..slash].Contains('.', StringComparison.Ordinal) || text[..slash].Contains(':', StringComparison.Ordinal) || text[..slash] == "localhost"))
        {
            registry = text[..slash];
            rest = text[(slash + 1)..];
        }
        else
        {
            registry = DockerHub;
            rest = text;
        }

        if (registry == "index.docker.io")
        {
            registry = DockerHub;
        }

        string? tag = null;
        var lastSlash = rest.LastIndexOf('/');
        var colon = rest.LastIndexOf(':');
        if (colon > lastSlash)
        {
            tag = rest[(colon + 1)..];
            rest = rest[..colon];
            if (tag.Length is 0 or > 128 || !tag.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-') || tag[0] is '.' or '-')
            {
                error = $"'{tag}' is not a valid image tag.";
                return false;
            }
        }

        if (rest.Length == 0 || rest.Split('/').Any(part => part.Length == 0 || !part.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_' or '-')))
        {
            error = $"'{text}' is not a valid image name: names use lowercase letters, digits and . _ - separated by /.";
            return false;
        }

        if (registry == DockerHub && !rest.Contains('/', StringComparison.Ordinal))
        {
            rest = "library/" + rest;
        }

        reference = new ImageReference(registry, rest, tag ?? (digest is null ? "latest" : null), digest);
        error = null;
        return true;
    }

    /// <summary>Whether a string is a SHA-256 content digest.</summary>
    public static bool IsDigest(string? text) =>
        text is { Length: 71 } && text.StartsWith("sha256:", StringComparison.Ordinal) && text.AsSpan(7).ContainsAnyExcept(DigestCharacters) is false;
}

