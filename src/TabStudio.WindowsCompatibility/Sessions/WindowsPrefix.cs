using System.Globalization;
using System.Text;
using TabStudio.Toolchains.Processes;

namespace TabStudio.WindowsCompatibility.Sessions;

/// <summary>
/// The Wine prefix Windows apps run in: its C: drive and registry, created by <c>wineboot</c> the first time and
/// whenever Wine is updated, and set up for TabStudio: Wine's virtual desktop, the display scale, WPF's renderer,
/// and no desktop shortcuts or Mono and Gecko prompts.
/// </summary>
/// <remarks>
/// <c>wineboot</c> copies Wine's own DLLs into the prefix's <c>system32</c>, which doubles what Wine takes on the
/// device; the copies that are identical to Wine's own files become links to them, which Wine loads the same way.
/// </remarks>
internal sealed class WindowsPrefix(WindowsLayout layout, Func<IReadOnlyDictionary<string, string?>> environment)
{
    /// <summary>The name of Wine's virtual desktop, which every app TabStudio starts shares.</summary>
    public const string DesktopName = "TabStudio";

    private const string MarkerFile = ".tabstudio-prefix";

    /// <summary>The file that says which Wine set the prefix up, in the prefix.</summary>
    private string Marker => layout.ToDevicePath(layout.Prefix + "/" + MarkerFile);

    /// <summary>Creates or updates the prefix when it is missing or older than Wine.</summary>
    /// <param name="progress">Receives what happens.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the prefix was (re)created.</returns>
    public async Task<bool> EnsureAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var version = await RunAsync([layout.Wine!, "--version"], cancellationToken).ConfigureAwait(false);
        var wine = version.Output.Trim();
        if (File.Exists(Marker) && (await File.ReadAllTextAsync(Marker, cancellationToken).ConfigureAwait(false)).Trim() == wine)
        {
            return false;
        }

        var update = Directory.Exists(layout.ToDevicePath(layout.Prefix + "/drive_c"));
        progress?.Report(update ? $"Updating the Windows installation for {wine}" : "Setting up Windows (the first time takes a minute)");
        // Without Mono and Gecko, which .NET apps do not need: Wine would offer to download them.
        var boot = await RunAsync([layout.Wine!, "wineboot", update ? "-u" : "-i"], cancellationToken, ("WINEDLLOVERRIDES", "mscoree=;mshtml=;winemenubuilder.exe=;winedbg.exe=")).ConfigureAwait(false);
        if (!boot.Succeeded)
        {
            throw new WindowsCompatibilityException($"Wine could not set up Windows ({boot.Exit.Description}): {boot.Tail(4)}");
        }

        await WaitForServerAsync(cancellationToken).ConfigureAwait(false);
        await ApplySettingsAsync(new WindowsSessionOptions(), cancellationToken).ConfigureAwait(false);
        var saved = LinkWineDlls();
        progress?.Report(string.Create(CultureInfo.InvariantCulture, $"Windows is set up ({saved / 1048576} MB of Wine's files shared with the installation)"));
        await File.WriteAllTextAsync(Marker, wine + "\n", cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Writes the registry settings a session needs: the desktop, the display scale and WPF's renderer.</summary>
    /// <param name="options">The session's settings.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task ApplySettingsAsync(WindowsSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var reg = layout.SessionFolder + "/settings.reg";
        Directory.CreateDirectory(Path.GetDirectoryName(layout.ToDevicePath(reg))!);
        await File.WriteAllTextAsync(layout.ToDevicePath(reg), Registry(options), cancellationToken).ConfigureAwait(false);
        var result = await RunAsync([layout.Wine!, "regedit", "/S", WindowsLayout.ToWindowsPath(reg)], cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new WindowsCompatibilityException($"Wine could not save the Windows settings ({result.Exit.Description}): {result.Tail(3)}");
        }
    }

    /// <summary>The registry file for a session's settings.</summary>
    internal static string Registry(WindowsSessionOptions options)
    {
        var text = new StringBuilder("REGEDIT4\n\n");
        text.Append("[HKEY_CURRENT_USER\\Software\\Wine\\Explorer]\n\"Desktop\"=\"").Append(DesktopName).Append("\"\n\n");
        text.Append("[HKEY_CURRENT_USER\\Software\\Wine\\Explorer\\Desktops]\n\"").Append(DesktopName).Append("\"=\"")
            .Append(options.Width.ToString(CultureInfo.InvariantCulture)).Append('x').Append(options.Height.ToString(CultureInfo.InvariantCulture)).Append("\"\n\n");
        text.Append("[HKEY_CURRENT_USER\\Control Panel\\Desktop]\n\"LogPixels\"=dword:").Append(options.Dpi.ToString("x8", CultureInfo.InvariantCulture)).Append("\n\n");
        text.Append("[HKEY_CURRENT_USER\\Software\\Wine\\Fonts]\n\"LogPixels\"=dword:").Append(options.Dpi.ToString("x8", CultureInfo.InvariantCulture)).Append("\n\n");
        text.Append("[HKEY_CURRENT_USER\\Software\\Microsoft\\Avalon.Graphics]\n\"DisableHWAcceleration\"=dword:")
            .Append(options.WpfRendering == WpfRendering.Software ? "00000001" : "00000000").Append("\n\n");
        text.Append("[HKEY_CURRENT_USER\\Software\\Wine\\DllOverrides]\n\"winemenubuilder.exe\"=\"\"\n\n");

        // An unhandled exception ends the app, as on Windows without a debugger, instead of starting Wine's debugger.
        text.Append("[HKEY_LOCAL_MACHINE\\Software\\Microsoft\\Windows NT\\CurrentVersion\\AeDebug]\n\"Debugger\"=\"\"\n\"Auto\"=\"0\"\n\n");
        text.Append("[HKEY_CURRENT_USER\\Software\\Wine\\WineDbg]\n\"ShowCrashDialog\"=dword:00000000\n\n");
        return text.ToString();
    }

    /// <summary>Waits until the Wine server has written the registry and stopped, as <c>wineserver -w</c> does.</summary>
    private async Task WaitForServerAsync(CancellationToken cancellationToken)
    {
        if (layout.WineServer is { } server)
        {
            await RunAsync([server, "-w"], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Replaces the prefix's copies of Wine's DLLs with links to Wine's own files.</summary>
    /// <returns>The bytes saved.</returns>
    private long LinkWineDlls()
    {
        if (layout.Wine is not { } wine)
        {
            return 0;
        }

        var library = layout.ToDevicePath(wine[..wine.LastIndexOf('/')] + "/../lib/wine");
        if (!Directory.Exists(library))
        {
            return 0;
        }

        var builtins = new Dictionary<(string Name, long Size), List<string>>();
        foreach (var folder in Directory.EnumerateDirectories(library, "*-windows"))
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var info = new FileInfo(file);
                var key = (info.Name.ToLowerInvariant(), info.Length);
                if (!builtins.TryGetValue(key, out var list))
                {
                    builtins[key] = list = [];
                }

                list.Add(file);
            }
        }

        long saved = 0;
        foreach (var system in new[] { "system32", "syswow64" })
        {
            var folder = layout.ToDevicePath(layout.Prefix + "/drive_c/windows/" + system);
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                if (info.LinkTarget is not null || !builtins.TryGetValue((info.Name.ToLowerInvariant(), info.Length), out var candidates))
                {
                    continue;
                }

                var same = candidates.FirstOrDefault(candidate => SameContent(file, candidate));
                if (same is not null)
                {
                    File.Delete(file);
                    File.CreateSymbolicLink(file, ToProgramPath(same));
                    saved += info.Length;
                }
            }
        }

        return saved;
    }

    /// <summary>A file's path as programs in the environment see it, for a link they follow.</summary>
    private string ToProgramPath(string devicePath)
    {
        var root = layout.ToDevicePath("/");
        return devicePath.StartsWith(root, StringComparison.Ordinal) ? "/" + devicePath[root.Length..].TrimStart('/') : devicePath;
    }

    private static bool SameContent(string a, string b)
    {
        using var first = File.OpenRead(a);
        using var second = File.OpenRead(b);
        if (first.Length != second.Length)
        {
            return false;
        }

        var left = new byte[65536];
        var right = new byte[65536];
        int read;
        while ((read = first.Read(left, 0, left.Length)) > 0)
        {
            second.ReadExactly(right, 0, read);
            if (!left.AsSpan(0, read).SequenceEqual(right.AsSpan(0, read)))
            {
                return false;
            }
        }

        return true;
    }

    private Task<CommandResult> RunAsync(IReadOnlyList<string> command, CancellationToken cancellationToken, params (string Name, string Value)[] variables)
    {
        var values = new Dictionary<string, string?>(environment(), StringComparer.Ordinal);
        foreach (var (name, value) in variables)
        {
            values[name] = value;
        }

        return CommandRunner.RunAsync(
            layout.Runtime,
            new ProcessStartSpec { Program = command[0], Arguments = [.. command.Skip(1)], Environment = values },
            null,
            cancellationToken);
    }
}
