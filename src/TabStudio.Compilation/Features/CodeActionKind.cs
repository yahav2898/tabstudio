namespace TabStudio.Compilation.Features;

/// <summary>Whether a code action fixes a diagnostic or refactors working code.</summary>
public enum CodeActionKind
{
    /// <summary>Fixes an error or warning, such as adding a missing <c>using</c> directive.</summary>
    QuickFix,

    /// <summary>Changes working code, such as extracting a method or converting a loop.</summary>
    Refactoring,
}
