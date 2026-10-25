using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Editing;

/// <summary>
/// Shows the file locations in the Output panel's lines as links, such as <c>File "main.py", line 4</c> in a
/// Python traceback, which open the file at that line when tapped or clicked.
/// </summary>
/// <param name="viewModel">The panel, whose <see cref="OutputViewModel.LinkFinder"/> finds the locations.</param>
/// <param name="brush">The links' color.</param>
internal sealed class OutputLinkGenerator(Func<OutputViewModel?> viewModel, Func<IBrush> brush) : VisualLineElementGenerator
{
    private int _line = -1;
    private IReadOnlyList<OutputLink> _links = [];

    /// <inheritdoc />
    public override void StartGeneration(ITextRunConstructionContext context)
    {
        base.StartGeneration(context);
        _line = -1;
        _links = [];
    }

    /// <inheritdoc />
    public override int GetFirstInterestedOffset(int startOffset)
    {
        var line = CurrentContext.Document.GetLineByOffset(startOffset);
        foreach (var link in LinksOf(line))
        {
            var offset = line.Offset + link.Start;
            if (offset >= startOffset)
            {
                return offset;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public override VisualLineElement ConstructElement(int offset)
    {
        var line = CurrentContext.Document.GetLineByOffset(offset);
        var link = LinksOf(line).FirstOrDefault(l => line.Offset + l.Start == offset);
        return link is null || viewModel() is not { } panel
            ? null!
            : new OutputLinkText(CurrentContext.VisualLine, link, panel, brush());
    }

    private IReadOnlyList<OutputLink> LinksOf(DocumentLine line)
    {
        if (line.LineNumber != _line)
        {
            _line = line.LineNumber;
            var finder = viewModel()?.LinkFinder;
            _links = finder is null || line.Length == 0
                ? []
                : [.. finder(CurrentContext.Document.GetText(line)).Where(l => l.Start >= 0 && l.Length > 0 && l.Start + l.Length <= line.Length).OrderBy(l => l.Start)];
        }

        return _links;
    }

    /// <summary>A link's text: underlined, and opening its location on a tap or click.</summary>
    private sealed class OutputLinkText : VisualLineText
    {
        private readonly OutputLink _link;
        private readonly OutputViewModel _panel;
        private readonly IBrush _brush;

        public OutputLinkText(VisualLine line, OutputLink link, OutputViewModel panel, IBrush brush)
            : this(line, link.Length, link, panel, brush)
        {
        }

        private OutputLinkText(VisualLine line, int length, OutputLink link, OutputViewModel panel, IBrush brush)
            : base(line, length)
        {
            _link = link;
            _panel = panel;
            _brush = brush;
        }

        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            TextRunProperties.SetForegroundBrush(_brush);
            TextRunProperties.SetTextDecorations(TextDecorations.Underline);
            return base.CreateTextRun(startVisualColumn, context);
        }

        protected override void OnQueryCursor(PointerEventArgs e)
        {
            if (e.Source is InputElement element)
            {
                element.Cursor = new Cursor(StandardCursorType.Hand);
            }

            e.Handled = true;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (!e.Handled)
            {
                _panel.ActivateLink(_link);
                e.Handled = true;
            }
        }

        protected override VisualLineText CreateInstance(int length) => new OutputLinkText(ParentVisualLine, length, _link, _panel, _brush);
    }
}
