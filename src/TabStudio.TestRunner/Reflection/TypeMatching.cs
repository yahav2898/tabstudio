namespace TabStudio.TestRunner.Reflection;

/// <summary>Compares types by full name, so framework types match whichever assembly or version they come from.</summary>
internal static class TypeMatching
{
    /// <summary>Whether a type, or one of its base types, has the given full name.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="fullName">A full name such as <c>Xunit.FactAttribute</c>.</param>
    public static bool Is(Type? type, string fullName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(NameOf(current), fullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a type is, derives from or implements a type with the given full name.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="fullName">A full name. Generic interfaces use their definition's name, such as <c>Xunit.IClassFixture`1</c>.</param>
    public static bool IsOrImplements(Type? type, string fullName) => Is(type, fullName) || FindInterface(type, fullName) is not null;

    /// <summary>Finds an interface a type implements by its full name.</summary>
    /// <param name="type">The type to search.</param>
    /// <param name="fullName">The interface's full name; for a generic interface, its definition's name.</param>
    /// <returns>The first matching interface, or <see langword="null"/>.</returns>
    public static Type? FindInterface(Type? type, string fullName)
    {
        if (type is null)
        {
            return null;
        }

        foreach (var candidate in type.GetInterfaces())
        {
            if (string.Equals(NameOf(candidate), fullName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return type.IsInterface && string.Equals(NameOf(type), fullName, StringComparison.Ordinal) ? type : null;
    }

    /// <summary>Finds every implemented interface with the given generic definition name, such as all <c>IClassFixture&lt;T&gt;</c>.</summary>
    /// <param name="type">The type to search.</param>
    /// <param name="fullName">The generic definition's full name.</param>
    public static List<Type> FindInterfaces(Type type, string fullName) =>
        type.GetInterfaces().Where(i => string.Equals(NameOf(i), fullName, StringComparison.Ordinal)).ToList();

    /// <summary>The full name of a type, or of its generic definition when it is a constructed generic type.</summary>
    /// <param name="type">The type.</param>
    public static string NameOf(Type type)
    {
        var definition = type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;
        return definition.FullName ?? definition.Name;
    }

    /// <summary>
    /// Whether a type cannot have instances: a C# <c>static</c> class (abstract and sealed in metadata) or a Visual
    /// Basic <c>Module</c> (sealed, without constructors, marked with <c>StandardModuleAttribute</c>).
    /// </summary>
    /// <param name="type">The type.</param>
    public static bool IsStatic(Type type) =>
        type.IsSealed && (type.IsAbstract || AttributeReader.Of(type).Exists(a => a.Is(FrameworkNames.VisualBasicStandardModule)));

    /// <summary>The type hierarchy from the root base class (excluding <see cref="object"/>) down to the type itself.</summary>
    /// <param name="type">The most derived type.</param>
    public static List<Type> HierarchyFromBase(Type type)
    {
        var chain = new List<Type>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            chain.Add(current);
        }

        chain.Reverse();
        return chain;
    }
}
