namespace TabStudio.Compilation.Wpf;

/// <summary>
/// What WPF has that OpenSilver does not: the namespaces none of whose types OpenSilver has, the types it lacks, and
/// the members its types lack, with WPF's base types to look members up along. TabStudio ships the catalog
/// (<c>Wpf/MissingInOpenSilver.txt</c>, made from WPF's reference assemblies by the tests), so a WPF project's
/// errors and its XAML can name what OpenSilver does not have and point to the Windows Bridge.
/// </summary>
/// <remarks>Names are .NET metadata names, such as <c>System.Windows.Controls.DocumentViewer</c>.</remarks>
public sealed class WpfApiCatalog
{
    /// <summary>The catalog's file name, as the tests write it.</summary>
    public const string FileName = "MissingInOpenSilver.txt";

    /// <summary>The name the catalog is embedded under.</summary>
    public const string ResourceName = "Wpf/" + FileName;

    private static readonly Lazy<WpfApiCatalog> LazyShipped = new(LoadShipped);

    private readonly HashSet<string> _namespaces = new(StringComparer.Ordinal);
    private readonly HashSet<string> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _typesByName = new(StringComparer.Ordinal);
    private readonly HashSet<(string Type, string Member)> _members = [];
    private readonly Dictionary<string, string> _bases = new(StringComparer.Ordinal);

    private WpfApiCatalog()
    {
    }

    /// <summary>The catalog TabStudio ships, or an empty one when it is not in the build.</summary>
    public static WpfApiCatalog Shipped => LazyShipped.Value;

    /// <summary>Whether the catalog lists nothing.</summary>
    public bool IsEmpty => _namespaces.Count == 0 && _types.Count == 0 && _members.Count == 0;

    /// <summary>Reads a catalog's text.</summary>
    /// <param name="text">Lines of <c>N namespace</c>, <c>T type</c>, <c>M type member</c> and <c>B type base</c>; <c>#</c> starts a comment.</param>
    public static WpfApiCatalog Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var catalog = new WpfApiCatalog();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < 3 || line[0] == '#' || line[1] != ' ')
            {
                continue;
            }

            var rest = line[2..];
            var space = rest.IndexOf(' ', StringComparison.Ordinal);
            switch (line[0])
            {
                case 'N':
                    catalog._namespaces.Add(rest);
                    break;
                case 'T':
                    catalog._types.Add(rest);
                    var name = SimpleName(rest);
                    if (!catalog._typesByName.TryGetValue(name, out var list))
                    {
                        catalog._typesByName[name] = list = [];
                    }

                    list.Add(rest);
                    break;
                case 'M' when space > 0:
                    catalog._members.Add((rest[..space], rest[(space + 1)..]));
                    break;
                case 'B' when space > 0:
                    catalog._bases[rest[..space]] = rest[(space + 1)..];
                    break;
            }
        }

        return catalog;
    }

    /// <summary>Whether WPF has a namespace none of whose types OpenSilver has, such as <c>System.Windows.Media.Media3D</c>.</summary>
    /// <param name="ns">The namespace.</param>
    public bool IsMissingNamespace(string ns) => _namespaces.Contains(ns);

    /// <summary>Whether WPF has a type OpenSilver does not.</summary>
    /// <param name="fullName">The type's metadata name.</param>
    public bool IsMissingType(string fullName) => _types.Contains(fullName);

    /// <summary>The WPF types OpenSilver does not have with a simple name, such as <c>DocumentViewer</c>, in any namespace.</summary>
    /// <param name="name">The name, without a namespace or generic arity.</param>
    public IReadOnlyList<string> MissingTypesNamed(string name) =>
        _typesByName.TryGetValue(name, out var types) ? types : [];

    /// <summary>
    /// The WPF type that declares a member OpenSilver's type lacks, looked up from a type along WPF's base types:
    /// <c>System.Windows.Media.Visual</c> for <c>PointToScreen</c> on a button.
    /// </summary>
    /// <param name="typeFullName">The type the member is used on.</param>
    /// <param name="member">The member's name.</param>
    /// <returns>The declaring type's metadata name, or <see langword="null"/> when WPF has no such member OpenSilver lacks.</returns>
    public string? FindMissingMember(string typeFullName, string member)
    {
        ArgumentNullException.ThrowIfNull(typeFullName);
        ArgumentNullException.ThrowIfNull(member);
        string? type = typeFullName;
        for (var steps = 0; type is not null && steps < 64; steps++)
        {
            if (_members.Contains((type, member)))
            {
                return type;
            }

            type = _bases.GetValueOrDefault(type);
        }

        return null;
    }

    /// <summary>A type's name without its namespace or generic arity, such as <c>DocumentViewer</c>.</summary>
    /// <param name="fullName">The type's metadata name.</param>
    public static string SimpleName(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        var name = fullName[(fullName.LastIndexOf('.') + 1)..];
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }

    private static WpfApiCatalog LoadShipped()
    {
        using var stream = typeof(WpfApiCatalog).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return new WpfApiCatalog();
        }

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
