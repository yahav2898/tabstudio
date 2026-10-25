using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// The small assembly that gives cells their notebook features: <c>display(...)</c>, <c>DisplayAs</c> and the
/// interrupt token. Its main type is the globals object of every cell.
/// </summary>
/// <remarks>
/// <para>
/// The C# compiler needs metadata for the globals type of a script. A type compiled into TabStudio itself
/// would need its assembly file, and on Android the app's assemblies live inside the APK with no file path
/// (<see cref="System.Reflection.Assembly.Location"/> is empty). So the host is compiled from source here, in
/// memory, against the same reference assemblies as the cells, and loaded into each session from its bytes.
/// It talks to the kernel only through delegates of .NET types.
/// </para>
/// <para>
/// Every session loads its own copy into its own load context, so the static hook that <c>DisplayAs</c> uses
/// belongs to one session.
/// </para>
/// </remarks>
internal sealed class NotebookHostImage
{
    public const string AssemblyName = "TabStudio.NotebookHost";
    public const string Namespace = "TabStudio.Notebooks.Hosting";
    public const string ContextTypeName = Namespace + ".NotebookContext";

    private const string Source = """
        namespace TabStudio.Notebooks.Hosting
        {
            using System;
            using System.Threading;

            /// <summary>What every notebook cell can use in addition to its own code.</summary>
            public sealed class NotebookContext
            {
                private readonly Action<object, string[]> _display;
                private readonly Func<CancellationToken> _cancellationToken;

                public NotebookContext(Action<object, string[]> display, Func<CancellationToken> cancellationToken)
                {
                    _display = display;
                    _cancellationToken = cancellationToken;
                    NotebookDisplay.Sink = display;
                }

                /// <summary>Cancelled when the cell is interrupted. Long-running loops should check it.</summary>
                public CancellationToken CancellationToken => _cancellationToken();

                /// <summary>Shows a value below the cell. Pass a MIME type such as "text/html" to show a string as that type.</summary>
                public void display(object value, params string[] mimeTypes) => _display(value, mimeTypes ?? Array.Empty<string>());

                /// <summary>Shows a value below the cell. Pass a MIME type such as "text/html" to show a string as that type.</summary>
                public void Display(object value, params string[] mimeTypes) => _display(value, mimeTypes ?? Array.Empty<string>());
            }

            /// <summary>Extension methods for showing values, as in Polyglot Notebooks.</summary>
            public static class NotebookDisplay
            {
                internal static Action<object, string[]> Sink;

                /// <summary>Shows a string as the given MIME type, such as "text/html" or "text/markdown".</summary>
                public static void DisplayAs(this string content, string mimeType)
                {
                    var sink = Sink;
                    if (sink != null)
                    {
                        sink(content, new[] { mimeType });
                    }
                }

                /// <summary>Shows a value below the cell.</summary>
                public static void Display(this object value, params string[] mimeTypes)
                {
                    var sink = Sink;
                    if (sink != null)
                    {
                        sink(value, mimeTypes ?? Array.Empty<string>());
                    }
                }
            }
        }
        """;

    private NotebookHostImage(byte[] image, MetadataReference reference)
    {
        Image = image;
        Reference = reference;
    }

    /// <summary>The assembly's bytes.</summary>
    public byte[] Image { get; }

    /// <summary>The assembly as a reference for cell compilations.</summary>
    public MetadataReference Reference { get; }

    /// <summary>Compiles the host against a set of reference assemblies.</summary>
    /// <exception cref="InvalidOperationException">The host did not compile, which means the references are incomplete.</exception>
    public static NotebookHostImage Compile(IReadOnlyList<MetadataReference> frameworkReferences, CancellationToken cancellationToken)
    {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(Source, Encoding.UTF8), CSharpCompiler.CreateParseOptions(), "NotebookHost.cs", cancellationToken);
        var compilation = CSharpCompilation.Create(
            AssemblyName,
            [tree],
            frameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, deterministic: true));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, cancellationToken: cancellationToken);
        if (!result.Success)
        {
            var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString());
            throw new InvalidOperationException("The notebook host could not be compiled: " + string.Join(" ", errors));
        }

        var image = stream.ToArray();
        return new NotebookHostImage(image, MetadataReference.CreateFromImage(image));
    }
}
