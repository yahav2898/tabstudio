using System.Text;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace TabStudio.App.Editing;

/// <summary>
/// Syntax highlighting for every language other than C#, from the TextMate grammars that VS Code uses,
/// colored with TabStudio's own palette.
/// </summary>
/// <remarks>
/// TextMate grammars need the Oniguruma regular expression engine, a small native library. If it cannot be
/// loaded on a device, the editor simply shows plain text for those files; nothing else is affected.
/// </remarks>
public static class TextMateHighlighting
{
    private static readonly Lazy<TabStudioRegistryOptions?> Options = new(CreateOptions);

    /// <summary>Whether TextMate highlighting is available on this device.</summary>
    public static bool IsAvailable => Options.Value is not null;

    /// <summary>Installs highlighting for a file type on an editor.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="extension">The extension whose grammar to use, such as <c>.json</c>.</param>
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
            installation.SetGrammar(scope);
            return installation;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or TypeInitializationException or BadImageFormatException)
        {
            return null;
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

    /// <summary>The grammars bundled with TextMateSharp, with TabStudio's theme instead of VS Code's.</summary>
    private sealed class TabStudioRegistryOptions : IRegistryOptions
    {
        private readonly RegistryOptions _inner = new(ThemeName.DarkPlus);
        private readonly IRawTheme _theme = LoadTheme();

        public string? GetScopeByExtension(string extension) => _inner.GetScopeByExtension(extension);

        public IRawTheme GetTheme(string scopeName) => _theme;

        public IRawGrammar GetGrammar(string scopeName) => _inner.GetGrammar(scopeName);

        public ICollection<string> GetInjections(string scopeName) => _inner.GetInjections(scopeName);

        public IRawTheme GetDefaultTheme() => _theme;

        private static IRawTheme LoadTheme()
        {
            using var stream = typeof(TextMateHighlighting).Assembly.GetManifestResourceStream("Themes/TabStudioDark.json")
                ?? throw new InvalidOperationException("The editor theme is missing from this build.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return ThemeReader.ReadThemeSync(reader);
        }
    }
}
