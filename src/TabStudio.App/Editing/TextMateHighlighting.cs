using System.Text;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace TabStudio.App.Editing;

/// <summary>Grammars and a color theme from installed VS Code extensions, which TextMate highlighting prefers to its own.</summary>
public interface ITextMateContributions
{
    /// <summary>The theme the editor's TextMate colors come from, or <see langword="null"/> for TabStudio's own.</summary>
    IRawTheme? Theme { get; }

    /// <summary>The root scope of the grammar an extension contributes for a file, or <see langword="null"/>.</summary>
    /// <param name="fileName">A file name, or an extension with its dot.</param>
    string? ScopeFor(string fileName);

    /// <summary>A grammar an extension contributes, or <see langword="null"/>.</summary>
    /// <param name="scopeName">The grammar's root scope.</param>
    IRawGrammar? GetGrammar(string scopeName);

    /// <summary>The scopes of the injection grammars extensions inject into a scope.</summary>
    /// <param name="scopeName">The scope injected into.</param>
    IReadOnlyCollection<string> GetInjections(string scopeName);
}

/// <summary>
/// Syntax highlighting for every language other than C#, from the TextMate grammars that VS Code uses,
/// colored with TabStudio's own palette, or with an installed extension's color theme. Grammars of installed
/// extensions take precedence over the ones TabStudio bundles.
/// </summary>
/// <remarks>
/// TextMate grammars need the Oniguruma regular expression engine, a small native library. If it cannot be
/// loaded on a device, the editor simply shows plain text for those files; nothing else is affected.
/// </remarks>
public static class TextMateHighlighting
{
    private static readonly Lazy<TabStudioRegistryOptions?> Options = new(CreateOptions);
    private static readonly List<WeakReference<TextMate.Installation>> Installations = [];

    /// <summary>Whether TextMate highlighting is available on this device.</summary>
    public static bool IsAvailable => Options.Value is not null;

    /// <summary>What installed extensions contribute, or <see langword="null"/> for TabStudio's bundled grammars and palette.</summary>
    public static ITextMateContributions? Contributions { get; set; }

    /// <summary>Installs highlighting for a file type on an editor.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="extension">The extension whose grammar to use, such as <c>.json</c>, or a file name an extension claims.</param>
    /// <returns>The installation, to dispose with the editor, or <see langword="null"/> when there is no grammar.</returns>
    public static TextMate.Installation? TryInstall(TextEditor editor, string extension)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (Options.Value is not { } options || options.GetScopeByExtension(extension) is not { } scope)
        {
            return null;
        }

        try
        {
            var installation = editor.InstallTextMate(options);
            TrySetGrammar(installation, scope);
            Track(installation);
            return installation;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or TypeInitializationException or BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Colors an editor with another grammar, as when an installed extension starts claiming the file, or with none.
    /// A grammar that cannot be loaded, such as an extension's broken one, leaves the editor as it was.
    /// </summary>
    /// <param name="installation">The editor's highlighting.</param>
    /// <param name="scope">The grammar's root scope, or <see langword="null"/> for plain text.</param>
    /// <returns>Whether the grammar is in use.</returns>
    public static bool TrySetGrammar(TextMate.Installation installation, string? scope)
    {
        ArgumentNullException.ThrowIfNull(installation);
        try
        {
            installation.SetGrammar(scope);
            return true;
        }
        catch (TMException)
        {
            return false;
        }
    }

    /// <summary>The grammar scope highlighting would use for a file, or <see langword="null"/> when it has none.</summary>
    /// <param name="extension">The extension, such as <c>.json</c>, or a file name an extension claims.</param>
    public static string? ScopeFor(string extension) => Options.Value?.GetScopeByExtension(extension);

    /// <summary>
    /// Colors every open editor's TextMate highlighting with the current theme, after the user chose another one.
    /// Call on the UI thread.
    /// </summary>
    public static void RefreshTheme()
    {
        if (Options.Value is not { } options)
        {
            return;
        }

        var theme = options.GetDefaultTheme();
        lock (Installations)
        {
            Installations.RemoveAll(reference => !reference.TryGetTarget(out _));
            foreach (var reference in Installations)
            {
                if (reference.TryGetTarget(out var installation))
                {
                    installation.SetTheme(theme);
                }
            }
        }
    }

    /// <summary>TabStudio's own editor theme, which the palette in <see cref="SyntaxTheme"/> matches.</summary>
    public static IRawTheme LoadDefaultTheme()
    {
        using var stream = typeof(TextMateHighlighting).Assembly.GetManifestResourceStream("Themes/TabStudioDark.json")
            ?? throw new InvalidOperationException("The editor theme is missing from this build.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return ThemeReader.ReadThemeSync(reader);
    }

    private static void Track(TextMate.Installation installation)
    {
        lock (Installations)
        {
            Installations.RemoveAll(reference => !reference.TryGetTarget(out _));
            Installations.Add(new WeakReference<TextMate.Installation>(installation));
        }
    }

    private static TabStudioRegistryOptions? CreateOptions()
    {
        try
        {
            TextMate.RegisterExceptionHandler(_ => { });
            return new TabStudioRegistryOptions();
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or IOException)
        {
            return null;
        }
    }

    /// <summary>The grammars bundled with TextMateSharp and those of installed extensions, with TabStudio's theme or the chosen one.</summary>
    private sealed class TabStudioRegistryOptions : IRegistryOptions
    {
        private readonly RegistryOptions _inner = new(ThemeName.DarkPlus);
        private readonly IRawTheme _theme = LoadDefaultTheme();

        public string? GetScopeByExtension(string extension) =>
            Contributions?.ScopeFor(extension) ?? _inner.GetScopeByExtension(extension);

        public IRawTheme GetTheme(string scopeName) => GetDefaultTheme();

        public IRawGrammar GetGrammar(string scopeName) =>
            Contributions?.GetGrammar(scopeName) ?? _inner.GetGrammar(scopeName);

        public ICollection<string> GetInjections(string scopeName)
        {
            var bundled = _inner.GetInjections(scopeName);
            var contributed = Contributions?.GetInjections(scopeName) ?? [];
            return contributed.Count == 0 ? bundled : [.. bundled ?? [], .. contributed];
        }

        public IRawTheme GetDefaultTheme() => Contributions?.Theme ?? _theme;
    }
}
