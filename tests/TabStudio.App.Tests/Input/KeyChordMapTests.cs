using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Input;

namespace TabStudio.App.Tests.Input;

public sealed class KeyChordMapTests
{
    [Fact]
    public void The_second_key_runs_the_chord_and_is_swallowed()
    {
        var ran = new List<string>();
        var map = new KeyChordMap();
        map.Add(Key.R, KeyModifiers.Control, Key.A, () => new RelayCommand(() => ran.Add("all")));
        map.Add(Key.R, KeyModifiers.Control, Key.F, () => new RelayCommand(() => ran.Add("failed")));

        Assert.False(map.HandleKeyDown(Key.LeftCtrl, KeyModifiers.Control));
        Assert.False(map.HandleKeyDown(Key.R, KeyModifiers.Control));
        Assert.True(map.IsPending);
        Assert.True(map.HandleKeyDown(Key.F, KeyModifiers.None));

        Assert.Equal(new[] { "failed" }, ran);
        Assert.False(map.IsPending);
    }

    [Fact]
    public void Another_key_ends_the_chord_and_passes_through()
    {
        var ran = false;
        var map = new KeyChordMap();
        map.Add(Key.R, KeyModifiers.Control, Key.A, () => new RelayCommand(() => ran = true));

        map.HandleKeyDown(Key.R, KeyModifiers.Control);

        Assert.False(map.HandleKeyDown(Key.X, KeyModifiers.None));
        Assert.False(map.HandleKeyDown(Key.A, KeyModifiers.None));
        Assert.False(ran);
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
