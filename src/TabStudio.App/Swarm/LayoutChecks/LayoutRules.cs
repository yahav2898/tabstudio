namespace TabStudio.App.Swarm.LayoutChecks;

/// <summary>Which layout checks run. Each can be turned off, as Visual Studio's analyzers can.</summary>
/// <param name="ClippedText">Text clipped or trimmed by its layout (SWRM001).</param>
/// <param name="OffScreen">Elements outside the screen or their scroll viewer's extent (SWRM002).</param>
/// <param name="Overlap">Interactive elements that overlap (SWRM003).</param>
/// <param name="SmallTouchTarget">Touch targets smaller than a finger (SWRM004).</param>
/// <param name="LowContrast">Text with too little contrast (SWRM005).</param>
public sealed record LayoutCheckSettings(
    bool ClippedText = true,
    bool OffScreen = true,
    bool Overlap = true,
    bool SmallTouchTarget = true,
    bool LowContrast = true)
{
    /// <summary>Every check on.</summary>
    public static LayoutCheckSettings All { get; } = new();

    /// <summary>Whether a check runs.</summary>
    /// <param name="kind">The check.</param>
    public bool IsEnabled(LayoutIssueKind kind) => kind switch
    {
        LayoutIssueKind.ClippedText => ClippedText,
        LayoutIssueKind.OffScreen => OffScreen,
        LayoutIssueKind.Overlap => Overlap,
        LayoutIssueKind.SmallTouchTarget => SmallTouchTarget,
        LayoutIssueKind.LowContrast => LowContrast,
        _ => false,
    };

    /// <summary>The settings with one check turned on or off.</summary>
    /// <param name="kind">The check.</param>
    /// <param name="enabled">Whether it runs.</param>
    public LayoutCheckSettings With(LayoutIssueKind kind, bool enabled) => kind switch
    {
        LayoutIssueKind.ClippedText => this with { ClippedText = enabled },
        LayoutIssueKind.OffScreen => this with { OffScreen = enabled },
        LayoutIssueKind.Overlap => this with { Overlap = enabled },
        LayoutIssueKind.SmallTouchTarget => this with { SmallTouchTarget = enabled },
        LayoutIssueKind.LowContrast => this with { LowContrast = enabled },
        _ => this,
    };
}

/// <summary>The layout checks' codes, names and pages, as the Error List shows them.</summary>
public static class LayoutRules
{
    /// <summary>Where the checks are explained, one section per code.</summary>
    public static Uri Documentation { get; } = new("https://github.com/yahav2898/tabstudio/blob/main/docs/layout-checks.md");

    /// <summary>Every check, in the order of their codes.</summary>
    public static IReadOnlyList<LayoutIssueKind> All { get; } =
        [LayoutIssueKind.ClippedText, LayoutIssueKind.OffScreen, LayoutIssueKind.Overlap, LayoutIssueKind.SmallTouchTarget, LayoutIssueKind.LowContrast];

    /// <summary>A check's code, such as SWRM001 for clipped text.</summary>
    /// <param name="kind">The check.</param>
    public static string Code(LayoutIssueKind kind) => kind switch
    {
        LayoutIssueKind.ClippedText => "SWRM001",
        LayoutIssueKind.OffScreen => "SWRM002",
        LayoutIssueKind.Overlap => "SWRM003",
        LayoutIssueKind.SmallTouchTarget => "SWRM004",
        LayoutIssueKind.LowContrast => "SWRM005",
        _ => "SWRM000",
    };

    /// <summary>A check's name, such as "Clipped text".</summary>
    /// <param name="kind">The check.</param>
    public static string Title(LayoutIssueKind kind) => kind switch
    {
        LayoutIssueKind.ClippedText => "Clipped text",
        LayoutIssueKind.OffScreen => "Off screen",
        LayoutIssueKind.Overlap => "Overlapping controls",
        LayoutIssueKind.SmallTouchTarget => "Small touch targets",
        LayoutIssueKind.LowContrast => "Low contrast",
        _ => kind.ToString(),
    };

    /// <summary>What a check finds, in a sentence, for its setting.</summary>
    /// <param name="kind">The check.</param>
    public static string Description(LayoutIssueKind kind) => kind switch
    {
        LayoutIssueKind.ClippedText => "Text cut off, overflowing or trimmed with an ellipsis.",
        LayoutIssueKind.OffScreen => "Controls outside the screen, or outside what their scroll viewer can reach.",
        LayoutIssueKind.Overlap => "Controls that take taps lying over each other.",
        LayoutIssueKind.SmallTouchTarget => "Controls under 44 × 44 on phones and tablets, or 24 × 24 on desktops.",
        LayoutIssueKind.LowContrast => "Text below WCAG AA contrast: 4.5:1, or 3:1 for large text.",
        _ => string.Empty,
    };

    /// <summary>The page that explains a check.</summary>
    /// <param name="kind">The check.</param>
    public static Uri HelpLink(LayoutIssueKind kind) => new($"{Documentation}#swrm{Code(kind)[4..]}");

    /// <summary>The check a code names, if it is one of these.</summary>
    /// <param name="code">A code, such as SWRM003.</param>
    public static LayoutIssueKind? FromCode(string? code)
    {
        foreach (var kind in All)
        {
            if (string.Equals(Code(kind), code, StringComparison.Ordinal))
            {
                return kind;
            }
        }

        return null;
    }
}
