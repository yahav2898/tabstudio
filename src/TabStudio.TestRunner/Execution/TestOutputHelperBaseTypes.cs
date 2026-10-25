using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// The class <see cref="DispatchProxy"/> derives each output helper from: one per load context the test framework
/// is loaded into, so that test sessions with their own copies of the framework each get a helper of their own.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DispatchProxy"/> generates every proxy whose base class lives in one load context into one dynamic
/// module, and a dynamic module binds an assembly name to the first assembly of that name it references. With
/// <see cref="TestOutputHelperProxy"/> as the base class, the proxies of every session would share TabStudio's
/// module, and a second session's proxy would implement the first session's <c>ITestOutputHelper</c>, which the
/// second session's test class cannot take.
/// </para>
/// <para>
/// So each framework load context gets an empty class derived from <see cref="TestOutputHelperProxy"/>, generated
/// into that context, and <see cref="DispatchProxy"/> builds that context's proxies in a module of its own.
/// </para>
/// </remarks>
internal static class TestOutputHelperBaseTypes
{
    private const string IgnoresAccessChecksTo = "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute";

    private static readonly ConditionalWeakTable<AssemblyLoadContext, Type> s_types = new();
    private static int s_count;

    /// <summary>The class to derive the output helper that implements <paramref name="interfaceType"/> from.</summary>
    /// <param name="interfaceType">The framework's <c>ITestOutputHelper</c>.</param>
    public static Type For(Type interfaceType)
    {
        var context = AssemblyLoadContext.GetLoadContext(interfaceType.Assembly);
        return context is null || context == AssemblyLoadContext.Default || !RuntimeFeature.IsDynamicCodeSupported
            ? typeof(TestOutputHelperProxy)
            : s_types.GetValue(context, Create);
    }

    private static Type Create(AssemblyLoadContext context)
    {
        // A dynamic assembly joins the contextual reflection context, which makes the framework's context its own.
        using var scope = context.EnterContextualReflection();
        var number = Interlocked.Increment(ref s_count).ToString(CultureInfo.InvariantCulture);
        var name = new AssemblyName("TabStudio.TestOutput" + number);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(name.Name!);
        AllowAccessTo(assembly, module, typeof(TestOutputHelperProxy).Assembly);

        var type = module.DefineType("TestOutputHelper", TypeAttributes.Public | TypeAttributes.Class, typeof(TestOutputHelperProxy));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        return type.CreateType();
    }

    /// <summary>
    /// Lets the dynamic assembly derive from TabStudio's internal class, the way <see cref="DispatchProxy"/> itself
    /// does: the runtime honors an <c>IgnoresAccessChecksToAttribute</c> that the assembly defines for itself.
    /// </summary>
    private static void AllowAccessTo(AssemblyBuilder assembly, ModuleBuilder module, Assembly target)
    {
        var attribute = module.DefineType(IgnoresAccessChecksTo, TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class, typeof(Attribute));
        var constructor = attribute.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.HasThis,
            [typeof(string)]);
        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!);
        il.Emit(OpCodes.Ret);

        var created = attribute.CreateType();
        assembly.SetCustomAttribute(new CustomAttributeBuilder(created.GetConstructor([typeof(string)])!, [target.GetName().Name]));
    }
}
