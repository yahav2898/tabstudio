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

    /// <summary>
    /// The program F5 runs among a Makefile's, chosen as a person would: the only one; else the first that a launch
    /// configuration's <c>program</c> names; else the only one at the project's root rather than in its bin, build or
    /// out folder. <see langword="null"/> when a person would have to choose.
    /// </summary>
    /// <param name="programs">The programs, from <see cref="Find"/>.</param>
    /// <param name="directory">The project root.</param>
    /// <param name="launchPrograms">The <c>program</c> of each launch configuration, in order, with its variables replaced.</param>
    /// <param name="launchDirectory">The folder a relative <c>program</c> is in: the workspace's.</param>
    public static string? Choose(IReadOnlyList<string> programs, string directory, IReadOnlyList<string> launchPrograms, string launchDirectory)
    {
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(launchPrograms);
        ArgumentNullException.ThrowIfNull(launchDirectory);
        if (programs.Count == 1)
        {
            return programs[0];
        }

        foreach (var program in launchPrograms)
        {
            if (programs.FirstOrDefault(p => Names(program, p, launchDirectory)) is { } named)
            {
                return named;
            }
        }

        var atRoot = programs.Where(p => Path.GetDirectoryName(p) is { } folder && SamePath(folder, directory)).Take(2).ToList();
        return atRoot.Count == 1 ? atRoot[0] : null;
    }

    /// <summary>
    /// Whether the <c>program</c> of a launch configuration names a built executable: by its file name, with or
    /// without its extension, or by its path, absolute or relative to <paramref name="directory"/>.
    /// </summary>
    /// <param name="program">The configuration's program, with its variables replaced.</param>
    /// <param name="executable">The executable's absolute path.</param>
    /// <param name="directory">The folder a relative path is in, such as the workspace's.</param>
    public static bool Names(string program, string executable, string directory)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(directory);
        if (program.Length == 0)
        {
            return false;
        }

        if (program.Contains('/', StringComparison.Ordinal) || program.Contains('\\', StringComparison.Ordinal))
        {
            return SamePath(Path.Combine(directory, program), executable);
        }

        return string.Equals(program, Path.GetFileName(executable), PathComparison)
            || string.Equals(program, Path.GetFileNameWithoutExtension(executable), PathComparison);
    }

    /// <summary>Whether two paths name the same file or folder, as the file system compares them.</summary>
    internal static bool SamePath(string first, string second) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)), PathComparison);

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

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
