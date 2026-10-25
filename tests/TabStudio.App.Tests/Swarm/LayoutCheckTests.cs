using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.LayoutChecks;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// The layout checks, each with a case it reports and a case a designer would not want reported: clipped text,
/// controls off screen, overlapping controls, small touch targets and low contrast.
/// </summary>
public sealed class LayoutCheckTests
{
    private const double Width = 360;
    private const double Height = 640;

    [AvaloniaFact]
    public void Text_that_runs_past_its_box_is_trimmed_or_loses_lines_is_clipped()
    {
        var issues = Inspect(Stack(
            new TextBlock { Name = "Overflow", Width = 80, Text = "A sentence much longer than eighty dips" },
            new TextBlock { Name = "Trimmed", Width = 80, TextTrimming = TextTrimming.CharacterEllipsis, Text = "Another long sentence to trim" },
            new TextBlock { Name = "Lines", Width = 80, Height = 20, TextWrapping = TextWrapping.Wrap, Text = "Wrapping text that needs several lines of room" },
            new Border { Name = "Box", Width = 60, Height = 30, ClipToBounds = true, Child = new Canvas { Children = { new TextBlock { Text = "Clipped by the box" } } } }));

        Assert.Equal(LayoutIssueSeverity.Error, Issue(issues, LayoutIssueKind.ClippedText, "TextBlock#Overflow").Severity);
        Assert.Contains("runs past its box", Issue(issues, LayoutIssueKind.ClippedText, "TextBlock#Overflow").Message, StringComparison.Ordinal);
        Assert.Equal(LayoutIssueSeverity.Warning, Issue(issues, LayoutIssueKind.ClippedText, "TextBlock#Trimmed").Severity);
        Assert.Contains("cut off at the bottom", Issue(issues, LayoutIssueKind.ClippedText, "TextBlock#Lines").Message, StringComparison.Ordinal);
        Assert.Contains("cut off by its container", Issue(issues, LayoutIssueKind.ClippedText, "Border#Box/Canvas[0]/TextBlock[0]").Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Text_that_fits_wraps_or_scrolls_into_view_is_not_clipped()
    {
        var issues = Inspect(Stack(
            new TextBlock { Width = 300, Text = "Short" },
            new TextBlock { Width = 120, TextWrapping = TextWrapping.Wrap, Text = "Wrapping text that has all the room it needs" },
            new ScrollViewer
            {
                Height = 40,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new TextBlock { Text = "A very long line that scrolls sideways inside its scroll viewer, so nothing is lost" },
            },
            new Button
            {
                FlowDirection = FlowDirection.RightToLeft,
                Width = 28,
                Height = 28,
                MinWidth = 0,
                MinHeight = 0,
                Padding = default,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = "?",
            },
            new TextBlock { FlowDirection = FlowDirection.RightToLeft, Text = "שלום! ההזמנה שלך בדרך אליך" }));

        Assert.DoesNotContain(issues, i => i.Kind == LayoutIssueKind.ClippedText);
    }

    [AvaloniaFact]
    public void Controls_outside_the_screen_are_reported_and_those_a_scroll_viewer_reaches_are_not()
    {
        var far = new Button { Name = "Far", Width = 100, Height = 48, Content = "Far" };
        var partly = new Button { Name = "Partly", Width = 200, Height = 48, Content = "Partly" };
        var canvas = new Canvas { Width = Width, Height = Height / 2, VerticalAlignment = VerticalAlignment.Top };
        canvas.Children.Add(far);
        canvas.Children.Add(partly);
        Canvas.SetLeft(far, 420);
        Canvas.SetLeft(partly, 260);
        Canvas.SetTop(partly, 100);

        var below = new StackPanel();
        for (var i = 0; i < 30; i++)
        {
            below.Children.Add(new Button { Name = $"Row{i}", Height = 48, Width = 200, Content = $"Row {i}" });
        }

        var issues = Inspect(new Grid { RowDefinitions = new RowDefinitions("*,*"), Children = { canvas, Row(new ScrollViewer { Content = below }, 1) } });

        var outside = Issue(issues, LayoutIssueKind.OffScreen, "Canvas[0]/Button#Far");
        Assert.Equal(LayoutIssueSeverity.Error, outside.Severity);
        Assert.Contains("60 dips past the right edge", outside.Message, StringComparison.Ordinal);
        var cut = Issue(issues, LayoutIssueKind.OffScreen, "Canvas[0]/Button#Partly");
        Assert.Equal(LayoutIssueSeverity.Warning, cut.Severity);
        Assert.Contains("100 of its 200 dips of width are cut off", cut.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(issues, i => i.Kind == LayoutIssueKind.OffScreen && i.ElementPath.Contains("Row", StringComparison.Ordinal));

        // The text of a button that is off screen is not reported again.
        Assert.DoesNotContain(issues, i => i.Kind == LayoutIssueKind.ClippedText && i.ElementPath.StartsWith("Canvas[0]/Button#Far", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Controls_that_overlap_are_reported_with_the_one_on_top_and_nested_or_adjacent_ones_are_not()
    {
        var grid = new Grid { Width = 300, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        grid.Children.Add(new Button { Name = "Under", Width = 120, Height = 48, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = "Under" });
        grid.Children.Add(new Button { Name = "Over", Width = 120, Height = 48, Margin = new Thickness(60, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = "Over" });
        grid.Children.Add(new Button { Name = "Below", Width = 120, Height = 48, Margin = new Thickness(0, 48, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = "Below" });
        var list = new ListBox { Height = 120, ItemsSource = new[] { "One", "Two" } };

        var issues = Inspect(Stack(grid, list));

        var overlap = Assert.Single(issues, i => i.Kind == LayoutIssueKind.Overlap);
        Assert.Equal("StackPanel/Grid[0]/Button#Under", overlap.ElementPath);
        Assert.Equal(LayoutIssueSeverity.Error, overlap.Severity);
        Assert.Contains("lies under Button \"Over\"", overlap.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Touch_targets_need_44_dips_on_phones_and_tablets_and_24_on_desktops()
    {
        Control Content() => Stack(
            new Button { Name = "Small", Width = 30, Height = 30, MinWidth = 0, MinHeight = 0, Content = "S" },
            new Button { Name = "Tiny", Width = 16, Height = 16, MinWidth = 0, MinHeight = 0 },
            new Button { Name = "Big", Width = 120, Height = 48, Content = "Big" });

        var phone = Inspect(Content());
        Assert.Equal(LayoutIssueSeverity.Warning, Issue(phone, LayoutIssueKind.SmallTouchTarget, "Button#Small").Severity);
        Assert.Contains("30 × 30 dips", Issue(phone, LayoutIssueKind.SmallTouchTarget, "Button#Small").Message, StringComparison.Ordinal);
        Assert.Equal(LayoutIssueSeverity.Error, Issue(phone, LayoutIssueKind.SmallTouchTarget, "Button#Tiny").Severity);
        Assert.DoesNotContain(phone, i => i.ElementPath.EndsWith("Button#Big", StringComparison.Ordinal));

        var desktop = Inspect(Content(), FormFactor.Desktop);
        Assert.DoesNotContain(desktop, i => i.Kind == LayoutIssueKind.SmallTouchTarget && i.ElementPath.EndsWith("Button#Small", StringComparison.Ordinal));
        Assert.Contains(desktop, i => i.Kind == LayoutIssueKind.SmallTouchTarget && i.ElementPath.EndsWith("Button#Tiny", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Text_below_WCAG_AA_contrast_is_reported_from_the_colors_a_person_sees()
    {
        var issues = Inspect(new Border
        {
            Background = Brushes.White,
            Child = Stack(
                new TextBlock { Name = "Faint", Text = "Faint", Foreground = new SolidColorBrush(Color.Parse("#999999")) },
                new TextBlock { Name = "Readable", Text = "Readable", Foreground = new SolidColorBrush(Color.Parse("#767676")) },
                new TextBlock { Name = "LargeFaint", Text = "Large", FontSize = 26, Foreground = new SolidColorBrush(Color.Parse("#949494")) },
                new TextBlock { Name = "Faded", Text = "Faded by opacity", Opacity = 0.4, Foreground = Brushes.Black },
                new Border { Background = new SolidColorBrush(Color.Parse("#80000000")), Child = new TextBlock { Name = "OnGray", Text = "On half black", Foreground = new SolidColorBrush(Color.Parse("#404040")) } },
                new Border
                {
                    Background = new LinearGradientBrush { GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.Gray, 1) } },
                    Child = new TextBlock { Name = "OnGradient", Text = "On a gradient", Foreground = new SolidColorBrush(Color.Parse("#999999")) },
                },
                new Button { IsEnabled = false, Content = new TextBlock { Name = "Disabled", Text = "Disabled", Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")) } }),
        });

        var faint = Issue(issues, LayoutIssueKind.LowContrast, "Border/StackPanel[0]/TextBlock#Faint");
        Assert.Equal(LayoutIssueSeverity.Error, faint.Severity);
        Assert.Contains("2.8:1", faint.Message, StringComparison.Ordinal);
        Assert.Contains("needs at least 4.5:1", faint.Message, StringComparison.Ordinal);
        Assert.Contains(issues, i => i.ElementPath.EndsWith("TextBlock#Faded", StringComparison.Ordinal) && i.Kind == LayoutIssueKind.LowContrast);
        Assert.Contains(issues, i => i.ElementPath.EndsWith("TextBlock#OnGray", StringComparison.Ordinal) && i.Kind == LayoutIssueKind.LowContrast);
        Assert.DoesNotContain(issues, i => i.Kind == LayoutIssueKind.LowContrast
            && (i.ElementPath.EndsWith("TextBlock#Readable", StringComparison.Ordinal) || i.ElementPath.EndsWith("TextBlock#LargeFaint", StringComparison.Ordinal)
                || i.ElementPath.EndsWith("TextBlock#OnGradient", StringComparison.Ordinal) || i.ElementPath.EndsWith("TextBlock#Disabled", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public void A_rule_that_is_turned_off_reports_nothing_and_the_most_serious_issues_come_first()
    {
        Control Content() => Stack(new Button { Name = "Small", Width = 30, Height = 30, MinWidth = 0, MinHeight = 0 }, new TextBlock { Width = 40, Text = "Too long for forty dips" });

        var all = Inspect(Content());
        Assert.Equal(LayoutIssueSeverity.Error, all[0].Severity);
        Assert.Contains(all, i => i.Kind == LayoutIssueKind.SmallTouchTarget);

        var withoutTouch = Inspect(Content(), settings: LayoutCheckSettings.All.With(LayoutIssueKind.SmallTouchTarget, false));
        Assert.DoesNotContain(withoutTouch, i => i.Kind == LayoutIssueKind.SmallTouchTarget);
        Assert.Contains(withoutTouch, i => i.Kind == LayoutIssueKind.ClippedText);
        Assert.Equal("SWRM004", LayoutRules.Code(LayoutIssueKind.SmallTouchTarget));
        Assert.Equal(LayoutIssueKind.Overlap, LayoutRules.FromCode("SWRM003"));
        Assert.EndsWith("layout-checks.md#swrm005", LayoutRules.HelpLink(LayoutIssueKind.LowContrast).ToString(), StringComparison.Ordinal);
    }

    /// <summary>Shows content on a 360 × 640 screen and runs every check on it.</summary>
    private static IReadOnlyList<LayoutIssue> Inspect(Control content, FormFactor formFactor = FormFactor.Phone, LayoutCheckSettings? settings = null)
    {
        var window = SwarmTestHelpers.Show(content, Width, Height);
        try
        {
            return new LayoutInspector(settings ?? LayoutCheckSettings.All).Inspect(content, SwarmTestHelpers.Profile(Width, Height, formFactor));
        }
        finally
        {
            window.Close();
        }
    }

    private static LayoutIssue Issue(IReadOnlyList<LayoutIssue> issues, LayoutIssueKind kind, string pathEnd)
    {
        var matches = issues.Where(i => i.Kind == kind && i.ElementPath.EndsWith(pathEnd, StringComparison.Ordinal)).ToList();
        Assert.True(matches.Count == 1, $"Expected one {kind} issue at {pathEnd}, found: {string.Join("; ", issues.Select(i => $"{i.Kind} {i.ElementPath}: {i.Message}"))}");
        return matches[0];
    }

    private static StackPanel Stack(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        stack.Children.AddRange(children);
        return stack;
    }

    private static Control Row(Control control, int row)
    {
        Grid.SetRow(control, row);
        return control;
    }
}
