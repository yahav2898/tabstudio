using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Reads the assembly attributes the .NET SDK generates into <c>AssemblyInfo.cs</c>: one
/// <c>InternalsVisibleTo</c> per <c>&lt;InternalsVisibleTo&gt;</c> item and one attribute per
/// <c>&lt;AssemblyAttribute&gt;</c> item, as <c>Microsoft.NET.GenerateAssemblyInfo.targets</c> writes them.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Nothing is generated when <c>GenerateAssemblyInfo</c> is <c>false</c>; <c>InternalsVisibleTo</c> items are
///   also skipped when <c>GenerateInternalsVisibleToAttributes</c> is <c>false</c>.</item>
///   <item>An <c>InternalsVisibleTo</c> item's <c>Key</c> or <c>PublicKey</c> metadata, or else the project's <c>PublicKey</c> property,
///   becomes the friend's <c>PublicKey=</c>, which a strong-named assembly needs.</item>
///   <item>An <c>AssemblyAttribute</c> item's <c>_Parameter1</c>, <c>_Parameter2</c>... are its positional arguments,
///   strings unless <c>_ParameterN_IsLiteral</c> is <c>true</c>; any other metadata is a named argument.</item>
///   <item><c>$(Property)</c> references are expanded with the project's properties, <c>AssemblyName</c> defaulting to
///   the project name, as in <c>&lt;InternalsVisibleTo Include="$(AssemblyName).Tests" /&gt;</c>. An item that still
///   refers to an unknown property is left out.</item>
/// </list>
/// </remarks>
public static partial class AssemblyAttributeItems
{
    /// <summary>The attribute an <c>InternalsVisibleTo</c> item becomes.</summary>
    public const string InternalsVisibleTo = "System.Runtime.CompilerServices.InternalsVisibleToAttribute";

    /// <summary>Targets that run before the compiler, so the items their dependents add are compiled.</summary>
    private static readonly HashSet<string> BeforeCompileTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "BeforeBuild", "PrepareForBuild", "BeforeCompile", "CoreCompile", "Compile", "GenerateAssemblyInfo", "CoreGenerateAssemblyInfo", "GetAssemblyAttributes",
    };

    private static readonly HashSet<string> NotArguments = new(StringComparer.OrdinalIgnoreCase) { "Include", "Condition", "Remove", "Update", "Exclude" };

    /// <summary>Whether a list of NuGet assets, such as a <c>PrivateAssets</c> value, includes <c>all</c>.</summary>
    /// <param name="assets">The value, separated by semicolons, or <see langword="null"/>.</param>
    public static bool IncludesAll(string? assets) =>
        assets?.Split(';', StringSplitOptions.TrimEntries).Any(asset => string.Equals(asset, "all", StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>Reads the generated attributes from a project and the files it imports.</summary>
    /// <param name="roots">The project file's root element, then those of <c>Directory.Build.props</c> and <c>.targets</c>.</param>
    /// <param name="properties">The project's properties, already expanded.</param>
    /// <param name="projectName">The project name, the default <c>AssemblyName</c>.</param>
    public static IReadOnlyList<AssemblyAttributeInfo> Read(IReadOnlyList<XElement> roots, IReadOnlyDictionary<string, string> properties, string projectName)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(projectName);
        if (IsFalse(properties.GetValueOrDefault("GenerateAssemblyInfo")))
        {
            return [];
        }

        string? Expand(string? value) => value is null ? null : ExpandProperties(value.Trim(), properties, projectName);
        // Items in a target are added when it runs, which TabStudio does not do, except the friends below.
        var items = roots.SelectMany(root => root.Descendants())
            .Where(e => !e.Ancestors().Any(a => a.Name.LocalName == "Target"))
            .ToList();
        var attributes = new List<AssemblyAttributeInfo>();
        if (!IsFalse(properties.GetValueOrDefault("GenerateInternalsVisibleToAttributes")))
        {
            var defaultKey = Expand(properties.GetValueOrDefault("PublicKey"));
            var friends = items.Where(e => e.Name.LocalName == "InternalsVisibleTo")
                .Select(item => (Item: item, Include: Expand(item.Attribute("Include")?.Value)))
                .Concat(BeforeCompileFriends(roots, items, Expand));
            foreach (var (item, include) in friends)
            {
                foreach (var friend in (include ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var key = Expand(Metadata(item, "Key")) is { Length: > 0 } own ? own
                        : Expand(Metadata(item, "PublicKey")) is { Length: > 0 } named ? named : defaultKey;
                    var value = string.IsNullOrEmpty(key) ? friend : $"{friend}, PublicKey={key}";
                    attributes.Add(new AssemblyAttributeInfo(InternalsVisibleTo, [new AssemblyAttributeArgument(value)]));
                }
            }
        }

        foreach (var item in items.Where(e => e.Name.LocalName == "AssemblyAttribute"))
        {
            if (Expand(item.Attribute("Include")?.Value) is not { Length: > 0 } type || ReadArguments(item, Expand) is not { } arguments)
            {
                continue;
            }

            attributes.Add(new AssemblyAttributeInfo(type, arguments));
        }

        return [.. attributes.Distinct(AttributeComparer.Instance)];
    }

    /// <summary>
    /// The <c>InternalsVisibleTo</c> items a target adds just before compiling, from items of another type, as in
    /// Polly's <c>&lt;Target BeforeTargets="BeforeCompile"&gt;&lt;InternalsVisibleTo Include="%(InternalsVisibleToProject.Identity)" PublicKey="..." /&gt;</c>.
    /// Only an <c>Include</c> of <c>%(Type.Identity)</c> or <c>@(Type)</c> is expanded, from the items outside targets.
    /// </summary>
    private static IEnumerable<(XElement Item, string? Include)> BeforeCompileFriends(IReadOnlyList<XElement> roots, List<XElement> items, Func<string?, string?> expand)
    {
        var targets = roots.SelectMany(root => root.Descendants().Where(e => e.Name.LocalName == "Target"))
            .Where(t => (t.Attribute("BeforeTargets")?.Value ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(BeforeCompileTargets.Contains));
        foreach (var item in targets.SelectMany(t => t.Descendants().Where(e => e.Name.LocalName == "InternalsVisibleTo")))
        {
            if (ItemTransform().Match(item.Attribute("Include")?.Value.Trim() ?? string.Empty) is not { Success: true } match)
            {
                continue;
            }

            var type = match.Groups["type"].Value;
            foreach (var source in items.Where(e => e.Name.LocalName.Equals(type, StringComparison.OrdinalIgnoreCase)))
            {
                yield return (item, expand(source.Attribute("Include")?.Value));
            }
        }
    }

    /// <summary>The arguments of an <c>AssemblyAttribute</c> item, or <see langword="null"/> when one cannot be expanded.</summary>
    private static List<AssemblyAttributeArgument>? ReadArguments(XElement item, Func<string?, string?> expand)
    {
        var metadata = item.Attributes().Select(a => (Name: a.Name.LocalName, a.Value))
            .Concat(item.Elements().Select(e => (Name: e.Name.LocalName, e.Value)))
            .Where(m => !NotArguments.Contains(m.Name))
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

        var positional = new SortedDictionary<int, AssemblyAttributeArgument>();
        var named = new List<AssemblyAttributeArgument>();
        foreach (var (name, raw) in metadata)
        {
            if (name.EndsWith("_IsLiteral", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_TypeName", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (expand(raw) is not { } value || value.Contains("$(", StringComparison.Ordinal))
            {
                return null;
            }

            var isLiteral = IsTrue(metadata.GetValueOrDefault(name + "_IsLiteral"));
            if (ParameterIndex().Match(name) is { Success: true } match)
            {
                positional[int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture)] = new AssemblyAttributeArgument(value, isLiteral);
            }
            else
            {
                named.Add(new AssemblyAttributeArgument(value, isLiteral, name));
            }
        }

        return [.. positional.Values, .. named];
    }

    private static string? ExpandProperties(string value, IReadOnlyDictionary<string, string> properties, string projectName)
    {
        var unknown = false;
        var expanded = PropertyReference().Replace(value, match =>
        {
            var name = match.Groups["name"].Value;
            if (properties.TryGetValue(name, out var known))
            {
                return known;
            }

            if (name.Equals("AssemblyName", StringComparison.OrdinalIgnoreCase)
                || name.Equals("MSBuildProjectName", StringComparison.OrdinalIgnoreCase)
                || name.Equals("TargetName", StringComparison.OrdinalIgnoreCase)
                || name.Equals("RootNamespace", StringComparison.OrdinalIgnoreCase))
            {
                return name.Equals("MSBuildProjectName", StringComparison.OrdinalIgnoreCase)
                    ? projectName
                    : properties.GetValueOrDefault("AssemblyName") ?? projectName;
            }

            unknown = true;
            return match.Value;
        });
        return unknown ? null : expanded;
    }

    private static string? Metadata(XElement item, string name) =>
        item.Attribute(name)?.Value ?? item.Elements().FirstOrDefault(c => c.Name.LocalName == name)?.Value;

    private static bool IsFalse(string? value) => string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\$\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PropertyReference();

    [GeneratedRegex(@"^(?:%\((?<type>[A-Za-z_][A-Za-z0-9_.-]*)\.Identity\)|@\((?<type>[A-Za-z_][A-Za-z0-9_.-]*)\))$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 100)]
    private static partial Regex ItemTransform();

    [GeneratedRegex(@"^_Parameter(?<index>[0-9]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 100)]
    private static partial Regex ParameterIndex();

    /// <summary>Compares attributes by value, so the same item in a project and its props is generated once.</summary>
    private sealed class AttributeComparer : IEqualityComparer<AssemblyAttributeInfo>
    {
        public static AttributeComparer Instance { get; } = new();

        public bool Equals(AssemblyAttributeInfo? x, AssemblyAttributeInfo? y) =>
            ReferenceEquals(x, y) || (x is not null && y is not null && x.TypeName == y.TypeName && x.Arguments.SequenceEqual(y.Arguments));

        public int GetHashCode(AssemblyAttributeInfo obj) =>
            obj.Arguments.Aggregate(obj.TypeName.GetHashCode(StringComparison.Ordinal), (hash, argument) => HashCode.Combine(hash, argument));
    }
}
