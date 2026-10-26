using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;

namespace TabStudio.Compilation.HotReload;

/// <summary>ApplyUpdate only changes IL; the host must dispatch the framework's metadata update handlers.</summary>
internal static class MetadataUpdateHandlers
{
    public static string Notify(AssemblyLoadContext context)
    {
        var failures = new List<string>();
        var handlers = new HashSet<Type>();
        foreach (var assembly in context.Assemblies.Concat(AssemblyLoadContext.Default.Assemblies).Distinct())
        {
            try
            {
                foreach (var attribute in assembly.GetCustomAttributes<MetadataUpdateHandlerAttribute>())
                    handlers.Add(attribute.HandlerType);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                failures.Add($" {assembly.GetName().Name}: {ex.GetBaseException().Message}");
            }
        }
        // All caches must be invalidated before any framework (ASP.NET Core/Blazor included) refreshes its UI.
        foreach (var name in new[] { "ClearCache", "UpdateApplication" })
        {
            foreach (var type in handlers)
            {
                try
                {
                    type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                        binder: null, types: [typeof(Type[])], modifiers: null)?.Invoke(null, [null]);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    failures.Add($" {type.Name}.{name}: {ex.GetBaseException().Message}");
                }
            }
        }

        return string.Concat(failures);
    }
}
