using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// Evaluates MSBuild conditions, such as <c>'$(IncludePollyUsings)' == 'true'</c>,
/// <c>'$(TargetFramework)' != 'net462' and Exists('x.props')</c> or
/// <c>$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))</c>, as MSBuild does (see
/// <see cref="MsBuildExpressions"/>). A condition this cannot evaluate, such as one reading the registry, counts as true
/// where elements are kept or removed.
/// </summary>
public static class MsBuildConditions
{
    /// <summary>Evaluates a condition.</summary>
    /// <param name="condition">The condition's text.</param>
    /// <param name="properties">The properties known at that point.</param>
    /// <param name="baseDirectory">The folder relative paths in <c>Exists</c> start from.</param>
    /// <returns>The result, or <see langword="null"/> when the condition is not one this evaluates.</returns>
    public static bool? Evaluate(string condition, IReadOnlyDictionary<string, string> properties, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        return MsBuildExpressions.Condition(condition, properties, thisFile: null, baseDirectory);
    }

    /// <summary>
    /// Removes the elements of a props or targets file whose condition, or whose group's condition, is false, so that
    /// only the items MSBuild would add remain. Conditions that cannot be evaluated keep their elements.
    /// </summary>
    /// <param name="root">The file's root element; it is changed.</param>
    /// <param name="properties">The project's properties.</param>
    /// <param name="baseDirectory">The file's folder.</param>
    public static void RemoveFalse(XElement root, IReadOnlyDictionary<string, string> properties, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(root);
        foreach (var element in root.Descendants().Where(e => e.Attribute("Condition") is { } c && Evaluate(c.Value, properties, baseDirectory) == false).ToList())
        {
            if (element.Parent is not null)
            {
                element.Remove();
            }
        }
    }
}
