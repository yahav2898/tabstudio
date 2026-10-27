using TabStudio.Core.Previews;

namespace TabStudio.App.ViewModels;

/// <summary>One choice of a preview's zoom selector, such as "100%".</summary>
/// <param name="Mode">The mode it chooses.</param>
/// <param name="Label">What the selector shows.</param>
public sealed record PreviewZoomChoice(PreviewZoomMode Mode, string Label)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}
