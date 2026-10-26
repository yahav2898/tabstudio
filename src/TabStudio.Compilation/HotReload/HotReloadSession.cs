using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace TabStudio.Compilation.HotReload;

/// <summary>Applies validated Roslyn deltas to the actual assemblies of one running app, preserving its state.</summary>
public sealed class HotReloadSession : IDisposable
{
    private readonly Lock _gate = new();
    private readonly AssemblyLoadContext _context;
    private readonly Dictionary<string, CompiledAssembly> _images;
    private readonly Dictionary<string, HotReloadBaseline> _baselines = new(StringComparer.Ordinal);
    private bool _faulted;
    private bool _disposed;

    /// <summary>Whether CoreCLR or Android Mono enabled metadata updates at process startup.</summary>
    public static bool IsSupported => MetadataUpdater.IsSupported;

    /// <summary>The reason updates are unavailable, suitable for the IDE.</summary>
    public static string UnsupportedReason => "C# Hot Reload is unavailable in this runtime. Start TabStudio with DOTNET_MODIFIABLE_ASSEMBLIES=debug (Android Mono also needs the interpreter), then run a Debug build.";

    /// <summary>Creates a session for a loaded local build.</summary>
    /// <param name="entry">The build's entry image.</param>
    /// <param name="dependencies">Its referenced project images.</param>
    /// <param name="loadedEntry">The exact assembly loaded for this run, never a previous run's copy.</param>
    public HotReloadSession(CompiledAssembly entry, IReadOnlyList<CompiledAssembly> dependencies, Assembly loadedEntry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(loadedEntry);
        _context = AssemblyLoadContext.GetLoadContext(loadedEntry)!;
        _images = dependencies.Append(entry).ToDictionary(a => a.Name, StringComparer.Ordinal);
        if (loadedEntry.ManifestModule.ModuleVersionId != ReadMvid(entry.Image))
        {
            throw new ArgumentException("The Hot Reload image does not match the loaded assembly.", nameof(loadedEntry));
        }
    }

    /// <summary>Validates every project first, then applies its deltas and dispatches MetadataUpdateHandlers.</summary>
    /// <param name="updated">The current Debug build of every project, with retained compilations.</param>
    /// <param name="cancellationToken">Cancels preparation; application itself is not cancellable.</param>
    public HotReloadResult Apply(IReadOnlyList<CompiledAssembly> updated, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updated);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!IsSupported)
            {
                return new(HotReloadStatus.Unsupported, UnsupportedReason, []);
            }

            if (_faulted)
            {
                return new(HotReloadStatus.RestartRequired, "A previous runtime update failed. Restart the app before applying more edits.", []);
            }

            if (!_images.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(updated.Select(a => a.Name)))
            {
                return new(HotReloadStatus.RestartRequired, "Changing project references requires a restart.", []);
            }

            var pending = new List<(CompiledAssembly Image, HotReloadBaseline Baseline, HotReloadDelta Delta, Assembly Assembly)>();
            foreach (var image in updated)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (image.HotReloadCompilation is null || _images[image.Name].HotReloadCompilation is null)
                {
                    return new(HotReloadStatus.RestartRequired, "Hot Reload needs a local Debug build. Restart in Debug configuration.", []);
                }

                if (!SameResources(_images[image.Name].Image, image.Image))
                {
                    return new(HotReloadStatus.RestartRequired, "Changing embedded resources or XAML requires a restart.", []);
                }

                if (!_baselines.TryGetValue(image.Name, out var baseline))
                {
                    baseline = new HotReloadBaseline(_images[image.Name]);
                    _baselines.Add(image.Name, baseline);
                }

                var result = baseline.EmitDifference(image.HotReloadCompilation, out var delta, cancellationToken);
                if (!result.Success)
                {
                    return result;
                }

                if (delta is not null)
                {
                    var assembly = _context.LoadFromAssemblyName(new AssemblyName(image.Name));
                    pending.Add((image, baseline, delta, assembly));
                }
            }

            // Do not leave a half-applied build because cancellation arrived between two assemblies.
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var item in pending)
                {
                    MetadataUpdater.ApplyUpdate(item.Assembly, item.Delta.Metadata, item.Delta.IL, item.Delta.Pdb);
                    item.Baseline.Accept(item.Delta);
                    _images[item.Image.Name] = item.Image;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // ApplyUpdate isn't transactional across assemblies. Never retry a possibly half-applied build.
                _faulted = true;
                return new(HotReloadStatus.RestartRequired, $"The runtime could not apply these edits: {ex.Message} Restart the app.", []);
            }

            if (pending.Count == 0)
            {
                return new(HotReloadStatus.NoChanges, "No executable C# changes to apply.", []);
            }

            var warnings = MetadataUpdateHandlers.Notify(_context);
            return new(HotReloadStatus.Applied, "C# edits applied without restarting. Existing calls finish their old code; new calls use the edits." + warnings, []);
        }
    }

    private static Guid ReadMvid(byte[] image)
    {
        using var pe = new PEReader(new MemoryStream(image, writable: false));
        var reader = pe.GetMetadataReader();
        return reader.GetGuid(reader.GetModuleDefinition().Mvid);
    }

    private static bool SameResources(byte[] before, byte[] after)
    {
        static byte[] Read(byte[] image)
        {
            using var pe = new PEReader(new MemoryStream(image, writable: false));
            var directory = pe.PEHeaders.CorHeader!.ResourcesDirectory;
            return directory.Size == 0 ? [] : pe.GetSectionData(directory.RelativeVirtualAddress).GetContent(0, directory.Size).ToArray();
        }

        return Read(before).AsSpan().SequenceEqual(Read(after));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var baseline in _baselines.Values) baseline.Dispose();
            _baselines.Clear();
            _images.Clear();
        }
    }
}
