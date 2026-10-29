using System.Reflection;
using System.Runtime.Loader;
using TabStudio.Forms.Runtime;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>
/// Finds the program's own assembly. TabStudio runs programs inside its own process, whose entry assembly is the IDE,
/// so the assembly comes from the Windows Forms app TabStudio runs, or from the program's My class.
/// </summary>
internal static class ProgramAssembly
{
    /// <summary>The program's assembly.</summary>
    /// <param name="myObject">An object of the program, such as <c>My.Application</c>, or <see langword="null"/>.</param>
    public static Assembly Find(object? myObject)
    {
        if (FormsRuntime.Current?.EntryAssembly is { } entry)
        {
            return entry;
        }

        var own = typeof(ProgramAssembly).Assembly;
        if (myObject?.GetType().Assembly is { } declaring && declaring != own)
        {
            return declaring;
        }

        return Assembly.GetEntryAssembly() ?? own;
    }

    /// <summary>
    /// Whether a program's assembly runs in a load context of its own, as TabStudio runs programs: the process's command
    /// line and entry assembly are then the IDE's, not the program's.
    /// </summary>
    /// <param name="assembly">The program's assembly.</param>
    public static bool IsHosted(Assembly assembly) =>
        AssemblyLoadContext.GetLoadContext(assembly) is { } context && context != AssemblyLoadContext.Default;
}
