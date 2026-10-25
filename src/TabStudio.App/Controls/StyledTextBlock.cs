using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Controls;

/// <summary>
/// A text block that shows <see cref="StyledRun"/>s: code colored with the editor's <see cref="SyntaxTheme"/>, with
/// bold parts. Signatures in Quick Info, parameter info and the completion list use it.
/// </summary>
public sealed class StyledTextBlock : TextBlock
{
    /// <summary>The runs to show.</summary>
    public static readonly StyledProperty<IReadOnlyList<StyledRun>?> RunsProperty =
        AvaloniaProperty.Register<StyledTextBlock, IReadOnlyList<StyledRun>?>(nameof(Runs));

    static StyledTextBlock()
    {
        RunsProperty.Changed.AddClassHandler<StyledTextBlock>((block, _) => block.UpdateInlines());
    }

    /// <inheritdoc cref="RunsProperty"/>
    public IReadOnlyList<StyledRun>? Runs
    {
        get => GetValue(RunsProperty);
        set => SetValue(RunsProperty, value);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextBlock);

    private void UpdateInlines()
    {
        var inlines = Inlines ??= new InlineCollection();
        inlines.Clear();
        if (Runs is not { } runs)
        {
            return;
        }

        foreach (var run in runs)
        {
            var lines = run.Text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    inlines.Add(new LineBreak());
                }

                if (lines[i].Length > 0)
                {
                    inlines.Add(CreateRun(lines[i].TrimEnd('\r'), run));
                }
            }
        }
    }

    private static Run CreateRun(string text, StyledRun style)
    {
        var inline = new Run(text);
        if (style.Classification is { } classification && SyntaxTheme.For(classification) is { } brush)
        {
            inline.Foreground = brush;
        }

        if (style.IsBold)
        {
            inline.FontWeight = FontWeight.Bold;
        }

        return inline;
    }
}
