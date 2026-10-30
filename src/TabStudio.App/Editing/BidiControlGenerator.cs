using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;
using TabStudio.App.Theming;

namespace TabStudio.App.Editing;

/// <summary>
/// Shows the invisible characters that change the direction of text (U+202A to U+202E, U+2066 to U+2069, and the
/// left-to-right, right-to-left and Arabic letter marks) as their names, such as RLO, in place of the character.
/// Unseen, they let code read differently from how it compiles ("Trojan Source", CVE-2021-42574), and they make
/// Hebrew and Arabic text hard to edit.
/// </summary>
public sealed class BidiControlGenerator : VisualLineElementGenerator
{
    private static IBrush MarkBrush => ThemeBrushes.Editor("Editor.Breakpoint");

    /// <summary>The short name of a direction control character, or <see langword="null"/> for any other character.</summary>
    /// <param name="c">The character.</param>
    public static string? NameOf(char c) => c switch
    {
        '‪' => "LRE",
        '‫' => "RLE",
        '‬' => "PDF",
        '‭' => "LRO",
        '‮' => "RLO",
        '⁦' => "LRI",
        '⁧' => "RLI",
        '⁨' => "FSI",
        '⁩' => "PDI",
        '‎' => "LRM",
        '‏' => "RLM",
        '؜' => "ALM",
        _ => null,
    };

    /// <inheritdoc />
    public override int GetFirstInterestedOffset(int startOffset)
    {
        var end = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
        var text = CurrentContext.GetText(startOffset, end - startOffset);
        for (var i = 0; i < text.Count; i++)
        {
            if (NameOf(text.Text[text.Offset + i]) is not null)
            {
                return startOffset + i;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public override VisualLineElement? ConstructElement(int offset) =>
        NameOf(CurrentContext.Document.GetCharAt(offset)) is { } name ? new MarkElement(name) : null;

    /// <summary>The character's name, standing for the one character it replaces.</summary>
    private sealed class MarkElement(string name) : FormattedTextElement(name, 1)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            TextRunProperties.SetForegroundBrush(MarkBrush);
            TextRunProperties.SetFontRenderingEmSize(Math.Max(8, TextRunProperties.FontRenderingEmSize - 2));
            return base.CreateTextRun(startVisualColumn, context);
        }
    }
}
