using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Swarm;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// A screen's input: what a person does is captured as inputs that name elements, and inputs replay as a person's
/// taps, holds, scrolls, text and keys, through Avalonia's own input handling, on a screen of any size.
/// </summary>
public sealed class ScreenInputTests
{
    [AvaloniaFact]
    public async Task A_replayed_tap_clicks_the_button_through_its_own_input_handling_and_is_not_captured_again()
    {
        var app = new CounterApp();
        var window = SwarmTestHelpers.Show(app.Surface, 360, 640);
        using var input = new ScreenInput(app.Surface, app.Root);
        var captured = new List<AppInput>();
        input.Captured += (_, i) => captured.Add(i);

        var tapped = await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.Tap, "StackPanel/Button#Count"), CancellationToken.None));

        Assert.True(tapped);
        Assert.Equal(1, app.Clicks);
        Assert.False(app.Count.IsPressed);
        Assert.Empty(captured);
        Assert.False(await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.Tap, "StackPanel/Button#Missing"), CancellationToken.None)));
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_person_s_tap_is_captured_by_its_element_and_replays_on_a_screen_of_another_size()
    {
        var phone = new CounterApp();
        var tablet = new CounterApp();
        var phoneWindow = SwarmTestHelpers.Show(phone.Surface, 360, 640);
        var tabletWindow = SwarmTestHelpers.Show(tablet.Surface, 800, 1280);
        using var phoneInput = new ScreenInput(phone.Surface, phone.Root);
        using var tabletInput = new ScreenInput(tablet.Surface, tablet.Root);
        var captured = new List<AppInput>();
        phoneInput.Captured += (_, i) => captured.Add(i);

        // A person clicks the phone's button, a quarter of the way into it.
        var point = phone.Count.TranslatePoint(new Point(phone.Count.Bounds.Width / 4, phone.Count.Bounds.Height / 2), phoneWindow)!.Value;
        phoneWindow.MouseDown(point, MouseButton.Left);
        phoneWindow.MouseUp(point, MouseButton.Left);

        Assert.Equal(1, phone.Clicks);
        var tap = Assert.Single(captured);
        Assert.Equal(AppInputKind.Tap, tap.Kind);
        Assert.Equal("StackPanel/Button#Count", tap.ElementPath);
        Assert.Equal(0.25, tap.Position.X, 2);
        Assert.Equal(0.5, tap.Position.Y, 2);

        // The same tap on the tablet clicks the tablet's button.
        Assert.True(await SwarmTestHelpers.WithFramesAsync(tabletInput.ReplayAsync(tap, CancellationToken.None)));
        Assert.Equal(1, tablet.Clicks);
        phoneWindow.Close();
        tabletWindow.Close();
    }

    [AvaloniaFact]
    public async Task Typed_text_and_keys_reach_the_text_box_the_screen_focused_and_go_back_out_as_captures()
    {
        var app = new CounterApp();
        var window = SwarmTestHelpers.Show(app.Surface, 360, 640);
        using var input = new ScreenInput(app.Surface, app.Root);
        var captured = new List<AppInput>();
        input.Captured += (_, i) => captured.Add(i);

        Assert.False(await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.Text, string.Empty, Text: "nowhere"), CancellationToken.None)));
        Assert.True(await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.Tap, "StackPanel/TextBox#Name"), CancellationToken.None)));
        Assert.Same(app.Name, input.FocusedElement);
        Assert.True(await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.Text, string.Empty, Text: "Adah"), CancellationToken.None)));
        Assert.True(await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.Key, string.Empty, Key: "Back"), CancellationToken.None)));
        Assert.Equal("Ada", app.Name.Text);

        // The app's own key bindings answer replayed keys, as they answer a keyboard.
        Assert.True(await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.Key, string.Empty, Key: "S", Modifiers: "Control"), CancellationToken.None)));
        Assert.Equal(1, app.Saves);
        Assert.Empty(captured);

        // A person typing into it is captured as text, and Enter as a key; letters are not keys.
        window.KeyTextInput("!");
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        Assert.Equal("Ada!", app.Name.Text);
        Assert.Collection(
            captured,
            text => Assert.Equal(new AppInput(AppInputKind.Text, string.Empty, Text: "!"), text),
            enter => Assert.Equal(new AppInput(AppInputKind.Key, string.Empty, Key: "Enter"), enter),
            selectAll => Assert.Equal(new AppInput(AppInputKind.Key, string.Empty, Key: "A", Modifiers: "Control"), selectAll));
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_replayed_long_press_holds_until_the_hold_is_recognized()
    {
        var app = new CounterApp();
        var window = SwarmTestHelpers.Show(app.Surface, 360, 640);
        using var input = new ScreenInput(app.Surface, app.Root);
        var holds = new List<HoldingState>();
        app.Count.AddHandler(Gestures.HoldingEvent, (_, e) => holds.Add(e.HoldingState));

        Assert.True(await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.LongPress, "StackPanel/Button#Count"), CancellationToken.None)));

        Assert.Equal(new[] { HoldingState.Started, HoldingState.Completed }, holds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_replayed_scroll_scrolls_by_its_distance_and_a_person_s_scroll_is_captured_once_it_rests()
    {
        var content = new StackPanel { Name = "Content" };
        for (var i = 0; i < 40; i++)
        {
            content.Children.Add(new Border { Height = 60, Child = new TextBlock { Text = $"Row {i}" } });
        }

        var viewer = new ScrollViewer { Name = "List", Content = content };
        var surface = new Border { Child = viewer };
        var window = SwarmTestHelpers.Show(surface, 360, 640);
        using var input = new ScreenInput(surface, viewer);
        var captured = new List<AppInput>();
        input.Captured += (_, i) => captured.Add(i);

        Assert.True(await SwarmTestHelpers.WithFramesAsync(input.ReplayAsync(new AppInput(AppInputKind.Scroll, "ScrollViewer#List", new Point(0.5, 0.5), ScrollDelta: new Vector(0, 300)), CancellationToken.None)));
        Assert.Equal(300, viewer.Offset.Y);

        // A person's two wheel notches, 100 dips back up, are one scroll input once the list rests.
        var middle = viewer.TranslatePoint(new Point(180, 320), window)!.Value;
        window.MouseWheel(middle, new Vector(0, 1));
        window.MouseWheel(middle, new Vector(0, 1));
        await UiTestHelpers.WaitUntilAsync(() => captured.Count > 0, TimeSpan.FromSeconds(10), "the scroll to be captured");

        var scroll = Assert.Single(captured);
        Assert.Equal(AppInputKind.Scroll, scroll.Kind);
        Assert.Equal("ScrollViewer#List", scroll.ElementPath);
        Assert.Equal(new Vector(0, -100), scroll.ScrollDelta);
        Assert.Equal(200, viewer.Offset.Y);
        window.Close();
    }

    /// <summary>A small app: a counting button, a name box, and Ctrl+S that saves.</summary>
    private sealed class CounterApp
    {
        public CounterApp()
        {
            Count = new Button { Name = "Count", Content = "Count", Width = 160, Height = 48, Command = new RelayCommand(() => Clicks++) };
            Name = new TextBox { Name = "Name", Width = 240 };
            Root = new StackPanel { Spacing = 12, Margin = new Thickness(24), Children = { Count, Name } };
            Root.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control), Command = new RelayCommand(() => Saves++) });
            Surface = new Border { Child = Root };
        }

        public Border Surface { get; }

        public StackPanel Root { get; }

        public Button Count { get; }

        public TextBox Name { get; }

        public int Clicks { get; private set; }

        public int Saves { get; private set; }
    }
}
