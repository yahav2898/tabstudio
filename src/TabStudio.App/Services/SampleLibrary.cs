using System.Reflection;

namespace TabStudio.App.Services;

/// <summary>
/// The sample solution that ships inside the app. It is extracted into the projects folder the first time
/// it is opened, and opened from there afterwards so the user's changes are kept.
/// </summary>
public sealed class SampleLibrary
{
    /// <summary>The name of the bundled sample.</summary>
    public const string SampleName = "HelloTablet";

    private const string ResourcePrefix = "Samples/" + SampleName + "/";
    private static readonly Assembly ResourceAssembly = typeof(SampleLibrary).Assembly;

    private readonly AppPaths _paths;

    /// <summary>Creates the library over the app's folders.</summary>
    /// <param name="paths">Where projects are stored.</param>
    public SampleLibrary(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <summary>The folder the sample lives in once extracted.</summary>
    public string SampleDirectory => Path.Combine(_paths.Projects, SampleName);

    /// <summary>The sample's solution file once extracted.</summary>
    public string SolutionPath => Path.Combine(SampleDirectory, SampleName + ".slnx");

    /// <summary>Extracts the sample unless it is already there, and returns its solution file.</summary>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    public async Task<string> EnsureExtractedAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(SolutionPath))
        {
            return SolutionPath;
        }

        var names = ResourceAssembly.GetManifestResourceNames()
            .Where(n => Normalize(n).StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToList();
        if (names.Count == 0)
        {
            throw new InvalidOperationException("The sample solution is missing from this build of TabStudio.");
        }

        foreach (var name in names)
        {
            var relative = Normalize(name)[ResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(Path.Combine(SampleDirectory, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using var source = ResourceAssembly.GetManifestResourceStream(name)!;
            await using var file = File.Create(target);
            await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        return SolutionPath;
    }

    /// <summary>Resource names keep the path separator of the machine that built the app.</summary>
    private static string Normalize(string resourceName) => resourceName.Replace('\\', '/');
}
