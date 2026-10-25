namespace TabStudio.Notebooks.Execution;

/// <summary>
/// The ids of diagnostics the notebook kernel reports itself, next to the C# compiler's <c>CS</c> diagnostics and
/// the in-process safety checks (<c>TS1001</c>, <c>TS1002</c>).
/// </summary>
public static class NotebookDiagnosticIds
{
    /// <summary>A cell references a NuGet package with <c>#r "nuget: ..."</c>, and no reference resolver is set up.</summary>
    public const string NuGetReferenceNotSupported = "TS3001";

    /// <summary>A cell uses a Polyglot Notebooks magic command, such as <c>#!set</c>, that TabStudio does not run yet.</summary>
    public const string MagicCommandNotSupported = "TS3002";

    /// <summary>A cell is in, or switches to, a language other than C#.</summary>
    public const string LanguageNotSupported = "TS3003";

    /// <summary>A <c>#r</c> reference could not be resolved, or assembly references are not set up.</summary>
    public const string ReferenceNotResolved = "TS3004";

    /// <summary>A cell uses <c>#load</c> to include a script file.</summary>
    public const string LoadNotSupported = "TS3005";

    /// <summary>TabStudio itself failed while preparing the cell; the message says why.</summary>
    public const string InternalError = "TS3006";
}
