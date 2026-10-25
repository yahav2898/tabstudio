namespace TabStudio.Compilation.Features;

/// <summary>A parameter of a <see cref="SignatureItem"/>.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Display">The parameter as shown in the signature, such as <c>int count = 1</c>.</param>
/// <param name="Documentation">Its description from the documentation comment, or an empty string.</param>
/// <param name="IsOptional">Whether the argument can be left out, because it has a default value or is a <c>params</c> array.</param>
public sealed record SignatureParameter(string Name, IReadOnlyList<TaggedRun> Display, string Documentation, bool IsOptional);

/// <summary>Signature help: the overloads of the call the caret is in, and the argument being typed.</summary>
/// <param name="Signatures">The overloads, fewest parameters first.</param>
/// <param name="ActiveSignature">Index of the overload that best matches the arguments typed so far.</param>
/// <param name="ArgumentIndex">Zero-based index of the argument the caret is in.</param>
/// <param name="ArgumentName">The name of that argument when it is a named argument (<c>name: value</c>), otherwise <see langword="null"/>.</param>
/// <param name="ApplicableSpanStart">Start of the argument list, just after its opening parenthesis or bracket.</param>
/// <param name="ApplicableSpanLength">Length of the argument list, up to its closing parenthesis or bracket. Signature help closes when the caret leaves it.</param>
public sealed record SignatureHelpResult(
    IReadOnlyList<SignatureItem> Signatures,
    int ActiveSignature,
    int ArgumentIndex,
    string? ArgumentName,
    int ApplicableSpanStart,
    int ApplicableSpanLength)
{
    /// <summary>The parameter the caret is on in the active overload, or -1 when there is none.</summary>
    public int ActiveParameter => Signatures[ActiveSignature].ActiveParameter;
}
