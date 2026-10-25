using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.Web.RazorApps;

/// <summary>
/// Lets Blazor's Interactive Server render mode find an app's own components after TabStudio has run the app before,
/// or another app with assemblies of the same names, in its process.
/// </summary>
/// <remarks>
/// <para>
/// When a circuit starts, Blazor finds each interactive component's type by the name of its assembly among every
/// assembly loaded in the process (<c>AppDomain.GetAssemblies</c>), and takes the first it meets. Each run of an app
/// loads its own copies of the app's and ASP.NET Core's assemblies, and an earlier run's copies stay loaded: Android
/// cannot unload assemblies, and elsewhere they stay until the garbage collector frees them. So after a restart, such
/// as the live preview's after a save, Blazor would meet the earlier run's types first and refuse the circuit with
/// "The list of component operations is not valid".
/// </para>
/// <para>
/// The hosting library announces every host it builds on its diagnostic listener. When an app's host is built, this
/// fills the app's own caches of those types, <c>RootTypeCache</c> and <c>ComponentParametersTypeCache</c> (internal
/// to <c>Microsoft.AspNetCore.Components.Server</c>), with its own copies, for the assemblies whose names another run
/// has loaded too. Should a later ASP.NET Core change those caches, nothing is filled, and only restarted apps are
/// affected.
/// </para>
/// </remarks>
internal static class ServerComponentTypes
{
    private const string HostingListenerName = "Microsoft.Extensions.Hosting";
    private const string HostBuiltEvent = "HostBuilt";
    private const string HostInterface = "Microsoft.Extensions.Hosting.IHost";
    private const string ComponentsAssemblyName = "Microsoft.AspNetCore.Components";
    private const string ServerAssemblyName = "Microsoft.AspNetCore.Components.Server";
    private const string ComponentInterface = "Microsoft.AspNetCore.Components.IComponent";
    private const string ParameterAttribute = "Microsoft.AspNetCore.Components.ParameterAttribute";
    private const string RootTypeCache = "RootTypeCache";
    private const string ParametersTypeCache = "ComponentParametersTypeCache";

    private static readonly Lazy<IDisposable> Subscription = new(() => DiagnosticListener.AllListeners.Subscribe(new ListenerWatcher()));

    /// <summary>Fills the caches of every app whose host is built from now on; calling it again does nothing more.</summary>
    public static void Install() => _ = Subscription.Value;

    /// <summary>Fills the type caches of a host an app has just built, if another run shares names with its assemblies.</summary>
    /// <param name="host">The app's <c>IHost</c>, from its own copy of the hosting library.</param>
    internal static void Fill(object host)
    {
        var context = AssemblyLoadContext.GetLoadContext(host.GetType().Assembly);
        if (context is null || context == AssemblyLoadContext.Default)
        {
            return;
        }

        var shared = NamesLoadedElsewhere(context);
        if (shared.Count == 0
            || Services(host) is not { } services
            || Loaded(context, ComponentsAssemblyName)?.GetType(ComponentInterface) is not { } component
            || Loaded(context, ServerAssemblyName) is not { } server)
        {
            return;
        }

        var roots = new List<(string Assembly, string Name, Type Type)>();
        var parameters = new List<(string Assembly, string Name, Type Type)>();
        foreach (var assembly in ComponentAssemblies(context, shared))
        {
            foreach (var type in LoadableTypes(assembly).Where(t => IsComponent(t, component)))
            {
                roots.Add(Entry(type));
                parameters.AddRange(ParameterTypes(type).Where(p => shared.Contains(SimpleName(p))).Select(Entry));
            }
        }

        var cacheTypes = LoadableTypes(server).Where(t => !t.IsNested).ToList();
        FillCache(services, cacheTypes.Find(t => t.Name == RootTypeCache), roots);
        FillCache(services, cacheTypes.Find(t => t.Name == ParametersTypeCache), parameters);
    }

    /// <summary>The names of the assemblies other contexts than the app's and the default one have loaded, such as earlier runs.</summary>
    private static HashSet<string> NamesLoadedElsewhere(AssemblyLoadContext context) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => AssemblyLoadContext.GetLoadContext(a) is { } other && other != context && other != AssemblyLoadContext.Default)
            .Select(SimpleName)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The app's own copies of the shared names that can hold components: Blazor's and those that use it.</summary>
    private static IEnumerable<Assembly> ComponentAssemblies(AssemblyLoadContext context, HashSet<string> shared)
    {
        foreach (var name in shared.Order(StringComparer.Ordinal))
        {
            if (Load(context, name) is { } assembly
                && (name == ComponentsAssemblyName || assembly.GetReferencedAssemblies().Any(r => r.Name == ComponentsAssemblyName)))
            {
                yield return assembly;
            }
        }
    }

    /// <summary>The app's own copy of an assembly it has loaded already, such as Blazor's Server library once it adds its services.</summary>
    private static Assembly? Loaded(AssemblyLoadContext context, string name) =>
        context.Assemblies.FirstOrDefault(a => SimpleName(a) == name);

    /// <summary>The app's own copy of an assembly, loading it if it has not yet; <see langword="null"/> when the app has none.</summary>
    private static Assembly? Load(AssemblyLoadContext context, string name)
    {
        try
        {
            var assembly = context.LoadFromAssemblyName(new AssemblyName(name));
            return AssemblyLoadContext.GetLoadContext(assembly) == context ? assembly : null;
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            return null;
        }
    }

    private static bool IsComponent(Type type, Type component) =>
        type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false, FullName: not null } && component.IsAssignableFrom(type);

    /// <summary>The types of a component's parameters, whose values a circuit may receive by type name.</summary>
    private static IEnumerable<Type> ParameterTypes(Type component) =>
        component.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CustomAttributes.Any(a => a.AttributeType.FullName == ParameterAttribute))
            .Select(p => p.PropertyType)
            .Where(t => t is { ContainsGenericParameters: false, FullName: not null });

    private static (string Assembly, string Name, Type Type) Entry(Type type) => (SimpleName(type), type.FullName!, type);

    private static string SimpleName(Type type) => SimpleName(type.Assembly);

    private static string SimpleName(Assembly assembly) => assembly.GetName().Name ?? string.Empty;

    private static Type[] LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.OfType<Type>()];
        }
    }

    /// <summary>The app's services, from <c>IHost.Services</c>.</summary>
    private static IServiceProvider? Services(object host) =>
        host.GetType().GetInterfaces()
            .Where(i => i.FullName == HostInterface)
            .Select(i => i.GetProperty("Services"))
            .FirstOrDefault(p => p is not null)?.GetValue(host) as IServiceProvider;

    /// <summary>Puts types in the app's instance of one of Blazor's caches, keyed by assembly and type name as it looks them up.</summary>
    private static void FillCache(IServiceProvider services, Type? cacheType, List<(string Assembly, string Name, Type Type)> entries)
    {
        if (cacheType is null
            || entries.Count == 0
            || cacheType.GetNestedType("Key", BindingFlags.NonPublic) is not { } keyType
            || cacheType.GetField("_typeToKeyLookUp", BindingFlags.Instance | BindingFlags.NonPublic) is not { } field
            || services.GetService(cacheType) is not { } cache
            || field.GetValue(cache) is not IDictionary lookup)
        {
            return;
        }

        foreach (var (assembly, name, type) in entries)
        {
            if (Activator.CreateInstance(keyType, assembly, name) is { } key)
            {
                lookup[key] = type;
            }
        }
    }

    /// <summary>Watches for the hosting library's listener, which each app creates while it builds its host.</summary>
    private sealed class ListenerWatcher : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == HostingListenerName)
            {
                // The app disposes the listener once its host is built, which ends the subscription.
                _ = value.Subscribe(HostWatcher.Instance);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }

    /// <summary>Fills the caches of each host the hosting library announces.</summary>
    private sealed class HostWatcher : IObserver<KeyValuePair<string, object?>>
    {
        public static readonly HostWatcher Instance = new();

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key != HostBuiltEvent || value.Value is not { } host)
            {
                return;
            }

            try
            {
                Fill(host);
            }
            catch (Exception ex) when (ex is TargetInvocationException or MemberAccessException or ArgumentException or InvalidOperationException
                or NotSupportedException or TypeLoadException or ReflectionTypeLoadException or IOException or BadImageFormatException)
            {
                // The app starts all the same; Blazor looks its types up as it would without TabStudio.
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
