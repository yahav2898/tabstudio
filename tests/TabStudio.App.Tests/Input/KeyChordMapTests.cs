using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Input;

namespace TabStudio.App.Tests.Input;

public sealed class KeyChordMapTests
{
    [Fact]
    public void Both_keys_of_a_chord_are_swallowed_and_the_second_runs_it()
    {
        var ran = new List<string>();
        var map = new KeyChordMap();
        map.Add(Key.R, KeyModifiers.Control, Key.A, () => new RelayCommand(() => ran.Add("all")));
        map.Add(Key.R, KeyModifiers.Control, Key.F, () => new RelayCommand(() => ran.Add("failed")));

        Assert.False(map.HandleKeyDown(Key.LeftCtrl, KeyModifiers.Control));
        Assert.True(map.HandleKeyDown(Key.R, KeyModifiers.Control));
        Assert.True(map.IsPending);
        Assert.True(map.HandleKeyDown(Key.F, KeyModifiers.None));

        Assert.Equal(new[] { "failed" }, ran);
        Assert.False(map.IsPending);
    }

    [Fact]
    public void A_second_key_that_finishes_no_chord_is_swallowed_and_reported()
    {
        var ran = false;
        var messages = new List<string>();
        var map = new KeyChordMap();
        map.Add(Key.R, KeyModifiers.Control, Key.A, () => new RelayCommand(() => ran = true));
        map.NotACommand += (_, message) => messages.Add(message);

        map.HandleKeyDown(Key.R, KeyModifiers.Control);

        Assert.True(map.HandleKeyDown(Key.X, KeyModifiers.None));
        Assert.False(map.HandleKeyDown(Key.A, KeyModifiers.None));
        Assert.False(ran);
        Assert.Equal(new[] { "The key combination (Ctrl+R, X) is not a command." }, messages);
    }

    [Fact]
    public void Chords_that_share_a_first_key_tell_their_second_keys_apart_by_modifiers()
    {
        var ran = new List<string>();
        var map = new KeyChordMap();
        map.Add(new KeyGesture(Key.R, KeyModifiers.Control), new KeyGesture(Key.R, KeyModifiers.Control), () => new RelayCommand(() => ran.Add("rename")));
        map.Add(Key.R, KeyModifiers.Control, Key.A, () => new RelayCommand(() => ran.Add("all")));

        map.HandleKeyDown(Key.R, KeyModifiers.Control);
        map.HandleKeyDown(Key.R, KeyModifiers.Control);
        Assert.False(map.IsPending);
        Assert.False(map.HandleKeyDown(Key.A, KeyModifiers.None));
        map.HandleKeyDown(Key.R, KeyModifiers.Control);
        map.HandleKeyDown(Key.A, KeyModifiers.None);

        Assert.Equal(new[] { "rename", "all" }, ran);
    }

    [Fact]
    public void A_chord_whose_command_does_not_apply_now_does_nothing()
    {
        var map = new KeyChordMap();
        map.Add(Key.K, KeyModifiers.Control, Key.I, () => null);

        map.HandleKeyDown(Key.K, KeyModifiers.Control);

        Assert.True(map.HandleKeyDown(Key.I, KeyModifiers.None));
        Assert.False(map.IsPending);
    }

    [Fact]
    public void Cancel_forgets_a_pending_chord()
    {
        var messages = new List<string?>();
        var map = new KeyChordMap();
        map.Add(Key.R, KeyModifiers.Control, Key.A, () => new RelayCommand(() => { }));
        map.PendingChanged += (_, text) => messages.Add(text);

        map.HandleKeyDown(Key.R, KeyModifiers.Control);
        map.Cancel();

        Assert.False(map.IsPending);
        Assert.False(map.HandleKeyDown(Key.A, KeyModifiers.None));
        Assert.Null(messages[^1]);
    }

    [Fact]
    public void The_status_bar_hears_when_a_chord_starts_and_ends()
    {
        var messages = new List<string?>();
        var map = new KeyChordMap();
        map.Add(Key.E, KeyModifiers.Control, Key.T, () => new RelayCommand<string>(_ => { }), "parameter");
        map.PendingChanged += (_, text) => messages.Add(text);

        map.HandleKeyDown(Key.E, KeyModifiers.Control);
        map.HandleKeyDown(Key.T, KeyModifiers.None);

        Assert.Equal(new[] { "(Ctrl+E) was pressed. Waiting for second key of chord...", null }, messages);
    }

    [Fact]
    public void A_disabled_command_does_not_run()
    {
        var ran = false;
        var map = new KeyChordMap();
        map.Add(Key.R, KeyModifiers.Control, Key.L, () => new RelayCommand(() => ran = true, () => false));

        map.HandleKeyDown(Key.R, KeyModifiers.Control);

        Assert.True(map.HandleKeyDown(Key.L, KeyModifiers.None));
        Assert.False(ran);
    }
}
