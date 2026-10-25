using System.Diagnostics.CodeAnalysis;

namespace TabStudio.Notebooks;

/// <summary>
/// The names notebooks use for cell languages, and the aliases Polyglot Notebooks accepts for them.
/// </summary>
/// <remarks>
/// Names are Polyglot Notebooks kernel names: <c>csharp</c> for C#, <c>fsharp</c> for F#, <c>pwsh</c> for
/// PowerShell and so on. They are what a <c>.dib</c> file writes after <c>#!</c> and what an <c>.ipynb</c> file
/// stores in a cell's <c>polyglot_notebook.kernelName</c> metadata. Aliases such as <c>C#</c> and <c>cs</c>
/// are accepted wherever a name is.
/// </remarks>
public static class NotebookLanguages
{
    /// <summary>C#, the language TabStudio's kernel runs.</summary>
    public const string CSharp = "csharp";

    /// <summary>F#.</summary>
    public const string FSharp = "fsharp";

    /// <summary>PowerShell.</summary>
    public const string PowerShell = "pwsh";

    /// <summary>JavaScript.</summary>
    public const string JavaScript = "javascript";

    /// <summary>HTML.</summary>
    public const string Html = "html";

    /// <summary>The language of Markdown cells.</summary>
    public const string Markdown = "markdown";

    /// <summary>The language of raw cells, which are never run.</summary>
    public const string Raw = "raw";

    private static readonly KnownLanguage[] Known =
    [
        new(CSharp, "C#", "c#", "C#", "cs"),
        new(FSharp, "F#", "f#", "F#", "fs"),
        new(PowerShell, "PowerShell", "powershell"),
        new(JavaScript, "JavaScript", "js"),
        new(Html, "HTML"),
        new("sql", "SQL"),
        new("kql", "KQL"),
        new("mermaid", "Mermaid"),
        new("http", "HTTP"),
        new("value", "Value"),
        new(Markdown, "Markdown", "md"),
    ];

    private static readonly Dictionary<string, KnownLanguage> ByNameOrAlias = CreateLookup();

    /// <summary>Returns the canonical name for a language name or alias, such as <c>csharp</c> for <c>C#</c>.</summary>
    /// <param name="nameOrAlias">A name or alias, in any letter case.</param>
    /// <returns>The canonical name, or <paramref name="nameOrAlias"/> unchanged when the language is not known.</returns>
    public static string Normalize(string nameOrAlias)
    {
        ArgumentNullException.ThrowIfNull(nameOrAlias);
        return TryNormalize(nameOrAlias, out var name) ? name : nameOrAlias;
    }

    /// <summary>Looks up the canonical name for a language name or alias.</summary>
    /// <param name="nameOrAlias">A name or alias, in any letter case.</param>
    /// <param name="name">The canonical name, when the language is known.</param>
    /// <returns>Whether the language is one Polyglot Notebooks knows.</returns>
    public static bool TryNormalize(string nameOrAlias, [NotNullWhen(true)] out string? name)
    {
        ArgumentNullException.ThrowIfNull(nameOrAlias);
        name = ByNameOrAlias.TryGetValue(nameOrAlias, out var language) ? language.Name : null;
        return name is not null;
    }

    /// <summary>Whether a language name or alias means C#.</summary>
    /// <param name="nameOrAlias">A name or alias, in any letter case.</param>
    public static bool IsCSharp(string nameOrAlias) =>
        TryNormalize(nameOrAlias, out var name) && name == CSharp;

    /// <summary>The name to show people for a language, such as <c>C#</c> for <c>csharp</c>.</summary>
    /// <param name="nameOrAlias">A name or alias, in any letter case.</param>
    /// <returns>The display name, or <paramref name="nameOrAlias"/> unchanged when the language is not known.</returns>
    public static string GetDisplayName(string nameOrAlias)
    {
        ArgumentNullException.ThrowIfNull(nameOrAlias);
        return ByNameOrAlias.TryGetValue(nameOrAlias, out var language) ? language.DisplayName : nameOrAlias;
    }

    /// <summary>Maps a Jupyter <c>kernelspec.language</c> value such as <c>C#</c> to a kernel name.</summary>
    /// <param name="language">The Jupyter language name, or <see langword="null"/>.</param>
    internal static string? FromJupyterLanguage(string? language) =>
        language is null ? null : Normalize(language);

    private static Dictionary<string, KnownLanguage> CreateLookup()
    {
        var lookup = new Dictionary<string, KnownLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in Known)
        {
            lookup[language.Name] = language;
            lookup[language.DisplayName] = language;
            foreach (var alias in language.Aliases)
            {
                lookup[alias] = language;
            }
        }

        return lookup;
    }

    private sealed record KnownLanguage(string Name, string DisplayName, params string[] Aliases);
}
