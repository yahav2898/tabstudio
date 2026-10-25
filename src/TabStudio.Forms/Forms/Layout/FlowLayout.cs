using System.Drawing;

namespace System.Windows.Forms.Layout;

/// <summary>
/// The layout of a <see cref="FlowLayoutPanel"/>: children one after another in the flow direction, wrapping to a new
/// row or column when they do not fit, each with its margins, aligned across its row by its anchors.
/// </summary>
internal sealed class FlowLayout : LayoutEngine
{
    /// <summary>The shared instance.</summary>
    public static FlowLayout Instance { get; } = new();

    /// <inheritdoc />
    public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
    {
        var panel = (FlowLayoutPanel)container;
        var display = panel.DisplayRectangle;
        var offset = 0;
        foreach (var line in Lines(panel, display.Size))
        {
            PlaceLine(panel.FlowDirection, display, line, offset);
            offset += line.Thickness;
        }

        return panel.AutoSize;
    }

    /// <inheritdoc />
    public override void InitLayout(object child, BoundsSpecified specified)
    {
    }

    /// <inheritdoc />
    internal override Size GetPreferredSize(Control container, Size proposedSize)
    {
        var panel = (FlowLayoutPanel)container;
        var padding = panel.Padding;
        var insets = panel.NonClientInsets;
        var horizontal = IsHorizontal(panel.FlowDirection);
        var available = new Size(int.MaxValue, int.MaxValue);
        if (panel.WrapContents && horizontal && proposedSize.Width > 0)
        {
            available = new Size(proposedSize.Width - padding.Horizontal - insets.Horizontal, int.MaxValue);
        }
        else if (panel.WrapContents && !horizontal && proposedSize.Height > 0)
        {
            available = new Size(int.MaxValue, proposedSize.Height - padding.Vertical - insets.Vertical);
        }

        int along = 0, across = 0;
        foreach (var line in Lines(panel, available))
        {
            along = Math.Max(along, line.Length);
            across += line.Thickness;
        }

        var size = horizontal ? new Size(along, across) : new Size(across, along);
        return new Size(size.Width + padding.Horizontal + insets.Horizontal, size.Height + padding.Vertical + insets.Vertical);
    }

    private static bool IsHorizontal(FlowDirection direction) => direction is FlowDirection.LeftToRight or FlowDirection.RightToLeft;

    private static Size SizeOf(Control child) => child.AutoSize ? child.GetPreferredSize(Size.Empty) : child.Size;

    private static List<Line> Lines(FlowLayoutPanel panel, Size available)
    {
        var horizontal = IsHorizontal(panel.FlowDirection);
        var limit = horizontal ? available.Width : available.Height;
        var lines = new List<Line>();
        var current = new Line();
        foreach (var child in Participants(panel))
        {
            var size = SizeOf(child);
            var margin = child.Margin;
            var length = horizontal ? size.Width + margin.Horizontal : size.Height + margin.Vertical;
            var thickness = horizontal ? size.Height + margin.Vertical : size.Width + margin.Horizontal;
            if (panel.WrapContents && current.Children.Count > 0 && (long)current.Length + length > limit)
            {
                lines.Add(current);
                current = new Line();
            }

            current.Children.Add((child, size));
            current.Length += length;
            current.Thickness = Math.Max(current.Thickness, thickness);
            if (panel.GetFlowBreak(child))
            {
                lines.Add(current);
                current = new Line();
            }
        }

        if (current.Children.Count > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    private static void PlaceLine(FlowDirection direction, Rectangle display, Line line, int offset)
    {
        var position = 0;
        foreach (var (child, size) in line.Children)
        {
            var margin = child.Margin;
            Rectangle bounds;
            if (IsHorizontal(direction))
            {
                var x = direction == FlowDirection.LeftToRight
                    ? display.Left + position + margin.Left
                    : display.Right - position - margin.Right - size.Width;
                var (y, height) = Across(child, display.Top + offset, line.Thickness, margin.Top, margin.Bottom, size.Height, AnchorStyles.Top, AnchorStyles.Bottom);
                bounds = new Rectangle(x, y, size.Width, height);
                position += size.Width + margin.Horizontal;
            }
            else
            {
                var y = direction == FlowDirection.TopDown
                    ? display.Top + position + margin.Top
                    : display.Bottom - position - margin.Bottom - size.Height;
                var (x, width) = Across(child, display.Left + offset, line.Thickness, margin.Left, margin.Right, size.Width, AnchorStyles.Left, AnchorStyles.Right);
                bounds = new Rectangle(x, y, width, size.Height);
                position += size.Height + margin.Vertical;
            }

            child.SetBoundsFromLayout(bounds);
        }
    }

    /// <summary>Where a child goes across its line: stretched, at either side, or centered, by its anchors or dock.</summary>
    private static (int Start, int Length) Across(Control child, int lineStart, int lineThickness, int marginStart, int marginEnd, int length, AnchorStyles startEdge, AnchorStyles endEdge)
    {
        var anchor = child.Anchor;
        var dock = child.Dock;
        var start = dock != DockStyle.None || (anchor & startEdge) != 0;
        var end = dock == DockStyle.Fill || (dock == DockStyle.None && (anchor & endEdge) != 0);
        var room = lineThickness - marginStart - marginEnd;
        if (start && end)
        {
            return (lineStart + marginStart, Math.Max(0, room));
        }

        if (end)
        {
            return (lineStart + lineThickness - marginEnd - length, length);
        }

        if (start)
        {
            return (lineStart + marginStart, length);
        }

        return (lineStart + marginStart + ((room - length) / 2), length);
    }

    /// <summary>A row, or a column, of the flow.</summary>
    private sealed class Line
    {
        public List<(Control Control, Size Size)> Children { get; } = [];

        public int Length { get; set; }

        public int Thickness { get; set; }
    }
}
