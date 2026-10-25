using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>An error, warning or message shown in the Quick Info tooltip, as Visual Studio shows it under the signature.</summary>
/// <param name="diagnostic">The diagnostic.</param>
public sealed class QuickInfoDiagnosticViewModel(BuildDiagnostic diagnostic)
{
    /// <summary>The diagnostic id, such as CS0103.</summary>
    public string Code { get; } = diagnostic.Id;

    /// <summary>The message.</summary>
    public string Message { get; } = diagnostic.Message;

    /// <summary>Whether it is an error.</summary>
    public bool IsError { get; } = diagnostic.Severity == DiagnosticSeverityLevel.Error;

    /// <summary>Whether it is a warning.</summary>
    public bool IsWarning { get; } = diagnostic.Severity == DiagnosticSeverityLevel.Warning;

    /// <summary>Whether it is a message.</summary>
    public bool IsMessage { get; } = diagnostic.Severity <= DiagnosticSeverityLevel.Info;
}

/// <summary>A section of the Quick Info tooltip under the signature, such as the documentation.</summary>
/// <param name="runs">Its text.</param>
public sealed class QuickInfoSectionViewModel(IReadOnlyList<StyledRun> runs)
{
    /// <summary>Its text.</summary>
    public IReadOnlyList<StyledRun> Runs { get; } = runs;
}

/// <summary>
/// The Quick Info tooltip: the signature of the symbol under the pointer or caret, colored like code, its
/// documentation, and any error or warning at the same place.
/// </summary>
public sealed class QuickInfoViewModel
{
    private QuickInfoViewModel(QuickInfoResult? info, IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        Kind = info?.Kind ?? SymbolGlyph.Other;
        var sections = info?.Sections ?? [];
        var signature = sections.FirstOrDefault(s => s.Kind == QuickInfoSectionKind.Description);
        Signature = signature is null ? [] : StyledRun.From(signature.Runs);
        Sections = sections
            .Where(s => !ReferenceEquals(s, signature))
            .Select(s => StyledRun.From(Trim(s.Runs)))
            .Where(runs => runs.Count > 0)
            .Select(runs => new QuickInfoSectionViewModel(runs))
            .ToList();
        Diagnostics = diagnostics.Select(d => new QuickInfoDiagnosticViewModel(d)).ToList();
        SpanStart = info?.SpanStart ?? diagnostics.FirstOrDefault()?.Location?.Start ?? 0;
        SpanLength = info?.SpanLength ?? diagnostics.FirstOrDefault()?.Location?.Length ?? 0;
    }

    /// <summary>The icon of the symbol.</summary>
    public SymbolGlyph Kind { get; }

    /// <summary>The signature, such as <c>string Greeter.Greet()</c>.</summary>
    public IReadOnlyList<StyledRun> Signature { get; }

    /// <summary>Whether there is a signature.</summary>
    public bool HasSignature => Signature.Count > 0;

    /// <summary>The documentation, returns, remarks and other sections, in order.</summary>
    public IReadOnlyList<QuickInfoSectionViewModel> Sections { get; }

    /// <summary>The errors and warnings at the same place.</summary>
    public IReadOnlyList<QuickInfoDiagnosticViewModel> Diagnostics { get; }

    /// <summary>Whether there are errors or warnings to show.</summary>
    public bool HasDiagnostics => Diagnostics.Count > 0;

    /// <summary>Whether a line separates the symbol from the errors and warnings.</summary>
    public bool HasDivider => HasDiagnostics && (HasSignature || Sections.Count > 0);

    /// <summary>Start of the text the tooltip is about. It stays open while the pointer is over that text.</summary>
    public int SpanStart { get; }

    /// <summary>Length of the text the tooltip is about.</summary>
    public int SpanLength { get; }

    /// <summary>Builds the tooltip, or returns <see langword="null"/> when there is nothing to show.</summary>
    /// <param name="info">Quick Info from the language service, if any.</param>
    /// <param name="diagnostics">Errors and warnings at the position.</param>
    public static QuickInfoViewModel? Create(QuickInfoResult? info, IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return info is null && diagnostics.Count == 0 ? null : new QuickInfoViewModel(info, diagnostics);
    }

    /// <summary>Whether a position is inside the text the tooltip is about.</summary>
    /// <param name="offset">A document offset.</param>
    public bool Covers(int offset) => offset >= SpanStart && offset <= SpanStart + SpanLength;

    /// <summary>Drops the line breaks and spaces documentation sections often start or end with.</summary>
    private static IEnumerable<TaggedRun> Trim(IReadOnlyList<TaggedRun> runs)
    {
        var start = 0;
        var end = runs.Count;
        while (start < end && string.IsNullOrWhiteSpace(runs[start].Text))
        {
            start++;
        }

        while (end > start && string.IsNullOrWhiteSpace(runs[end - 1].Text))
        {
            end--;
        }

        for (var i = start; i < end; i++)
        {
            yield return runs[i];
        }
    }
}
