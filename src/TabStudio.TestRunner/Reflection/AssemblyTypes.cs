using System.Reflection;

namespace TabStudio.TestRunner.Reflection;

/// <summary>Lists the types of an assembly, even when some of them cannot be loaded.</summary>
internal static class AssemblyTypes
{
    /// <summary>Returns every type of an assembly that could be loaded, in metadata order.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="problems">Receives a message for types that could not be loaded, if given.</param>
    public static List<Type> Of(Assembly assembly, List<string>? problems = null)
    {
        try
        {
            return [.. assembly.GetTypes().OrderBy(t => t.MetadataToken)];
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var loaderException in ex.LoaderExceptions.OfType<Exception>().Take(3))
            {
                problems?.Add($"Some types of '{assembly.GetName().Name}' could not be loaded: {loaderException.Message}");
            }

            return [.. ex.Types.OfType<Type>().OrderBy(t => t.MetadataToken)];
        }
    }
}
