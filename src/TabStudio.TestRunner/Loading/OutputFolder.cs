using System.Security.Cryptography;
using TabStudio.Compilation;

namespace TabStudio.TestRunner.Loading;

/// <summary>
/// Writes a test session's compiled assemblies to the project's output folder, as <c>dotnet build</c> does, so they
/// are loaded from files: <c>Assembly.Location</c> is then the file, and tests that find their data next to it,
/// as markdig's and Bogus's do, or that compile against it, as CsvHelper's do, work as under <c>dotnet test</c>.
/// </summary>
/// <remarks>
/// The runtime keeps an assembly file it loaded open, and reuses its image for the same path, for as long as the
/// process runs; sessions cannot be unloaded either. So a later session whose assemblies changed cannot use a folder
/// an earlier one loaded from: it gets a sibling at the same depth (<c>bin/Debug/net8.0-2</c>), so paths the tests
/// make relative to it, such as markdig's <c>../../..</c>, still lead to the project.
/// </remarks>
internal static class OutputFolder
{
    private static readonly Lock Gate = new();

    /// <summary>The files this process loaded assemblies from, with a hash of their content.</summary>
    private static readonly Dictionary<string, string> Loaded = new(StringComparer.Ordinal);

    /// <summary>
    /// Chooses the folder for a set of assemblies and writes them there, with their symbols, unless the files there
    /// are already these.
    /// </summary>
    /// <param name="folder">The project's output folder, such as <c>bin/Debug/net8.0</c>.</param>
    /// <param name="assemblies">The compiled assemblies.</param>
    /// <returns>The path of each assembly's file, by name.</returns>
    /// <exception cref="IOException">The folder could not be written.</exception>
    public static Dictionary<string, string> Write(string folder, IReadOnlyList<CompiledAssembly> assemblies)
    {
        var hashes = assemblies.ToDictionary(a => a.Name, a => Convert.ToHexString(SHA256.HashData(a.Image)), StringComparer.OrdinalIgnoreCase);
        lock (Gate)
        {
            var chosen = Candidates(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)))
                .First(candidate => assemblies.All(a => !Loaded.TryGetValue(FileOf(candidate, a), out var hash) || hash == hashes[a.Name]));
            _ = Directory.CreateDirectory(chosen);
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assembly in assemblies)
            {
                var file = FileOf(chosen, assembly);
                if (!Loaded.ContainsKey(file))
                {
                    Replace(file, assembly.Image);
                    if (assembly.Symbols is { } symbols)
                    {
                        Replace(Path.ChangeExtension(file, ".pdb"), symbols);
                    }
                }

                files[assembly.Name] = file;
            }

            return files;
        }
    }

    /// <summary>Records that an assembly was loaded from a file, so the file is not replaced while the process runs.</summary>
    /// <param name="file">The file.</param>
    /// <param name="assembly">What it holds.</param>
    public static void MarkLoaded(string file, CompiledAssembly assembly)
    {
        lock (Gate)
        {
            Loaded[file] = Convert.ToHexString(SHA256.HashData(assembly.Image));
        }
    }

    private static IEnumerable<string> Candidates(string folder)
    {
        yield return folder;
        for (var n = 2; ; n++)
        {
            yield return folder + "-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static string FileOf(string folder, CompiledAssembly assembly) => Path.Combine(folder, assembly.Name + ".dll");

    /// <summary>Writes a file beside its final name and moves it there, so nothing ever reads half of it.</summary>
    private static void Replace(string file, byte[] content)
    {
        var temporary = file + ".tmp";
        File.WriteAllBytes(temporary, content);
        File.Move(temporary, file, overwrite: true);
    }
}
