using TabStudio.Live.Text;

namespace TabStudio.Live;

/// <summary>
/// Where a participant is: the document it shows, its caret and selection there, and the first line it sees, which
/// is what someone following it is shown. Positions stay put while others type.
/// </summary>
/// <param name="Participant">The participant.</param>
/// <param name="Path">The document it shows, relative to the workspace's folder; <see langword="null"/> for none.</param>
/// <param name="Anchor">Where its selection starts; the caret alone when it equals <paramref name="Head"/>.</param>
/// <param name="Head">Where its caret is.</param>
/// <param name="TopLine">The first line it sees, one-based.</param>
public sealed record LivePresence(int Participant, string? Path, TextPosition Anchor, TextPosition Head, int TopLine);
