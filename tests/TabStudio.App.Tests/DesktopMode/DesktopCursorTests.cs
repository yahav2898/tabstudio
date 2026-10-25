using Avalonia;
using TabStudio.App.Platform;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>
/// The pointer TabStudio moves on the external display by the tablet's captured mouse: where it starts, how fast it
/// goes, and that it stays on the display.
/// </summary>
public sealed class DesktopCursorTests
{
    [Fact]
    public void The_pointer_starts_in_the_middle_of_the_display_in_its_units_and_pixels()
    {
        var cursor = new DesktopCursor();

        cursor.Reset(1920, 1080, 1.5);

        Assert.Equal(new Point(960, 540), cursor.PixelPosition);
        Assert.Equal(new Point(640, 360), cursor.Position);
        Assert.False(cursor.IsVisible);
    }

    [Fact]
    public void Slow_movements_move_one_pixel_a_count_and_fast_ones_speed_up()
    {
        Assert.Equal(1, DesktopCursor.Gain(0));
        Assert.Equal(1, DesktopCursor.Gain(DesktopCursor.PreciseDistance));
        Assert.Equal(1.75, DesktopCursor.Gain(16), 6);
        Assert.Equal(DesktopCursor.MaxGain, DesktopCursor.Gain(26));
        Assert.Equal(DesktopCursor.MaxGain, DesktopCursor.Gain(500));

        var cursor = new DesktopCursor();
        cursor.Reset(1920, 1080, 1);
        Assert.Equal(new Point(963, 544), cursor.MoveBy(3, 4));
        Assert.Equal(new Point(963 + 28, 544), cursor.MoveBy(16, 0));
    }

    [Fact]
    public void Fractions_of_slow_movements_add_up()
    {
        var cursor = new DesktopCursor();
        cursor.Reset(100, 100, 1);

        for (var i = 0; i < 4; i++)
        {
            cursor.MoveBy(0.25, 0);
        }

        Assert.Equal(new Point(51, 50), cursor.PixelPosition);
    }

    [Fact]
    public void The_pointer_stays_on_the_display_and_where_it_was_when_the_display_changes()
    {
        var cursor = new DesktopCursor();
        cursor.Reset(1920, 1080, 1);

        Assert.Equal(new Point(0, 0), cursor.MoveBy(-100_000, -100_000));
        Assert.Equal(new Point(1919, 1079), cursor.MoveBy(100_000, 100_000));

        cursor.MoveTo(300, 200);
        cursor.Reset(2560, 1440, 2);
        Assert.Equal(new Point(300, 200), cursor.PixelPosition);
        Assert.Equal(new Point(150, 100), cursor.Position);

        cursor.Reset(200, 100, 1);
        Assert.Equal(new Point(199, 99), cursor.PixelPosition);
    }

    [Fact]
    public void Showing_and_hiding_the_pointer_is_observable()
    {
        var cursor = new DesktopCursor();
        var changes = new List<string?>();
        cursor.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        cursor.Show();
        Assert.True(cursor.IsVisible);
        cursor.Hide();
        Assert.False(cursor.IsVisible);
        cursor.MoveTo(5, 5);

        Assert.Equal(new string?[] { nameof(DesktopCursor.IsVisible), nameof(DesktopCursor.IsVisible), nameof(DesktopCursor.Position) }, changes);
    }
}
