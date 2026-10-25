namespace TabStudio.Toolchains.Catalog;

/// <summary>
/// The Linux distribution toolchains are installed into on Android: Alpine Linux, pinned to one release, with
/// the root file system to start from for each processor architecture.
/// </summary>
public sealed record LinuxDistribution
{
    /// <summary>The distribution's name, such as <c>Alpine Linux</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The release, such as <c>3.24.3</c>.</summary>
    public required string Version { get; init; }

    /// <summary>The package repositories' branch, such as <c>v3.24</c>.</summary>
    public required string Branch { get; init; }

    /// <summary>The mirror's base address, such as <c>https://dl-cdn.alpinelinux.org/alpine</c>.</summary>
    public required Uri Mirror { get; init; }

    /// <summary>The repositories packages come from, such as <c>main</c> and <c>community</c>.</summary>
    public IReadOnlyList<string> Repositories { get; init; } = [];

    /// <summary>The minimal root file system for each architecture, by Alpine's architecture name.</summary>
    public IReadOnlyDictionary<string, RootfsImage> Rootfs { get; init; } = new Dictionary<string, RootfsImage>();

    /// <summary>The display name with the release, such as <c>Alpine Linux 3.24</c>.</summary>
    public string DisplayName => $"{Name} {Branch.TrimStart('v')}";

    /// <summary>The address of a repository's folder for an architecture, which holds its index and packages.</summary>
    /// <param name="repository">The repository, such as <c>main</c>.</param>
    /// <param name="architecture">The architecture, such as <c>aarch64</c>.</param>
    public Uri RepositoryAddress(string repository, string architecture) =>
        new($"{Mirror.AbsoluteUri.TrimEnd('/')}/{Branch}/{repository}/{architecture}/");

    /// <summary>The address of a root file system image.</summary>
    /// <param name="architecture">The architecture.</param>
    /// <param name="image">The image.</param>
    public Uri RootfsAddress(string architecture, RootfsImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new($"{Mirror.AbsoluteUri.TrimEnd('/')}/{Branch}/releases/{architecture}/{image.File}");
    }
}

/// <summary>A minimal root file system archive, pinned by its hash.</summary>
public sealed record RootfsImage
{
    /// <summary>The file name, such as <c>alpine-minirootfs-3.24.3-aarch64.tar.gz</c>.</summary>
    public required string File { get; init; }

    /// <summary>Its SHA-256, in hexadecimal.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Its size in bytes.</summary>
    public long Size { get; init; }
}
