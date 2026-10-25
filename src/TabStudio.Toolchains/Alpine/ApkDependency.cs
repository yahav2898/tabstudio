namespace TabStudio.Toolchains.Alpine;

/// <summary>
/// One dependency atom from an index's <c>D:</c>, <c>p:</c> or <c>i:</c> line, such as <c>python3</c>,
/// <c>so:libc.musl-x86_64.so.1</c>, <c>python3~3.14</c> or <c>!py3-six</c>.
/// </summary>
/// <param name="Name">The package or provided name, such as <c>so:libffi.so.8</c>.</param>
/// <param name="Operation">The version operator, such as <c>&gt;=</c>, or <see langword="null"/> for any version.</param>
/// <param name="Version">The version in the constraint, or <see langword="null"/>.</param>
/// <param name="IsConflict">Whether the atom forbids the package (<c>!name</c>) rather than needing it.</param>
public sealed record ApkDependency(string Name, string? Operation, string? Version, bool IsConflict)
{
    private static readonly char[] Operators = ['<', '>', '=', '~'];

    /// <summary>Parses an atom.</summary>
    /// <param name="atom">The atom.</param>
    public static ApkDependency Parse(string atom)
    {
        ArgumentException.ThrowIfNullOrEmpty(atom);
        var conflict = atom[0] == '!';
        var text = conflict ? atom[1..] : atom;
        var start = text.IndexOfAny(Operators);
        if (start <= 0)
        {
            return new ApkDependency(text, null, null, conflict);
        }

        var end = start;
        while (end < text.Length && Array.IndexOf(Operators, text[end]) >= 0)
        {
            end++;
        }

        return new ApkDependency(text[..start], text[start..end], text[end..], conflict);
    }

    /// <summary>Parses a space-separated list of atoms.</summary>
    /// <param name="atoms">The list, or <see langword="null"/>.</param>
    public static IReadOnlyList<ApkDependency> ParseList(string? atoms) =>
        string.IsNullOrWhiteSpace(atoms)
            ? []
            : atoms.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToList();

    /// <summary>Whether a version satisfies the atom's constraint; any version does when it has none.</summary>
    /// <param name="version">The version.</param>
    public bool AcceptsVersion(string? version) =>
        Operation is null || Version is null || (version is not null && ApkVersion.Satisfies(version, Operation, Version));

    /// <inheritdoc />
    public override string ToString() => $"{(IsConflict ? "!" : string.Empty)}{Name}{Operation}{Version}";
}
