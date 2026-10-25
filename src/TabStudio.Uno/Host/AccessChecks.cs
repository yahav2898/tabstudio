// Part of TabStudio's Uno Platform host, which TabStudio compiles on the device against the Uno Platform assemblies
// a project restores (see TabStudio.Uno.Hosting.UnoHostCompiler). Uno's own hosts (X11, Win32, the Linux frame
// buffer, Android) reach Uno's internal hosting API through InternalsVisibleTo; this host reaches the same API the
// way .NET's DispatchProxy reaches non-public interfaces: the compiler binds without accessibility checks, and these
// attributes tell the runtime (CoreCLR and Mono alike) to allow the access.

[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Uno")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Uno.UI")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Uno.UI.Composition")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Uno.UI.Dispatching")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Uno.Foundation")]
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Uno.Foundation.Logging")]

namespace System.Runtime.CompilerServices
{
    /// <summary>Lets this assembly use the non-public members of the named assembly.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName) => AssemblyName = assemblyName;

        public string AssemblyName { get; }
    }
}
