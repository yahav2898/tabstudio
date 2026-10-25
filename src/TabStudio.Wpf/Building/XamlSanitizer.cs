using TabStudio.Compilation.Wpf;
using TabStudio.Compilation.Wpf.Xaml;
using TabStudio.Core.Diagnostics;
using TabStudio.Wpf.Compiler;

namespace TabStudio.Wpf.Building;

/// <summary>
/// Gets a WPF project's XAML ready for OpenSilver's compiler, which stops at a member OpenSilver does not have: the
/// settings TabStudio takes care of itself are left out of a copy of the file, in place, so every line and column stays
/// where it is, and each one is reported with what happens to it.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>StartupUri</c> on the application: TabStudio's browser host opens that window (OpenSilver's
///   <c>Application</c> has no <c>StartupUri</c>).</item>
///   <item>On a window, the desktop settings OpenSilver's <c>Window</c> lacks: they have no effect in the web preview,
///   where the window fills the page; its <c>Background</c> is painted behind its content.</item>
/// </list>
/// Only what the restored OpenSilver lacks is left out, so a release that adds it gets it.
/// </remarks>
internal static class XamlSanitizer
{
    /// <summary>The XAML of a project as OpenSilver's compiler gets it.</summary>
    /// <param name="items">The project's XAML files.</param>
    /// <param name="types">The XAML types of the project's compilation.</param>
    /// <param name="copyDirectory">Where copies go, each under its path in the project.</param>
    /// <param name="projectName">The project, for the diagnostics.</param>
    public static (IReadOnlyList<XamlCompilerInput> Inputs, IReadOnlyList<BuildDiagnostic> Diagnostics) Prepare(
        IReadOnlyList<XamlItem> items,
        XamlTypeSystem types,
        string copyDirectory,
        string projectName)
    {
        var inputs = new List<XamlCompilerInput>(items.Count);
        var diagnostics = new List<BuildDiagnostic>();
        foreach (var item in items)
        {
            string text;
            try
            {
                text = File.ReadAllText(item.FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new BuildDiagnostic(WpfDiagnosticIds.InvalidXaml, DiagnosticSeverityLevel.Error, $"{item.RelativePath} could not be read: {ex.Message}", null, projectName));
                continue;
            }

            var document = XamlDocument.Parse(text);
            var removed = document.Root is { } root ? Removals(item, root, types, document, diagnostics, projectName) : [];
            if (removed.Count == 0)
            {
                inputs.Add(new XamlCompilerInput(item));
                continue;
            }

            var chars = text.ToCharArray();
            foreach (var attribute in removed)
            {
                for (var i = attribute.NameStart; i < attribute.End && i < chars.Length; i++)
                {
                    if (chars[i] is not ('\n' or '\r'))
                    {
                        chars[i] = ' ';
                    }
                }
            }

            var copy = Path.Combine(copyDirectory, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.WriteAllText(copy, new string(chars));
            inputs.Add(new XamlCompilerInput(item, copy));
        }

        return (inputs, diagnostics);
    }

    private static List<XamlAttribute> Removals(XamlItem item, XamlElement root, XamlTypeSystem types, XamlDocument document, List<BuildDiagnostic> diagnostics, string projectName)
    {
        var removed = new List<XamlAttribute>();
        var rootType = types.FindType(root.XmlNamespace, root.LocalName);
        if (item.Kind == XamlItemKind.ApplicationDefinition && root.Attribute("StartupUri") is { } startup
            && types.Application is { } application && types.FindMember(application, "StartupUri") is null)
        {
            removed.Add(startup);
        }

        if (types.Window is not { } window || !XamlTypeSystem.DerivesFrom(rootType, window))
        {
            return removed;
        }

        foreach (var attribute in root.Attributes)
        {
            if (attribute.Prefix.Length > 0 || attribute.IsNamespaceDeclaration || attribute.LocalName.Contains('.', StringComparison.Ordinal)
                || types.FindMember(rootType!, attribute.LocalName) is not null)
            {
                continue;
            }

            string? message = null;
            if (attribute.LocalName == WindowXamlSettings.Background)
            {
                message = WindowXamlSettings.BackgroundColor(root) is not null
                    ? "OpenSilver's Window has no Background, so TabStudio paints it behind the window's content."
                    : "OpenSilver's Window has no Background, and TabStudio paints only a plain color behind the window's content: set the brush on the window's root panel instead.";
            }
            else if (WindowXamlSettings.WithoutEffect.TryGetValue(attribute.LocalName, out var why))
            {
                message = $"{attribute.LocalName} has no effect in the web preview: {why}.";
            }
            else if (WindowXamlSettings.OnContent.Contains(attribute.LocalName))
            {
                message = $"OpenSilver's Window has no {attribute.LocalName}, so the window's content does not get it: set it on the controls, or on a style for them.";
            }

            if (message is null)
            {
                continue;
            }

            removed.Add(attribute);
            var (line, column) = document.LineAndColumn(attribute.NameStart);
            diagnostics.Add(new BuildDiagnostic(
                WpfDiagnosticIds.WindowSettingIgnored,
                attribute.LocalName == WindowXamlSettings.Background || WindowXamlSettings.WithoutEffect.ContainsKey(attribute.LocalName) ? DiagnosticSeverityLevel.Info : DiagnosticSeverityLevel.Warning,
                message,
                new DiagnosticLocation(item.FilePath, attribute.NameStart, attribute.NameEnd - attribute.NameStart, line, column),
                projectName));
        }

        return removed;
    }
}
