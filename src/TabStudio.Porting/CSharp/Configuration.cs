using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.Output;

namespace TabStudio.Porting.CSharp;

/// <summary>
/// System.Configuration, which WPF and Windows Forms projects get with the Windows desktop: a ported project keeps it
/// only for code that reads the app's settings, through the System.Configuration.ConfigurationManager package.
/// </summary>
internal static class Configuration
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "ConfigurationManager", "ApplicationSettingsBase", "SettingsProvider", "SettingsBase", "ConfigurationSection",
        "ConfigurationElement", "UserScopedSettingAttribute", "ApplicationScopedSettingAttribute", "DefaultSettingValueAttribute",
        "SettingsProperty", "ConnectionStringSettings",
    };

    /// <summary>Whether a file uses System.Configuration; when it does, the new project gets the package.</summary>
    /// <param name="root">The file.</param>
    /// <param name="context">The port, which records the package.</param>
    public static bool Uses(CompilationUnitSyntax root, PortContext context)
    {
        var uses = root.DescendantNodes().OfType<SimpleNameSyntax>().Any(n => Types.Contains(n.Identifier.ValueText) || Types.Contains(n.Identifier.ValueText + "Attribute"));
        context.UsesConfiguration |= uses;
        return uses;
    }
}
