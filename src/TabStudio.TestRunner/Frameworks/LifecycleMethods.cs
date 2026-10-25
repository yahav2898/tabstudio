using System.Reflection;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>Finds setup and cleanup methods, such as NUnit's <c>[SetUp]</c> or MSTest's <c>[TestInitialize]</c>.</summary>
internal static class LifecycleMethods
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Returns the methods marked with an attribute, grouped by the class that declares them, from the root base
    /// class down. A virtual method that is marked in a base class and again on its override runs once.
    /// </summary>
    /// <param name="hierarchy">The class hierarchy, base class first.</param>
    /// <param name="attributeName">The attribute's full name.</param>
    public static List<List<MethodInfo>> ByLevel(List<Type> hierarchy, string attributeName)
    {
        var seen = new HashSet<MethodInfo>();
        var levels = new List<List<MethodInfo>>();
        foreach (var type in hierarchy)
        {
            var level = new List<MethodInfo>();
            foreach (var method in Marked(type, attributeName))
            {
                if (seen.Add(method.GetBaseDefinition()))
                {
                    level.Add(method);
                }
            }

            levels.Add(level);
        }

        return levels;
    }

    /// <summary>Returns the methods a type itself declares with an attribute, in declaration order.</summary>
    /// <param name="type">The type.</param>
    /// <param name="attributeName">The attribute's full name.</param>
    public static List<MethodInfo> Marked(Type type, string attributeName) =>
        type.GetMethods(Declared)
            .Where(m => AttributeReader.Of(m).Exists(a => a.Is(attributeName)))
            .OrderBy(m => m.MetadataToken)
            .ToList();
}
