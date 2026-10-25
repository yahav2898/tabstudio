using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TabStudio.App.Platform;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.Web;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// The web preview as a screen, against a browser that answers as the page's screen script does: the page set up and
/// followed, what a person does on it passed on, taps and keys replayed through the browser's input, and its layout
/// issues read back.
/// </summary>
public sealed class WebAppScreenTests
{
    private const string Button = "body/main[0]/form#form/button[0]";

    [AvaloniaFact]
    public async Task A_page_is_set_up_as_a_screen_and_runs_with_its_size_and_theme()
    {
        var browser = new ScriptedBrowser();
        using var screen = new WebAppScreen();
        var changes = 0;
        screen.Changed += (_, _) => changes++;

        screen.Show(browser);
        await UiTestHelpers.WaitUntilAsync(() => screen.IsRunning, because: "the page as a screen");

        Assert.Equal(1, browser.Installs);
        Assert.Equal(WebAppScreen.WebPreviewId, screen.Id);
        Assert.Equal(AppScreenKind.Preview, screen.Kind);
        Assert.Equal(new ScreenProfile(WebAppScreen.WebPreviewName, FormFactor.Tablet, 412, 800, 2.5, IsDark: true), screen.Profile);
        Assert.Equal(new Uri("http://127.0.0.1:5000/"), screen.Page);
        Assert.True(changes >= 2);
    }

    [AvaloniaFact]
    public async Task A_page_that_loads_again_is_set_up_again()
    {
        var browser = new ScriptedBrowser();
        using var screen = new WebAppScreen();
        screen.Show(browser);
        await UiTestHelpers.WaitUntilAsync(() => screen.IsRunning, because: "the first page");

        browser.Installed = false;
        await UiTestHelpers.WaitUntilAsync(() => !screen.IsRunning || browser.Installs == 2, because: "the new page");
        await UiTestHelpers.WaitUntilAsync(() => screen.IsRunning && browser.Installs == 2, because: "the new page set up");
    }

    [AvaloniaFact]
    public async Task What_a_person_does_on_the_page_comes_out_by_element()
    {
        var browser = new ScriptedBrowser();
        using var screen = new WebAppScreen();
        var captured = new List<AppInput>();
        screen.InputCaptured += (_, input) => captured.Add(input);
        screen.Show(browser);
        await UiTestHelpers.WaitUntilAsync(() => screen.IsRunning, because: "the page");

        browser.Pending = $$"""
            [{"kind":"Tap","path":"{{Button}}","x":0.25,"y":0.5},
             {"kind":"Text","path":"","text":"Ada"},
             {"kind":"Key","path":"","key":"Enter","modifiers":"Control"},
             {"kind":"Scroll","path":"body","dx":0,"dy":120},
             {"kind":"Wave","path":"body"}]
            """;
        await UiTestHelpers.WaitUntilAsync(() => captured.Count == 4, because: "the person's input");

        Assert.Equal(new AppInput(AppInputKind.Tap, Button, new Point(0.25, 0.5)), captured[0]);
        Assert.Equal(new AppInput(AppInputKind.Text, string.Empty, Text: "Ada"), captured[1]);
        Assert.Equal(new AppInput(AppInputKind.Key, string.Empty, Key: "Enter", Modifiers: "Control"), captured[2]);
        Assert.Equal(new AppInput(AppInputKind.Scroll, "body", ScrollDelta: new Vector(0, 120)), captured[3]);
    }

    [AvaloniaFact]
    public async Task A_replayed_tap_touches_the_element_through_the_browser_s_touch_input()
    {
        var browser = new ScriptedBrowser();
        using var screen = await RunningAsync(browser);

        Assert.True(await screen.SendInputAsync(new AppInput(AppInputKind.Tap, Button, new Point(0.25, 0.5)), CancellationToken.None));
        Assert.True(await screen.SendInputAsync(new AppInput(AppInputKind.LongPress, Button), CancellationToken.None));

        var point = Assert.Single(browser.Scripts, s => s.Contains(".point(", StringComparison.Ordinal) && s.Contains("0.25", StringComparison.Ordinal));
        Assert.Contains($"\"{Button}\"", point, StringComparison.Ordinal);
        Assert.Equal(2, browser.Scripts.Count(s => s.Contains(".expect(\"Tap\", null)", StringComparison.Ordinal) || s.Contains(".expect(\"LongPress\", null)", StringComparison.Ordinal)));
        Assert.Equal(new[] { (new Point(300, 420), TimeSpan.FromMilliseconds(60)), (new Point(300, 420), TimeSpan.FromMilliseconds(800)) }, browser.Touches);

        // An element the page does not have is not touched.
        browser.PointAnswer = "null";
        Assert.False(await screen.SendInputAsync(new AppInput(AppInputKind.Tap, "body/nav#missing"), CancellationToken.None));
        Assert.Equal(2, browser.Touches.Count);
    }

    [AvaloniaFact]
    public async Task Keys_go_through_the_browser_s_key_input_and_text_into_the_focused_element()
    {
        var browser = new ScriptedBrowser();
        using var screen = await RunningAsync(browser);

        Assert.True(await screen.SendInputAsync(new AppInput(AppInputKind.Key, string.Empty, Key: "Return", Modifiers: "Control, Shift"), CancellationToken.None));
        Assert.True(await screen.SendInputAsync(new AppInput(AppInputKind.Text, string.Empty, Text: "שלום Ada"), CancellationToken.None));
        Assert.True(await screen.SendInputAsync(new AppInput(AppInputKind.Scroll, "body", ScrollDelta: new Vector(0, 200)), CancellationToken.None));
        Assert.False(await screen.SendInputAsync(new AppInput(AppInputKind.Key, string.Empty, Key: "NoSuchKey"), CancellationToken.None));

        Assert.Equal(new[] { (Key.Enter, KeyModifiers.Control | KeyModifiers.Shift) }, browser.Keys);
        Assert.Contains(browser.Scripts, s => s.Contains(".expect(\"Key\", \"Enter\")", StringComparison.Ordinal));
        Assert.Contains(browser.Scripts, s => s.Contains(".type(\"\\u05E9\\u05DC\\u05D5\\u05DD Ada\")", StringComparison.Ordinal));
        Assert.Contains(browser.Scripts, s => s.Contains(".scroll(\"body\", 0, 200)", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task The_page_s_layout_issues_come_back_as_the_checks_list_them()
    {
        var browser = new ScriptedBrowser
        {
            IssuesAnswer = $$$"""
                [{"kind":"SmallTouchTarget","severity":"Warning","path":"{{{Button}}}","message":"button \"Greet\" is 70 × 40 dips.","bounds":{"x":252.5,"y":169,"width":69.6,"height":40}},
                 {"kind":"Sparkle","severity":"Error","path":"body","message":"?","bounds":null}]
                """,
        };
        using var screen = await RunningAsync(browser);

        var issue = Assert.Single(await screen.InspectLayoutAsync(CancellationToken.None));

        Assert.Equal(new LayoutIssue(LayoutIssueKind.SmallTouchTarget, LayoutIssueSeverity.Warning, "button \"Greet\" is 70 × 40 dips.", Button, new Rect(252.5, 169, 69.6, 40)), issue);
        Assert.Contains(browser.Scripts, s => s.Contains(".inspect({ screen: \"a 412-wide tablet\", touch: 44, formFactor: \"Tablet\" })", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task A_hidden_screen_answers_nothing()
    {
        var browser = new ScriptedBrowser();
        using var screen = await RunningAsync(browser);

        screen.Hide();

        Assert.False(screen.IsRunning);
        Assert.Null(screen.WebView);
        Assert.False(await screen.SendInputAsync(new AppInput(AppInputKind.Tap, Button), CancellationToken.None));
        Assert.Null(await screen.CaptureAsync(CancellationToken.None));
        Assert.Empty(await screen.InspectLayoutAsync(CancellationToken.None));
        Assert.Empty(browser.Touches);
    }

    [AvaloniaFact]
    public async Task A_running_screen_takes_the_browser_s_picture()
    {
        var browser = new ScriptedBrowser();
        using var screen = await RunningAsync(browser);

        Assert.Equal(browser.Picture, await screen.CaptureAsync(CancellationToken.None));
        Assert.Equal(ScreenCapture.MaxPixels, browser.CapturedAt);
    }

    private static async Task<WebAppScreen> RunningAsync(ScriptedBrowser browser)
    {
        var screen = new WebAppScreen();
        screen.Show(browser);
        await UiTestHelpers.WaitUntilAsync(() => screen.IsRunning, because: "the page");
        return screen;
    }

    /// <summary>A browser whose page answers as the screen script does.</summary>
    private sealed class ScriptedBrowser : IScriptableWebView
    {
        private const string State = """{"ready":true,"url":"http://127.0.0.1:5000/","width":412.4,"height":800,"scale":2.5,"dark":true,"rtl":false,"inputs":""";

        public bool Installed { get; set; }

        public int Installs { get; private set; }

        /// <summary>Inputs the page queued, as JSON, which the next read takes.</summary>
        public string? Pending { get; set; }

        public string PointAnswer { get; set; } = """{"x":300,"y":420}""";

        public string IssuesAnswer { get; set; } = "[]";

        public byte[] Picture { get; } = [0x89, 0x50, 0x4E, 0x47];

        public int CapturedAt { get; private set; }

        public List<string> Scripts { get; } = [];

        public List<(Point Pixel, TimeSpan Hold)> Touches { get; } = [];

        public List<(Key Key, KeyModifiers Modifiers)> Keys { get; } = [];

        public Control Control { get; } = new Border();

        public event EventHandler<Uri>? Navigated
        {
            add { }
            remove { }
        }

        public void Navigate(Uri uri)
        {
        }

        public void Reload()
        {
        }

        public void GoBack()
        {
        }

        public void GoForward()
        {
        }

        public Task<string?> EvaluateAsync(string script, CancellationToken cancellationToken)
        {
            Scripts.Add(script);
            if (script == WebScreenScript.Install)
            {
                Installed = true;
                Installs++;
                return Task.FromResult<string?>("true");
            }

            if (script == WebScreenScript.State)
            {
                if (!Installed)
                {
                    return Task.FromResult<string?>("null");
                }

                var inputs = Pending ?? "[]";
                Pending = null;
                return Task.FromResult<string?>(State + inputs + "}");
            }

            string? answer = script switch
            {
                _ when script.Contains(".point(", StringComparison.Ordinal) => PointAnswer,
                _ when script.Contains(".inspect(", StringComparison.Ordinal) => IssuesAnswer,
                _ when script.Contains(".type(", StringComparison.Ordinal) || script.Contains(".scroll(", StringComparison.Ordinal) => "true",
                _ => "null",
            };
            return Task.FromResult(answer);
        }

        public Task<byte[]?> CaptureAsync(int maxPixels, CancellationToken cancellationToken)
        {
            CapturedAt = maxPixels;
            return Task.FromResult<byte[]?>(Picture);
        }

        public Task<bool> TouchAsync(Point pixel, TimeSpan hold, CancellationToken cancellationToken)
        {
            Touches.Add((pixel, hold));
            return Task.FromResult(true);
        }

        public Task<bool> PressKeyAsync(Key key, KeyModifiers modifiers, CancellationToken cancellationToken)
        {
            Keys.Add((key, modifiers));
            return Task.FromResult(true);
        }
    }
}
