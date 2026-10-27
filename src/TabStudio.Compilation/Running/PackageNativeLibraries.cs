using System.Runtime.InteropServices;

namespace TabStudio.Compilation.Running;

/// <summary>
/// The native libraries of a program's NuGet packages, such as SQLite's <c>libe_sqlite3.so</c>, and how a program's
/// load context finds them when its code calls into one (<c>[DllImport]</c>, <c>[LibraryImport]</c> or
/// <c>NativeLibrary.Load</c>).
/// </summary>
/// <remarks>
/// <para>
/// A restore lists them with the package assemblies (<c>RestoreResult.RuntimeFiles</c>); they are told apart by the
/// <c>runtimes/{rid}/native/</c> folder they come from. A library name is matched the way the .NET host probes:
/// as given, and with the <c>lib</c> prefix and the platform's suffix (<c>.so</c>, <c>.dylib</c>, <c>.dll</c>).
/// </para>
/// <para>
/// A library that TabStudio itself already uses is never loaded a second time: when a library of the same file name is
/// already mapped into the process, such as the IDE's own <c>libSkiaSharp.so</c> or <c>libe_sqlite3.so</c>, that copy
/// is used. On Android the app's own copy (in the APK, built for Android's C library) comes next, because the Linux
/// builds that packages carry for <c>linux-arm64</c> need glibc. Only then is the package's file loaded, from the
/// global packages folder in the app's private storage: Android forbids executing files from app storage, but loading
/// a library from it with <c>dlopen</c> is allowed.
/// </para>
/// </remarks>
public sealed class PackageNativeLibraries
{
    private readonly Dictionary<string, string> _byFileName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Collects the native libraries from a list of package files.</summary>
    /// <param name="packageFiles">
    /// Package assemblies and native libraries, as <c>RestoreResult.RuntimeFiles</c> lists them; only the native ones are
    /// kept. The first file of a name wins.
    /// </param>
    public PackageNativeLibraries(IEnumerable<string> packageFiles)
    {
        ArgumentNullException.ThrowIfNull(packageFiles);
        foreach (var path in packageFiles.Where(IsNativeLibrary))
        {
            _byFileName.TryAdd(path[(path.LastIndexOfAny(['/', '\\']) + 1)..], path);
        }
    }

    /// <summary>Whether there are no native libraries.</summary>
    public bool IsEmpty => _byFileName.Count == 0;

    /// <summary>Whether a package file is a native library: it is in a <c>runtimes/{rid}/native/</c> folder.</summary>
    /// <param name="path">The full path of a file in a package folder.</param>
    public static bool IsNativeLibrary(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var parts = path.Split('/', '\\');
        for (var i = 0; i + 3 < parts.Length; i++)
        {
            if (parts[i].Equals("runtimes", StringComparison.OrdinalIgnoreCase) && parts[i + 2].Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The package files that are assemblies: every file but the native libraries.</summary>
    /// <param name="packageFiles">Package assemblies and native libraries.</param>
    public static IReadOnlyList<string> Assemblies(IEnumerable<string> packageFiles)
    {
        ArgumentNullException.ThrowIfNull(packageFiles);
        return [.. packageFiles.Where(path => !IsNativeLibrary(path))];
    }

    /// <summary>Finds the package file for a library name, such as <c>e_sqlite3</c> for <c>libe_sqlite3.so</c>.</summary>
    /// <param name="libraryName">The name code asked for.</param>
    /// <param name="path">The file, when a package has it.</param>
    public bool TryFind(string libraryName, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrEmpty(libraryName) || _byFileName.Count == 0 || Path.GetFileName(libraryName) != libraryName)
        {
            return false;
        }

        foreach (var candidate in Candidates(libraryName))
        {
            if (_byFileName.TryGetValue(candidate, out var found))
            {
                path = found;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Loads a library a package brings, preferring a copy TabStudio already has (see the remarks), for a load context's
    /// <c>LoadUnmanagedDll</c>.
    /// </summary>
    /// <param name="libraryName">The name code asked for.</param>
    /// <returns>The library's handle, or zero to let the runtime look for it the usual way.</returns>
    public nint Load(string libraryName)
    {
        if (!TryFind(libraryName, out var path))
        {
            return 0;
        }

        var fileName = Path.GetFileName(path);
        if (FindMapped(fileName) is { } mapped && NativeLibrary.TryLoad(mapped, out var shared))
        {
            return shared;
        }

        if (OperatingSystem.IsAndroid() && NativeLibrary.TryLoad(fileName, typeof(PackageNativeLibraries).Assembly, null, out var own))
        {
            return own;
        }

        return NativeLibrary.TryLoad(path, out var handle) ? handle : 0;
    }

    /// <summary>The file names a library name may have, as the .NET host probes them.</summary>
    private static IEnumerable<string> Candidates(string name)
    {
        yield return name;
        foreach (var suffix in (string[])[".so", ".dylib", ".dll"])
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                if (!name.StartsWith("lib", StringComparison.Ordinal) && suffix != ".dll")
                {
                    yield return "lib" + name;
                }

                yield break;
            }
        }

        yield return name + ".so";
        yield return "lib" + name + ".so";
        yield return name + ".dylib";
        yield return "lib" + name + ".dylib";
        yield return name + ".dll";
    }

    /// <summary>A library of this file name already mapped into the process, from <c>/proc/self/maps</c>.</summary>
    private static string? FindMapped(string fileName)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsAndroid())
        {
            return null;
        }

        try
        {
            foreach (var line in File.ReadLines("/proc/self/maps"))
            {
                var slash = line.IndexOf('/', StringComparison.Ordinal);
                if (slash < 0)
                {
                    continue;
                }

                var mappedPath = line[slash..].TrimEnd();
                if (string.Equals(Path.GetFileName(mappedPath), fileName, StringComparison.Ordinal))
                {
                    return mappedPath;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Without the map, the package's own copy is used.
        }

        return null;
    }
}
