using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;

namespace TabStudio.Compilation.HotReload;

/// <summary>ApplyUpdate only changes IL; the host must dispatch the framework's metadata update handlers.</summary>
internal static class MetadataUpdateHandlers
{
    public static string Notify(AssemblyLoadContext context)
    {
        var handlers = context.Assemblies.Concat(AssemblyLoadContext.Default.Assemblies).Distinct()
            .SelectMany(a => a.GetCustomAttributes<MetadataUpdateHandlerAttribute>()).Select(a => a.HandlerType).Distinct().ToArray();
        var failures = new List<string>();
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
