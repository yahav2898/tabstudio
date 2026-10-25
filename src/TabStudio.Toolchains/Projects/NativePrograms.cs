namespace TabStudio.Toolchains.Projects;

/// <summary>Finds executable output from a Makefile without requiring an invented make run target.</summary>
public static class NativePrograms
{
    /// <summary>Executable binaries in the project and its usual output folders.</summary>
    /// <param name="directory">The project root.</param>
    public static IReadOnlyList<string> Find(string directory)
    {
        var folders = new[] { directory, Path.Combine(directory, "bin"), Path.Combine(directory, "build"), Path.Combine(directory, "out") };
        return [.. folders.Where(Directory.Exists).SelectMany(Directory.EnumerateFiles).Where(IsExecutable).Order(StringComparer.Ordinal)];
    }

    private static bool IsExecutable(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            Span<byte> header = stackalloc byte[32];
            if (stream.Read(header) < 32)
            {
                return false;
            }

            if (header[0] == 0x7f && header[1] == 'E' && header[2] == 'L' && header[3] == 'F')
            {
                // A PIE and a shared library are both ET_DYN; a program also has an entry point.
                return header[5] == 1 && header[16] is 2 or 3 && header.Slice(24, header[4] == 1 ? 4 : 8).ContainsAnyExcept((byte)0);
            }

            return (header[0] == 'M' && header[1] == 'Z' && file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                || (header[0] == 0xcf && header[1] == 0xfa && header[2] == 0xed && header[3] == 0xfe && header[12] == 2);
        }
        catch (IOException)
        {
            return false;
        }
    }
}
