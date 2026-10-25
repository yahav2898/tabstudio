using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;
using TabStudio.LiveTesting.Instrumentation;

namespace TabStudio.LiveTesting.Impact;

/// <summary>How a member changed between two versions of a file.</summary>
public enum MemberChangeKind
{
    /// <summary>The member's code changed.</summary>
    Changed,

    /// <summary>The member is new.</summary>
    Added,

    /// <summary>The member was removed, or its signature changed.</summary>
    Removed,
}

/// <summary>A member whose code changed between two versions of a file.</summary>
/// <param name="TypeName">The full name of its type, as reflection writes it.</param>
/// <param name="Name">The member's name, such as <c>Add</c>.</param>
/// <param name="Key">The member's key in its file.</param>
/// <param name="Kind">How it changed.</param>
public sealed record MemberChange(string TypeName, string Name, string Key, MemberChangeKind Kind);

/// <summary>
/// What changed in one source file between two builds: the members whose code changed, and whether anything
/// outside function bodies changed, which can affect any code.
/// </summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="IsStructural">
/// Whether code outside the tracked members changed: fields, constants, auto-properties, attributes of types, base
/// types, using directives, or the file was added or deleted.
/// </param>
/// <param name="Members">The members that changed, were added or were removed.</param>
public sealed record FileChange(string FilePath, bool IsStructural, IReadOnlyList<MemberChange> Members)
{
    /// <summary>Compares two versions of a file. Whitespace and comments are ignored.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="before">The earlier text, or <see langword="null"/> when the file is new.</param>
    /// <param name="after">The later text, or <see langword="null"/> when the file was deleted.</param>
    /// <param name="cancellationToken">Cancels the comparison.</param>
    /// <returns>The change, or <see langword="null"/> when the two versions have the same code.</returns>
    public static FileChange? Compare(string filePath, SourceText? before, SourceText? after, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (before is not null && after is not null && (ReferenceEquals(before, after) || before.ContentEquals(after)))
        {
            return null;
        }

        var (oldMembers, oldSkeleton) = Read(before, filePath, cancellationToken);
        var (newMembers, newSkeleton) = Read(after, filePath, cancellationToken);
        var changes = new List<MemberChange>();
        foreach (var (key, old) in oldMembers)
        {
            if (!newMembers.TryGetValue(key, out var current))
            {
                changes.Add(new MemberChange(old.TypeName, old.Name, key, MemberChangeKind.Removed));
            }
            else if (!string.Equals(old.Text, current.Text, StringComparison.Ordinal))
            {
                changes.Add(new MemberChange(current.TypeName, current.Name, key, MemberChangeKind.Changed));
            }
        }

        foreach (var (key, current) in newMembers)
        {
            if (!oldMembers.ContainsKey(key))
            {
                changes.Add(new MemberChange(current.TypeName, current.Name, key, MemberChangeKind.Added));
            }
        }

        var structural = before is null || after is null || !string.Equals(oldSkeleton, newSkeleton, StringComparison.Ordinal);
        return structural || changes.Count > 0 ? new FileChange(filePath, structural, changes) : null;
    }

    private static (Dictionary<string, MemberText> Members, string Skeleton) Read(SourceText? text, string filePath, CancellationToken cancellationToken)
    {
        if (text is null)
        {
            return (new Dictionary<string, MemberText>(StringComparer.Ordinal), string.Empty);
        }

        var root = CSharpSyntaxTree.ParseText(text, CSharpCompiler.CreateParseOptions(), filePath, cancellationToken).GetRoot(cancellationToken);
        var members = MemberKeys.Collect(root).ToDictionary(m => m.Key, StringComparer.Ordinal);
        return (members, MemberKeys.Skeleton(root));
    }
}
