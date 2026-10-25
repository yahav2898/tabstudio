namespace TabStudio.Extensions.Installation;

/// <summary>Where an installed extension came from.</summary>
public enum ExtensionOrigin
{
    /// <summary>Downloaded from an Open VSX registry.</summary>
    Registry,

    /// <summary>Installed from a .vsix file the user picked.</summary>
    File,

    /// <summary>Shipped inside TabStudio.</summary>
    Bundled,
}

/// <summary>Where an installed extension came from, and what was verified when it was installed.</summary>
/// <param name="Origin">The kind of source.</param>
/// <param name="Registry">The registry it was downloaded from, for <see cref="ExtensionOrigin.Registry"/>.</param>
/// <param name="FileName">The .vsix file's name, for <see cref="ExtensionOrigin.File"/>.</param>
/// <param name="Sha256">The package's SHA-256 hash, in lowercase hexadecimal.</param>
/// <param name="Verification">What was checked about the package before it was installed.</param>
public sealed record ExtensionSource(
    ExtensionOrigin Origin,
    Uri? Registry = null,
    string? FileName = null,
    string? Sha256 = null,
    PackageVerification Verification = PackageVerification.None);

/// <summary>What was checked about a package before it was installed.</summary>
public enum PackageVerification
{
    /// <summary>Nothing could be checked, as for a local file.</summary>
    None,

    /// <summary>Its SHA-256 hash matched the one the registry published.</summary>
    Checksum,

    /// <summary>Its hash matched, and the registry's signature of the package was verified with the registry's key.</summary>
    Signature,
}
