using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.App.Services;

/// <summary>
/// Makes TabStudio's Microsoft.VisualBasic.Forms the one every Visual Basic program and Microsoft.VisualBasic.Core
/// find. They bind to it by name from the default context, where the app's own reference loads it: .NET's binders
/// compare the simple name and require at least the version asked for (the assembly has .NET 10's, 10.0.0.0, above the
/// 4.0.0.0 Microsoft.VisualBasic.Core asks for), never the public key token of Microsoft's assembly. Should a request
/// still not bind, such as one for a newer version, the default context's Resolving event hands out the same assembly;
/// the runtime checks only the simple name of an assembly that event returns.
/// </summary>
internal static class VisualBasicRuntime
{
    private const string ShimName = "Microsoft.VisualBasic.Forms";

    private static int _registered;

    /// <summary>TabStudio's Microsoft.VisualBasic.Forms.</summary>
    public static Assembly Shim => typeof(global::Microsoft.VisualBasic.ApplicationServices.ApplicationBase).Assembly;

    /// <summary>Hands out TabStudio's Microsoft.VisualBasic.Forms for requests the binder does not satisfy. Safe to call more than once.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            AssemblyLoadContext.Default.Resolving += Resolve;
        }
    }

    private static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name) =>
        string.Equals(name.Name, ShimName, StringComparison.OrdinalIgnoreCase) ? Shim : null;
}
