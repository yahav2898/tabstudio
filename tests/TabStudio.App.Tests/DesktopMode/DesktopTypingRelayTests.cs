using TabStudio.App.Views.DesktopMode;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>
/// How a change of the companion's typing field, as an on-screen keyboard makes it, becomes keys on the big screen:
/// the shared beginning and end of the field's text before and after.
/// </summary>
public sealed class DesktopTypingRelayTests
{
    [Theory]
    [InlineData("", "var", 0, 0)]
    [InlineData("var", "var x", 3, 0)]
    [InlineData("var x", "var", 3, 0)]
    [InlineData("teh", "the", 1, 0)]
    [InlineData("// typed on a tablet", "// Typed on a tablet", 3, 16)]
    [InlineData("aaa", "aaaa", 3, 0)]
    [InlineData("abc", "", 0, 0)]
    public void The_change_is_found_between_what_the_texts_share(string before, string after, int prefix, int suffix)
    {
        Assert.Equal((prefix, suffix), DesktopTypingRelay.Common(before, after));
    }
}
