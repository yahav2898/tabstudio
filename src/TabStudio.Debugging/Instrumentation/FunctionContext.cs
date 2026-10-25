using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TabStudio.Debugging.Instrumentation;

/// <summary>
/// The function (method, accessor, local function, lambda or the top-level statements) whose body the instrumenter
/// is rewriting, and what its hooks may refer to.
/// </summary>
internal sealed class FunctionContext
{
    public FunctionContext(
        int methodId,
        SyntaxTree tree,
        TextSpan span,
        ImmutableHashSet<TextSpan> scopes,
        bool isIterator,
        string? thisType,
        IReadOnlyList<string> faultValues)
    {
        MethodId = methodId;
        Tree = tree;
        Span = span;
        Scopes = scopes;
        IsIterator = isIterator;
        ThisType = thisType;
        FaultValues = faultValues;
        Frame = HookSyntax.FrameVariable(methodId);
        ExceptionVariable = HookSyntax.ExceptionVariable(methodId);
    }

    /// <summary>The method id in the debug info map.</summary>
    public int MethodId { get; }

    /// <summary>The name of the local holding the frame, passed to every hook.</summary>
    public string Frame { get; }

    /// <summary>The name of the exception variable in the fault filter.</summary>
    public string ExceptionVariable { get; }

    /// <summary>The tree that declares the function.</summary>
    public SyntaxTree Tree { get; }

    /// <summary>The span of the function's own declaration syntax.</summary>
    public TextSpan Span { get; }

    /// <summary>
    /// The declarations whose locals and parameters hooks may read: this function and, for lambdas, the functions
    /// around it. A local function or static lambda stops the chain, because reading an outer variable there would
    /// capture it and could change definite assignment at its call sites.
    /// </summary>
    public ImmutableHashSet<TextSpan> Scopes { get; }

    /// <summary>Whether the function is an iterator, which cannot have a <c>catch</c> around a <c>yield</c>.</summary>
    public bool IsIterator { get; }

    /// <summary>The type of <c>this</c> when hooks may read it, otherwise <see langword="null"/>.</summary>
    public string? ThisType { get; }

    /// <summary>The expressions the fault filter passes: <c>this</c> and the readable parameters.</summary>
    public IReadOnlyList<string> FaultValues { get; }

    /// <summary>Whether a local or parameter declared by <paramref name="owner"/> may be read by this function's hooks.</summary>
    /// <param name="owner">The symbol that declares the variable.</param>
    /// <param name="byReference">Whether the variable is a reference (<c>ref</c> local, <c>ref</c>/<c>in</c>/<c>out</c>
    /// parameter), which lambdas and local functions cannot capture.</param>
    public bool CanRead(ISymbol? owner, bool byReference)
    {
        if (owner is null)
        {
            return false;
        }

        foreach (var reference in owner.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree != Tree)
            {
                continue;
            }

            if (byReference ? reference.Span == Span : Scopes.Contains(reference.Span))
            {
                return true;
            }
        }

        return false;
    }
}
