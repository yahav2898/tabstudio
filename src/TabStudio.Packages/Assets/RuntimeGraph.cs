using System.Runtime.InteropServices;
using System.Text.Json;

namespace TabStudio.Packages.Assets;

/// <summary>
/// The runtime identifier (RID) graph: which RIDs can use the assets of which others, such as <c>linux-arm64</c> using
/// those of <c>linux</c>, <c>unix</c> and <c>any</c>. It decides which <c>runtimes/{rid}/</c> folders of a package a
/// device uses, as NuGet and the .NET host decide it.
/// </summary>
/// <remarks>
/// The graph starts from <see cref="Fallback"/>, the part of the .NET SDK's portable RID graph
/// (<c>PortableRuntimeIdentifierGraph.json</c> of SDK 9.0.3xx) for Android, Linux, Windows and macOS, and takes in the
/// <c>runtime.json</c> files of the restored packages, as NuGet merges them. An instance is immutable.
/// </remarks>
internal sealed class RuntimeGraph
{
    /// <summary>The name of the file in which a package describes RIDs.</summary>
    public const string FileName = "runtime.json";

    private static readonly Dictionary<string, string[]> PortableGraph = new(StringComparer.OrdinalIgnoreCase)
    {
        ["android"] = ["linux-bionic"],
        ["android-arm"] = ["android", "linux-bionic-arm"],
        ["android-arm64"] = ["android", "linux-bionic-arm64"],
        ["android-x64"] = ["android", "linux-bionic-x64"],
        ["android-x86"] = ["android", "linux-bionic-x86"],
        ["any"] = ["base"],
        ["base"] = [],
        ["linux"] = ["unix"],
        ["linux-arm"] = ["linux", "unix-arm"],
        ["linux-arm64"] = ["linux", "unix-arm64"],
        ["linux-armel"] = ["linux", "unix-armel"],
        ["linux-armv6"] = ["linux", "unix-armv6"],
        ["linux-bionic"] = ["linux"],
        ["linux-bionic-arm"] = ["linux-bionic", "linux-arm"],
        ["linux-bionic-arm64"] = ["linux-bionic", "linux-arm64"],
        ["linux-bionic-x64"] = ["linux-bionic", "linux-x64"],
        ["linux-bionic-x86"] = ["linux-bionic", "linux-x86"],
        ["linux-loongarch64"] = ["linux", "unix-loongarch64"],
        ["linux-mips64"] = ["linux", "unix-mips64"],
        ["linux-musl"] = ["linux"],
        ["linux-musl-arm"] = ["linux-musl", "linux-arm"],
        ["linux-musl-arm64"] = ["linux-musl", "linux-arm64"],
        ["linux-musl-armel"] = ["linux-musl", "linux-armel"],
        ["linux-musl-armv6"] = ["linux-musl", "linux-armv6"],
        ["linux-musl-ppc64le"] = ["linux-musl", "linux-ppc64le"],
        ["linux-musl-riscv64"] = ["linux-musl", "linux-riscv64"],
        ["linux-musl-loongarch64"] = ["linux-musl", "linux-loongarch64"],
        ["linux-musl-s390x"] = ["linux-musl", "linux-s390x"],
        ["linux-musl-x64"] = ["linux-musl", "linux-x64"],
        ["linux-musl-x86"] = ["linux-musl", "linux-x86"],
        ["linux-ppc64le"] = ["linux", "unix-ppc64le"],
        ["linux-riscv64"] = ["linux", "unix-riscv64"],
        ["linux-s390x"] = ["linux", "unix-s390x"],
        ["linux-x64"] = ["linux", "unix-x64"],
        ["linux-x86"] = ["linux", "unix-x86"],
        ["osx"] = ["unix"],
        ["osx-arm64"] = ["osx", "unix-arm64"],
        ["osx-x64"] = ["osx", "unix-x64"],
        ["unix"] = ["any"],
        ["unix-arm"] = ["unix"],
        ["unix-arm64"] = ["unix"],
        ["unix-armel"] = ["unix"],
        ["unix-armv6"] = ["unix"],
        ["unix-loongarch64"] = ["unix"],
        ["unix-mips64"] = ["unix"],
        ["unix-ppc64le"] = ["unix"],
        ["unix-riscv64"] = ["unix"],
        ["unix-s390x"] = ["unix"],
        ["unix-x64"] = ["unix"],
        ["unix-x86"] = ["unix"],
        ["win"] = ["any"],
        ["win-arm"] = ["win"],
        ["win-arm64"] = ["win"],
        ["win-x64"] = ["win"],
        ["win-x86"] = ["win"],
    };

    private readonly Dictionary<string, IReadOnlyList<string>> _imports;

    private RuntimeGraph(Dictionary<string, IReadOnlyList<string>> imports) => _imports = imports;

    /// <summary>
    /// The graph TabStudio ships: the .NET SDK's portable RIDs for Android, Linux (glibc, musl and Bionic), Windows and
    /// macOS, each importing the RIDs whose assets it can use.
    /// </summary>
    public static RuntimeGraph Fallback { get; } = new(PortableGraph.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// The RID of this device, from which package assets are chosen: <c>android-arm64</c> (which falls back through
    /// <c>linux-bionic-arm64</c> to <c>linux-arm64</c>, <c>linux</c>, <c>unix</c> and <c>any</c>) on an Android tablet,
    /// and the runtime's own portable RID, such as <c>linux-x64</c>, <c>win-x64</c> or <c>osx-arm64</c>, elsewhere.
    /// </summary>
    public static string DeviceRuntimeIdentifier { get; } = GetDeviceRuntimeIdentifier();

    /// <summary>
    /// The RIDs whose assets a RID can use, nearest first, starting with the RID itself: a breadth-first walk of the
    /// imports, as NuGet's <c>RuntimeGraph.ExpandRuntime</c> does. <c>any</c> and <c>base</c> always end the list, so
    /// an unknown RID still uses RID-neutral assets.
    /// </summary>
    /// <param name="runtimeIdentifier">The RID, such as <c>linux-arm64</c>.</param>
    public IReadOnlyList<string> Expand(string runtimeIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        var expansion = new List<string> { runtimeIdentifier };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { runtimeIdentifier };
        for (var i = 0; i < expansion.Count; i++)
        {
            if (!_imports.TryGetValue(expansion[i], out var imports))
            {
                continue;
            }

            foreach (var imported in imports)
            {
                if (seen.Add(imported))
                {
                    expansion.Add(imported);
                }
            }
        }

        foreach (var root in (string[])["any", "base"])
        {
            if (seen.Add(root))
            {
                expansion.Add(root);
            }
        }

        return expansion;
    }

    /// <summary>
    /// Adds the RIDs of a package's <c>runtime.json</c>. A RID the graph already has keeps its imports and gains the
    /// file's other ones after them; a new RID is added as the file describes it.
    /// </summary>
    /// <param name="json">The contents of <c>runtime.json</c>.</param>
    /// <returns>The merged graph, or this one when the file is not valid JSON or describes no RIDs.</returns>
    public RuntimeGraph Merge(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        Dictionary<string, List<string>> parsed;
        try
        {
            parsed = Parse(json);
        }
        catch (JsonException)
        {
            return this;
        }

        if (parsed.Count == 0)
        {
            return this;
        }

        var merged = new Dictionary<string, IReadOnlyList<string>>(_imports, StringComparer.OrdinalIgnoreCase);
        foreach (var (rid, imports) in parsed)
        {
            merged[rid] = merged.TryGetValue(rid, out var existing)
                ? [.. existing, .. imports.Where(i => !existing.Contains(i, StringComparer.OrdinalIgnoreCase))]
                : imports;
        }

        return new RuntimeGraph(merged);
    }

    /// <summary>Adds the <c>runtime.json</c> files of several packages, skipping files that cannot be read.</summary>
    /// <param name="packageDirectories">The folders of the packages.</param>
    public RuntimeGraph MergeFrom(IEnumerable<string> packageDirectories)
    {
        ArgumentNullException.ThrowIfNull(packageDirectories);
        var graph = this;
        foreach (var directory in packageDirectories)
        {
            var path = Path.Combine(directory, FileName);
            try
            {
                if (File.Exists(path))
                {
                    graph = graph.Merge(File.ReadAllText(path));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A package's RID file only refines the graph; the fallback already covers the device.
            }
        }

        return graph;
    }

    private static Dictionary<string, List<string>> Parse(string json)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("runtimes", out var runtimes)
            || runtimes.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var runtime in runtimes.EnumerateObject())
        {
            var imports = new List<string>();
            if (runtime.Value.ValueKind == JsonValueKind.Object
                && runtime.Value.TryGetProperty("#import", out var list)
                && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in list.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String && element.GetString() is { Length: > 0 } imported)
                    {
                        imports.Add(imported);
                    }
                }
            }

            result[runtime.Name] = imports;
        }

        return result;
    }

    private static string GetDeviceRuntimeIdentifier()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            Architecture.X86 => "x86",
            _ => "x64",
        };

        if (OperatingSystem.IsAndroid())
        {
            return $"android-{architecture}";
        }

        // Since .NET 8 this is the portable RID the runtime was built for, such as linux-x64 or linux-musl-arm64.
        var own = RuntimeInformation.RuntimeIdentifier;
        if (PortableGraph.ContainsKey(own))
        {
            return own;
        }

        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() ? "osx" : "linux";
        return $"{os}-{architecture}";
    }
}
