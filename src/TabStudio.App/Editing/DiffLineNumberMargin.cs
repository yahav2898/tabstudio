using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Editing;

namespace TabStudio.App.Editing;

/// <summary>
/// The line numbers of an inline diff, as in Visual Studio: the line's number in the unmodified version, its
/// number in the working copy, and a <c>+</c> or <c>−</c> for added and removed lines, tinted like the line.
/// </summary>
public sealed class DiffLineNumberMargin : AbstractMargin
{
    private const double OuterPadding = 10;
    private const double ColumnGap = 12;
    private const double SignWidth = 18;

    private readonly Func<InlineDiff?> _diff;
    private Typeface _typeface;
    private double _fontSize;
    private double _columnWidth;

    /// <summary>Creates the margin.</summary>
    /// <param name="diff">The diff shown; document line <c>n</c> shows its line <c>n - 1</c>.</param>
    public DiffLineNumberMargin(Func<InlineDiff?> diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        _diff = diff;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _typeface = new Typeface(GetValue(TextBlock.FontFamilyProperty));
        _fontSize = GetValue(TextBlock.FontSizeProperty);
        var digits = Math.Max(2, (_diff()?.LargestLineNumber ?? 0).ToString(CultureInfo.InvariantCulture).Length);
        _columnWidth = Format(new string('9', digits), DiffColors.LineNumber).Width;
        return new Size(OuterPadding + _columnWidth + ColumnGap + _columnWidth + SignWidth + OuterPadding, 0);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var textView = TextView;
        if (textView is not { VisualLinesValid: true } || _diff() is not { } diff)
        {
            return;
        }

        foreach (var visualLine in textView.VisualLines)
        {
            if (DiffColors.LineAt(diff.Lines, visualLine.FirstDocumentLine.LineNumber) is not { } line)
            {
                continue;
            }

            var top = visualLine.VisualTop - textView.VerticalOffset;
            var (background, foreground, sign) = line.Kind switch
            {
                DiffLineKind.Added => (DiffColors.AddedBackground, DiffColors.AddedText, "+"),
                DiffLineKind.Removed => (DiffColors.RemovedBackground, DiffColors.RemovedText, "−"),
                DiffLineKind.Collapsed => (DiffColors.CollapsedBackground, DiffColors.LineNumber, string.Empty),
                _ => ((IBrush?)null, DiffColors.LineNumber, string.Empty),
            };
            if (background is not null)
            {
                context.FillRectangle(background, new Rect(0, top, Bounds.Width, visualLine.Height));
            }

            var textTop = top + ((visualLine.Height - Format("0", foreground).Height) / 2);
            DrawRightAligned(context, line.OldNumber, foreground, OuterPadding + _columnWidth, textTop);
            DrawRightAligned(context, line.NewNumber, foreground, OuterPadding + _columnWidth + ColumnGap + _columnWidth, textTop);
            if (sign.Length > 0)
            {
                context.DrawText(Format(sign, foreground), new Point(Bounds.Width - OuterPadding - SignWidth + 6, textTop));
            }
        }
    }

    private void DrawRightAligned(DrawingContext context, int? number, IBrush brush, double right, double top)
    {
        if (number is { } value)
        {
            var text = Format(value.ToString(CultureInfo.CurrentCulture), brush);
            context.DrawText(text, new Point(right - text.Width, top));
        }
    }

    private FormattedText Format(string text, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, _fontSize, brush);
}
