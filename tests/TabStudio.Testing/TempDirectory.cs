namespace TabStudio.Testing;

/// <summary>
/// A scratch directory for one test, deleted when the test ends.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tabstudio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Writes a file, creating its folders. Forward slashes work on every platform.</summary>
    public string Write(string relativePath, string content = "")
    {
        var fullPath = Resolve(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public string Resolve(string relativePath) =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: the OS cleans the temp folder eventually.
        }
    }
}
