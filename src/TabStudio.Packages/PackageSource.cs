namespace TabStudio.Packages;

/// <summary>
/// A place packages are restored from: a NuGet V3 feed on the web, or a folder of <c>.nupkg</c> files on the device.
/// </summary>
/// <param name="Name">A short name used in messages, for example <c>nuget.org</c>.</param>
/// <param name="Location">
/// The address of a V3 service index, such as <c>https://api.nuget.org/v3/index.json</c>, or the path of a folder.
/// A folder may hold <c>.nupkg</c> files directly or in any subfolders, such as NuGet's <c>{id}/{version}/</c> layout.
/// </param>
public sealed record PackageSource(string Name, string Location)
{
    /// <summary>The public NuGet gallery, the default source.</summary>
    public static PackageSource NuGetOrg { get; } = new("nuget.org", "https://api.nuget.org/v3/index.json");

    /// <summary>Whether the source is a folder on the device rather than a feed on the web.</summary>
    public bool IsLocal => !Uri.TryCreate(Location, UriKind.Absolute, out var uri) || uri.IsFile;

    /// <summary>The full path of a local source's folder.</summary>
    internal string LocalPath =>
        Uri.TryCreate(Location, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath : Path.GetFullPath(Location);
}
