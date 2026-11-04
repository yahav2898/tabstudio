namespace TabStudio.App.Services;

/// <summary>What Find in Files and Replace in Files look for, with Visual Studio's options.</summary>
/// <param name="Text">What to find: plain text, or a regular expression when <paramref name="UseRegularExpressions"/> is set.</param>
/// <param name="MatchCase">Whether letter case must match. Off by default, as in Visual Studio.</param>
/// <param name="MatchWholeWord">Whether a match must not run on into a letter, digit or underscore on either side.</param>
/// <param name="UseRegularExpressions">
/// Whether <paramref name="Text"/> is a .NET regular expression, where <c>^</c> and <c>$</c> match at every line, and
/// <c>$1</c> and <c>${name}</c> in a replacement are the match's groups.
/// </param>
public sealed record FindInFilesQuery(string Text, bool MatchCase = false, bool MatchWholeWord = false, bool UseRegularExpressions = false);
