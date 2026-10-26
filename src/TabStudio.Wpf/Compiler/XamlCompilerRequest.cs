using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Wpf.Compiler;

/// <summary>
/// One pass of OpenSilver's XAML compiler over a project's XAML, as OpenSilver's build targets run its
/// <c>XamlPreprocessor</c> task: pass 1 before the C# compiler, for the classes' fields and
/// <c>InitializeComponent</c>; pass 2 after it, against the assembly pass 1 produced, for the code that builds the UI.
/// </summary>
public sealed record XamlCompilerRequest
{
    /// <summary>The project file, which messages are reported against.</summary>
    public required string ProjectFilePath { get; init; }

    /// <summary>The assembly's simple name, which the component addresses of its XAML use (<c>/WpfApp1;component/MainWindow.xaml</c>).</summary>
    public required string AssemblyName { get; init; }

    /// <summary>The project's root namespace.</summary>
    public string? RootNamespace { get; init; }

    /// <summary>The XAML files.</summary>
    public required IReadOnlyList<XamlCompilerInput> Files { get; init; }

    /// <summary>The assembly files the project compiles against: the framework's, OpenSilver's, its packages' and projects'.</summary>
    public required IReadOnlyList<string> References { get; init; }

    /// <summary>Where the generated C# files go, as the build's <c>obj</c> folder.</summary>
    public required string IntermediateDirectory { get; init; }

    /// <summary>Whether this is pass 2, which reads the project's own types from <see cref="FirstPassAssemblyPath"/>.</summary>
    public bool IsSecondPass { get; init; }

    /// <summary>For pass 2, the assembly pass 1 compiled: it must be named after <see cref="AssemblyName"/>.</summary>
    public string? FirstPassAssemblyPath { get; init; }
}

/// <summary>A XAML file to compile.</summary>
/// <param name="Item">The file as the project has it.</param>
/// <param name="ContentPath">
/// The file to read, when it is not <see cref="XamlItem.FilePath"/>: a copy that leaves out what OpenSilver does not
/// have, such as the WPF-only <c>StartupUri</c>. Its errors are reported in the project's file.
/// </param>
public sealed record XamlCompilerInput(XamlItem Item, string? ContentPath = null);

/// <summary>What one pass of the XAML compiler produced.</summary>
/// <param name="GeneratedFiles">The C# file of each XAML file that compiled, by the XAML file's full path.</param>
/// <param name="Diagnostics">Its errors and warnings, placed in the XAML files.</param>
/// <param name="Succeeded">Whether it reported no error.</param>
public sealed record XamlCompilerResult(IReadOnlyDictionary<string, string> GeneratedFiles, IReadOnlyList<BuildDiagnostic> Diagnostics, bool Succeeded);
