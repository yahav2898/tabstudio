using TabStudio.Core.Diagnostics;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.Wpf;

/// <summary>
/// The second pass of OpenSilver's XAML compiler, which completes a WPF project's build once its first pass compiled:
/// the code that builds each XAML file's user interface. <c>TabStudio.Wpf</c> runs OpenSilver's own compiler for it;
/// see <see cref="RoslynWorkspaceHost.SetWpfXamlCompiler"/>.
/// </summary>
public interface IWpfXamlCompiler
{
    /// <summary>Compiles a project's XAML against its first build, returning the compilation that completes it.</summary>
    /// <param name="request">The project and its first build.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    Task<WpfSecondPass> CompileAsync(WpfSecondPassRequest request, CancellationToken cancellationToken);
}

/// <summary>A WPF project whose first pass compiled, for the second pass of the XAML compiler.</summary>
public sealed record WpfSecondPassRequest
{
    /// <summary>The project.</summary>
    public required TsProject Project { get; init; }

    /// <summary>Its compilation, with the first pass's code of every XAML file.</summary>
    public required RoslynCompilation Compilation { get; init; }

    /// <summary>The assembly that compilation produced, whose types the XAML compiler reads.</summary>
    public required ReadOnlyMemory<byte> FirstPassImage { get; init; }
}

/// <summary>The outcome of the second pass.</summary>
/// <param name="Compilation">
/// The project's compilation with the second pass's code in place of the first's, ready to emit; <see langword="null"/>
/// when the XAML did not compile.
/// </param>
/// <param name="Diagnostics">The XAML compiler's errors, warnings and messages, in the XAML files.</param>
public sealed record WpfSecondPass(RoslynCompilation? Compilation, IReadOnlyList<BuildDiagnostic> Diagnostics);
