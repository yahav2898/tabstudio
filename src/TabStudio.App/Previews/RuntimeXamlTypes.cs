using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Markup.Xaml;
using TabStudio.Compilation.Running;

namespace TabStudio.App.Previews;

/// <summary>
/// Keeps the types Avalonia's run-time XAML compiler knows in step with the app being previewed.
/// </summary>
/// <remarks>
/// <para>
/// The compiler builds its list of assemblies once, from those loaded when it first runs, and finds a type that XAML
/// names, such as <c>vm:MainViewModel</c> with <c>xmlns:vm="using:App.ViewModels"</c>, in the first assembly that
/// has it. The XAML namespaces of control libraries (their <c>[XmlnsDefinition]</c> attributes) are read at the same
/// moment. Inside TabStudio, apps are rebuilt and started again many times in one process, and every build is a new
/// assembly with the same name and types; the older ones stay loaded, since the code the compiler emitted references
/// them. Without this, a restarted app would get its previous build's types, and a control library first loaded
/// after the compiler started would have no XAML namespace.
/// </para>
/// <para>
/// So before XAML of an app is loaded, other programs' assemblies, such as earlier builds, are taken out of the
/// compiler's list, the app's are added, and the XAML namespaces are read again. Avalonia has no public API for this; it reaches the compiler's fields by
/// name, for the Avalonia version TabStudio ships with, and a test fails if they ever change.
/// </para>
/// </remarks>
internal static class RuntimeXamlTypes
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private static readonly Assembly Loader = typeof(AvaloniaRuntimeXamlLoader).Assembly;
    private static readonly Type? CompilerType = Loader.GetType("Avalonia.Markup.Xaml.XamlIl.AvaloniaXamlIlRuntimeCompiler");
    private static readonly Type? TypeSystemType = Loader.GetType("XamlX.IL.SreTypeSystem");
    private static readonly FieldInfo? TypeSystemField = CompilerType?.GetField("_sreTypeSystem", Static);
    private static readonly FieldInfo? XmlnsField = CompilerType?.GetField("_sreXmlns", Static);
    private static readonly FieldInfo? AssembliesField = TypeSystemType?.GetField("_assemblies", Instance);
    private static readonly MethodInfo? AddAssemblyMethod = TypeSystemType?.GetMethod("GetAssembly", Instance, [typeof(Assembly)]);

    /// <summary>The fields of the dynamic assembly the compiler emits loaded XAML into, and what it put there first.</summary>
    private static readonly FieldInfo?[] EmitterFields =
    [
        CompilerType?.GetField("_sreAsm", Static),
        CompilerType?.GetField("_sreBuilder", Static),
        CompilerType?.GetField("_sreContextType", Static),
        CompilerType?.GetField("_ignoresAccessChecksFromAttribute", Static),
    ];

    /// <summary>How many XAML files may be compiled into one dynamic assembly before the next starts a new one.</summary>
    private const int LoadsPerEmitter = 200;

    private static AssemblyLoadContext? _preparedFor;
    private static int _preparedAssemblyCount;
    private static int _loadsSinceNewEmitter;

    /// <summary>Whether the compiler's fields were found, so that <see cref="Prepare"/> does its work.</summary>
    public static bool IsSupported =>
        TypeSystemField is not null && XmlnsField is not null && AssembliesField is not null && AddAssemblyMethod is not null
        && EmitterFields.All(emitterField => emitterField is not null);

    /// <summary>
    /// Counts one more XAML file compiled at run time. The compiler puts all of them into one dynamic assembly that
    /// only grows; every so often, as the XAML designer renders on every pause in typing, the next file starts a new
    /// one, and the old one is collected once no preview uses it.
    /// </summary>
    public static void CountLoad()
    {
        if (!IsSupported || ++_loadsSinceNewEmitter < LoadsPerEmitter)
        {
            return;
        }

        ResetEmitter();
    }

    /// <summary>Makes the compiler emit the next XAML into a new dynamic assembly, created as that XAML is loaded.</summary>
    private static void ResetEmitter()
    {
        _loadsSinceNewEmitter = 0;
        foreach (var field in EmitterFields)
        {
            field!.SetValue(null, null);
        }
    }

    /// <summary>
    /// Makes the compiler find types in the build an app assembly belongs to, and not in other programs' builds,
    /// before XAML of that app is loaded. Cheap when nothing changed since the last call. UI thread only.
    /// </summary>
    /// <param name="assembly">An assembly of the app, loaded by TabStudio as a program.</param>
    public static void PrepareFor(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (!IsSupported)
        {
            return;
        }

        var context = AssemblyLoadContext.GetLoadContext(assembly);
        var count = AppDomain.CurrentDomain.GetAssemblies().Length;
        if (ReferenceEquals(context, _preparedFor) && count == _preparedAssemblyCount)
        {
            return;
        }

        Prepare(candidate => LoadedProgram.IsProgramAssembly(candidate) && AssemblyLoadContext.GetLoadContext(candidate) != context);

        // The code the compiler emits finds the app's assemblies by name, in the load context its dynamic assembly was
        // created in: another app, or another build of this one, needs a new dynamic assembly, created in its context.
        if (!ReferenceEquals(context, _preparedFor))
        {
            ResetEmitter();
        }

        _preparedFor = context;
        _preparedAssemblyCount = count;
    }

    /// <summary>
    /// Makes the compiler see the assemblies loaded now, except those <paramref name="isStale"/> picks, such as an
    /// earlier build of the app. UI thread only, as XAML is loaded.
    /// </summary>
    /// <param name="isStale">Picks the assemblies the compiler must no longer find types in.</param>
    public static void Prepare(Func<Assembly, bool> isStale)
    {
        ArgumentNullException.ThrowIfNull(isStale);
        if (!IsSupported)
        {
            return;
        }

        // Created the first time XAML is loaded; then the compiler reads every loaded assembly itself.
        var typeSystem = TypeSystemField!.GetValue(null) ?? Activator.CreateInstance(TypeSystemType!, nonPublic: true)!;
        var known = (IList)AssembliesField!.GetValue(typeSystem)!;
        var present = new HashSet<Assembly>();
        for (var i = known.Count - 1; i >= 0; i--)
        {
            if (AssemblyOf(known[i]) is not { } assembly)
            {
                continue;
            }

            if (isStale(assembly))
            {
                known.RemoveAt(i);
            }
            else
            {
                present.Add(assembly);
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !present.Contains(assembly) && !isStale(assembly))
            {
                AddAssembly(typeSystem, assembly);
            }
        }

        TypeSystemField.SetValue(null, typeSystem);

        // Read the XAML namespaces again on the next load, with the assemblies above.
        XmlnsField!.SetValue(null, null);
    }

    private static void AddAssembly(object typeSystem, Assembly assembly)
    {
        try
        {
            // The compiler reads every assembly's attributes for XAML namespaces, and fails on one it cannot read.
            _ = assembly.GetCustomAttributesData().Select(a => a.AttributeType).ToList();
            AddAssemblyMethod!.Invoke(typeSystem, [assembly]);
        }
        catch (Exception ex) when (ex is TargetInvocationException or TypeLoadException or FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            // An assembly whose types or attributes cannot all be loaded; XAML cannot use it either way.
        }
    }

    private static Assembly? AssemblyOf(object? xamlAssembly) =>
        xamlAssembly?.GetType().GetProperty("Assembly", BindingFlags.Public | BindingFlags.Instance)?.GetValue(xamlAssembly) as Assembly;
}
