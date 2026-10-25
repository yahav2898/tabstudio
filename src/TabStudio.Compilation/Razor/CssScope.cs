using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Razor;

/// <summary>
/// The scope of a scoped CSS file, such as <c>b-nz1yiwl0um</c>: the attribute the Razor compiler adds to every element
/// of the component, and the CSS rewriter adds to every selector of its stylesheet.
/// </summary>
/// <remarks>
/// It is computed like the SDK's <c>ComputeCssScope</c> task, from the file's path in the project and the assembly name,
/// so a component keeps the same scope in TabStudio as in a build with the .NET SDK on Linux.
/// </remarks>
public static class CssScope
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>The scope of a project's scoped CSS file.</summary>
    /// <param name="project">The project.</param>
    /// <param name="cssFilePath">Full path of the file, such as <c>Components/Layout/MainLayout.razor.css</c> in the project.</param>
    public static string Compute(TsProject project, string cssFilePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(cssFilePath);
        var relative = Path.GetRelativePath(project.DirectoryPath, cssFilePath);
        return Compute(project.GetProperty("AssemblyName") ?? project.Name, relative);
    }

    /// <summary>The scope of a scoped CSS file, from the assembly name and the file's path relative to the project.</summary>
    /// <param name="assemblyName">The project's assembly name, the SDK's <c>TargetName</c>.</param>
    /// <param name="relativePath">The file's path relative to the project folder, as the SDK's item spec has it.</param>
    public static string Compute(string assemblyName, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);
        ArgumentNullException.ThrowIfNull(relativePath);

        // The task lower-cases the item spec and doubles backslashes, as it always has; keep it bit for bit.
        var normalized = relativePath.ToLowerInvariant().Replace("\\", "//", StringComparison.Ordinal);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized + assemblyName));

        // The first nine bytes as a little-endian signed number, in base 36, least significant digit first.
        var dividend = BigInteger.Abs(new BigInteger(hash.AsSpan(0, 9)));
        var scope = new StringBuilder("b-", 12);
        for (var i = 0; i < 10; i++)
        {
            dividend = BigInteger.DivRem(dividend, 36, out var remainder);
            scope.Append(Alphabet[(int)remainder]);
        }

        return scope.ToString();
    }
}
