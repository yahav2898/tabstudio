using System.Reflection;
using System.Runtime.Loader;

namespace TabStudio.Wpf.Compiler;

/// <summary>
/// Loads OpenSilver's XAML compiler from its package's <c>tools</c> folder, beside the version of Mono.Cecil it was
/// built with, as MSBuild loads a task assembly. The MSBuild assemblies come from TabStudio, so the build engine
/// TabStudio hands the task is the one it expects.
/// </summary>
/// <param name="toolsDirectory">The package's <c>tools</c> folder.</param>
internal sealed class CompilerLoadContext(string toolsDirectory) : AssemblyLoadContext($"OpenSilver XAML compiler ({toolsDirectory})")
{
    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not { } name || name.StartsWith("Microsoft.Build", StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.Combine(toolsDirectory, name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}
