using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Platform;

namespace TabStudio.App.Previews;

/// <summary>
/// Points <c>avares://</c> addresses at the app being previewed, such as <c>avares://HelloAvalonia/Assets/logo.png</c>.
/// </summary>
/// <remarks>
/// Avalonia finds the assembly an <c>avares://</c> address names by its name, among the loaded assemblies, and keeps
/// what it found. Inside TabStudio every build of an app is a new assembly with the same name, and the earlier ones
/// stay loaded, so without this a restarted app would read its first build's images and fonts. Avalonia's
/// <see cref="AssetLoader.InvalidateAssemblyCache(string)"/> forgets an assembly but would find the first build
/// again; this puts the current one in its place. There is no public API for that, so it reaches Avalonia's asset
/// loader by name, for the Avalonia version TabStudio ships with, and a test fails if that ever changes.
/// </remarks>
internal static class RuntimeAssets
{
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly MethodInfo? GetLoaderMethod = typeof(AssetLoader).GetMethod("GetAssetLoader", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly Type? DescriptorType = typeof(AssetLoader).Assembly.GetType("Avalonia.Platform.Internal.AssemblyDescriptor");

    private static AssemblyLoadContext? _usedFor;
    private static int _usedAssemblyCount;

    /// <summary>Whether Avalonia's asset loader is the one this was written for, so that <see cref="Use"/> does its work.</summary>
    public static bool IsSupported => Cache() is not null && DescriptorType is not null;

    /// <summary>
    /// Makes <c>avares://</c> addresses read the resources of the build an app assembly belongs to: its own and those
    /// of the projects it references. UI thread only.
    /// </summary>
    /// <param name="assembly">An assembly of the app.</param>
    public static void UseFor(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var context = AssemblyLoadContext.GetLoadContext(assembly);
        var assemblies = context?.Assemblies.Where(a => !a.IsDynamic).ToList() ?? [assembly];
        if (ReferenceEquals(context, _usedFor) && assemblies.Count == _usedAssemblyCount)
        {
            return;
        }

        foreach (var built in assemblies)
        {
            Use(built);
        }

        _usedFor = context;
        _usedAssemblyCount = assemblies.Count;
    }

    /// <summary>Makes <c>avares://</c> addresses with the assembly's name read its resources. UI thread only.</summary>
    /// <param name="assembly">An assembly of the app, such as a new build of it.</param>
    public static void Use(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (assembly.GetName().Name is not { } name || Cache() is not { } cache || DescriptorType is null)
        {
            return;
        }

        cache[name] = Activator.CreateInstance(DescriptorType, assembly);
    }

    /// <summary>The asset loader's cache of assemblies by name, or null when Avalonia is not started or has changed.</summary>
    private static IDictionary? Cache()
    {
        try
        {
            var loader = GetLoaderMethod?.Invoke(null, null);
            var resolver = loader?.GetType().GetField("_assemblyDescriptorResolver", Instance)?.GetValue(loader);
            return resolver?.GetType().GetField("_assemblyNameCache", Instance)?.GetValue(resolver) as IDictionary;
        }
        catch (TargetInvocationException)
        {
            // No asset loader yet: Avalonia has not been started in this process.
            return null;
        }
    }
}
