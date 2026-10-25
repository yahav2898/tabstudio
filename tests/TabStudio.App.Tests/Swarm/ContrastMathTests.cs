using Avalonia.Media;
using TabStudio.App.Swarm.LayoutChecks;

namespace TabStudio.App.Tests.Swarm;

/// <summary>WCAG 2.2's contrast: relative luminance, the ratio, blending, and what counts as large text.</summary>
public sealed class ContrastMathTests
{
    [Fact]
    public void Black_on_white_is_21_to_1_and_a_color_on_itself_is_1_to_1()
    {
        Assert.Equal(21, ContrastMath.Ratio(Colors.Black, Colors.White), 3);
        Assert.Equal(21, ContrastMath.Ratio(Colors.White, Colors.Black), 3);
        Assert.Equal(1, ContrastMath.Ratio(Color.Parse("#336699"), Color.Parse("#336699")), 3);
        Assert.Equal(0, ContrastMath.RelativeLuminance(Colors.Black), 6);
        Assert.Equal(1, ContrastMath.RelativeLuminance(Colors.White), 6);
    }

    [Theory]
    [InlineData("#767676", "#FFFFFF", 4.54)]
    [InlineData("#777777", "#FFFFFF", 4.48)]
    [InlineData("#0000FF", "#FFFFFF", 8.59)]
    [InlineData("#FF0000", "#000000", 5.25)]
    [InlineData("#E0E0E0", "#1E1E1E", 12.63)]
    public void The_ratio_matches_published_values(string foreground, string background, double ratio)
    {
        Assert.Equal(ratio, ContrastMath.Ratio(Color.Parse(foreground), Color.Parse(background)), 2);
    }

    [Fact]
    public void A_translucent_color_is_seen_blended_over_what_is_under_it()
    {
        Assert.Equal(Color.FromRgb(128, 128, 128), ContrastMath.Over(Color.FromArgb(127, 0, 0, 0), Colors.White));
        Assert.Equal(Colors.Red, ContrastMath.Over(Colors.Red, Colors.Blue));
        Assert.Equal(Colors.Blue, ContrastMath.Over(Colors.Transparent, Colors.Blue));
        Assert.Equal(Color.FromArgb(64, 10, 20, 30), ContrastMath.WithOpacity(Color.FromArgb(128, 10, 20, 30), 0.5));
    }

    [Theory]
    [InlineData(24, FontWeight.Normal, true)]
    [InlineData(23.9, FontWeight.Normal, false)]
    [InlineData(18.7, FontWeight.Bold, true)]
    [InlineData(18.7, FontWeight.SemiBold, false)]
    [InlineData(14, FontWeight.Black, false)]
    public void Large_text_is_18_points_or_14_points_bold(double size, FontWeight weight, bool large)
    {
        Assert.Equal(large, ContrastMath.IsLargeText(size, weight));
        Assert.Equal(large ? 3 : 4.5, ContrastMath.Minimum(large));
    }
}
