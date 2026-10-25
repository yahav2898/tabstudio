using Microsoft.CodeAnalysis;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Notebooks.Formatting;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// How a <see cref="CSharpKernel"/> compiles and runs cells.
/// </summary>
public sealed record CSharpKernelOptions
{
    /// <summary>The namespaces every cell can use without a <c>using</c>, as in Polyglot Notebooks.</summary>
    public static IReadOnlyList<string> DefaultImports { get; } =
    [
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Text",
        "System.Threading.Tasks",
    ];

    /// <summary>
    /// The reference assemblies cells compile against: the in-memory .NET 10 references unless given, or the
    /// memory-mapped ones from <see cref="ReferenceAssemblies.LoadMapped"/> on devices short of memory.
    /// </summary>
    public IReadOnlyList<MetadataReference> FrameworkReferences { get; init; } = ReferenceAssemblies.Net100;

    /// <summary>The namespaces every cell can use without a <c>using</c>.</summary>
    public IReadOnlyList<string> Imports { get; init; } = DefaultImports;

    /// <summary>Supplies the assemblies behind <c>#r</c> lines. Without one, <c>#r</c> is reported as not supported yet.</summary>
    public INotebookReferenceResolver? ReferenceResolver { get; init; }

    /// <summary>Formats the values cells display and return.</summary>
    public ValueFormatter Formatter { get; init; } = ValueFormatter.Default;

    /// <summary>Whether nullable reference types are enabled in cells. Off unless set, as in Polyglot Notebooks.</summary>
    public bool Nullable { get; init; }

    /// <summary>Whether cells may use <c>unsafe</c> code.</summary>
    public bool AllowUnsafe { get; init; }

    /// <summary>The stack size, in bytes, of the thread a cell runs on: 16 MB unless given, for deeply recursive code.</summary>
    public int StackSize { get; init; } = ProgramRunner.DefaultStackSize;

    /// <summary>
    /// How long an interrupted cell may take to stop on its own, by observing its cancellation token, before the
    /// kernel stops waiting for it. Its output during this time still appears.
    /// </summary>
    public TimeSpan InterruptGracePeriod { get; init; } = TimeSpan.FromMilliseconds(500);
}
