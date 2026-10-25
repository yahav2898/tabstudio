using System.Text;
using TabStudio.Toolchains.Terminals;

namespace TabStudio.Toolchains.Tests.Terminals;

/// <summary>The terminal engine: what shells and full-screen programs write, and what the screen shows.</summary>
public sealed class TerminalEmulatorTests
{
    private const string Esc = "\u001b";

    [Fact]
    public void Prints_text_and_moves_to_new_lines()
    {
        var terminal = new TerminalEmulator(5, 20);

        terminal.Feed("hello\r\nworld");

        Assert.Equal("hello\nworld", terminal.ScreenText);
        Assert.Equal((1, 5), (terminal.CursorRow, terminal.CursorColumn));
    }

    [Fact]
    public void Wraps_long_lines_and_remembers_they_wrapped()
    {
        var terminal = new TerminalEmulator(4, 10);

        terminal.Feed("0123456789abc");

        Assert.Equal("0123456789\nabc", terminal.ScreenText);
        Assert.True(terminal.ScreenLine(0).IsWrapped);
        Assert.Equal("0123456789abc", terminal.GetText(0, 0, 1, 10));
    }

    [Fact]
    public void Waits_at_the_last_column_until_the_next_character()
    {
        var terminal = new TerminalEmulator(3, 5);

        terminal.Feed("abcde");
        Assert.Equal((0, 4), (terminal.CursorRow, terminal.CursorColumn));

        terminal.Feed("\r\nx");
        Assert.Equal("abcde\nx", terminal.ScreenText);
    }

    [Fact]
    public void Moves_the_cursor_and_erases()
    {
        var terminal = new TerminalEmulator(5, 20);
        terminal.Feed("line one\r\nline two\r\nline three");

        terminal.Feed($"{Esc}[2;6H{Esc}[K");
        Assert.Equal("line one\nline\nline three", terminal.ScreenText);

        terminal.Feed($"{Esc}[1;1H{Esc}[2J");
        Assert.Equal(string.Empty, terminal.ScreenText);
        Assert.Equal((0, 0), (terminal.CursorRow, terminal.CursorColumn));
    }

    [Theory]
    [InlineData("[3A", 0, 10)]
    [InlineData("[2B", 4, 10)]
    [InlineData("[5C", 2, 15)]
    [InlineData("[20D", 2, 0)]
    [InlineData("[G", 2, 0)]
    [InlineData("[8G", 2, 7)]
    [InlineData("[4d", 3, 10)]
    [InlineData("[99;99H", 5, 19)]
    [InlineData("[E", 3, 0)]
    public void Cursor_movements_stay_on_the_screen(string sequence, int row, int column)
    {
        var terminal = new TerminalEmulator(6, 20);
        terminal.Feed($"{Esc}[3;11H");

        terminal.Feed(Esc + sequence);

        Assert.Equal((row, column), (terminal.CursorRow, terminal.CursorColumn));
    }

    [Fact]
    public void Colors_and_attributes_follow_SGR()
    {
        var terminal = new TerminalEmulator(2, 40);

        terminal.Feed($"{Esc}[1;31mR{Esc}[38;5;208mO{Esc}[38;2;10;20;30;48;5;4mX{Esc}[0;7mI{Esc}[mN");

        var line = terminal.ScreenLine(0);
        Assert.Equal(new TerminalStyle(TerminalColor.Indexed(1), default, TerminalAttributes.Bold), line[0].Style);
        Assert.Equal(TerminalColor.Indexed(208), line[1].Style.Foreground);
        Assert.Equal(TerminalColor.FromRgb(10, 20, 30), line[2].Style.Foreground);
        Assert.Equal(TerminalColor.Indexed(4), line[2].Style.Background);
        Assert.Equal(TerminalStyle.Default with { Attributes = TerminalAttributes.Inverse }, line[3].Style);
        Assert.Equal(TerminalStyle.Default, line[4].Style);
        Assert.Equal(0x0A141Eu, line[2].Style.Foreground.ToRgb(0));
        Assert.Equal(0xFFFFFFu, TerminalColor.Indexed(231).ToRgb(0));
    }

    [Fact]
    public void Lines_scrolled_off_the_top_go_to_the_scrollback()
    {
        var terminal = new TerminalEmulator(3, 10, scrollbackLines: 2);

        terminal.Feed("1\r\n2\r\n3\r\n4\r\n5\r\n6");

        Assert.Equal("4\n5\n6", terminal.ScreenText);
        Assert.Equal(2, terminal.ScrollbackCount);
        Assert.Equal(["2", "3", "4", "5", "6"], Enumerable.Range(0, terminal.TotalLines).Select(i => terminal.Line(i).Text));
    }

    [Fact]
    public void A_scroll_region_scrolls_only_its_lines()
    {
        var terminal = new TerminalEmulator(5, 10);
        terminal.Feed("top\r\na\r\nb\r\nc\r\nbottom");

        terminal.Feed($"{Esc}[2;4r{Esc}[4;1H\nd");

        Assert.Equal("top\nb\nc\nd\nbottom", terminal.ScreenText);
        Assert.Equal(0, terminal.ScrollbackCount);
    }

    [Fact]
    public void The_alternate_screen_comes_and_goes_with_the_main_screen_kept()
    {
        var terminal = new TerminalEmulator(4, 20);
        terminal.Feed("$ vi notes.txt");

        terminal.Feed($"{Esc}[?1049h{Esc}[H{Esc}[2J~\r\n~\r\n{Esc}[4;1H\"notes.txt\" 0L");
        Assert.True(terminal.IsAlternateScreen);
        Assert.Equal("~\n~\n\n\"notes.txt\" 0L", terminal.ScreenText);

        terminal.Feed($"{Esc}[?1049l");
        Assert.False(terminal.IsAlternateScreen);
        Assert.Equal("$ vi notes.txt", terminal.ScreenText);
        Assert.Equal((0, 14), (terminal.CursorRow, terminal.CursorColumn));
    }

    [Fact]
    public void Answers_cursor_position_and_device_attribute_queries()
    {
        var terminal = new TerminalEmulator(24, 80);
        var answers = new List<string>();
        terminal.Responded += (_, bytes) => answers.Add(Encoding.UTF8.GetString(bytes));

        terminal.Feed($"{Esc}[5;12H{Esc}[6n{Esc}[c{Esc}[>c{Esc}[5n{Esc}[18t");

        Assert.Equal([$"{Esc}[5;12R", $"{Esc}[?1;2c", $"{Esc}[>1;10;0c", $"{Esc}[0n", $"{Esc}[8;24;80t"], answers);
    }

    [Fact]
    public void Inserts_and_deletes_characters_and_lines()
    {
        var terminal = new TerminalEmulator(4, 10);
        terminal.Feed("abcdef\r\nline2\r\nline3");

        terminal.Feed($"{Esc}[1;3H{Esc}[2@XY");
        Assert.Equal("abXYcdef", terminal.ScreenLine(0).Text);

        terminal.Feed($"{Esc}[1;1H{Esc}[3P");
        Assert.Equal("Ycdef\nline2\nline3", terminal.ScreenText);

        terminal.Feed($"{Esc}[2;1H{Esc}[L");
        Assert.Equal(["Ycdef", string.Empty, "line2", "line3"], Enumerable.Range(0, 4).Select(r => terminal.ScreenLine(r).Text));

        terminal.Feed($"{Esc}[M{Esc}[M");
        Assert.Equal(["Ycdef", "line3", string.Empty, string.Empty], Enumerable.Range(0, 4).Select(r => terminal.ScreenLine(r).Text));
    }

    [Fact]
    public void Wide_characters_take_two_columns()
    {
        var terminal = new TerminalEmulator(2, 10);

        terminal.Feed("a中b😀c");

        var line = terminal.ScreenLine(0);
        Assert.Equal("a中b😀c", line.Text);
        Assert.Equal(7, terminal.CursorColumn);
        Assert.True(line[2].IsContinuation);
        Assert.Equal(2, line[4].Width);
    }

    [Fact]
    public void Combining_marks_take_no_column()
    {
        var terminal = new TerminalEmulator(2, 10);

        terminal.Feed("éx");

        Assert.Equal(2, terminal.CursorColumn);
    }

    [Fact]
    public void Draws_lines_with_the_DEC_graphics_set()
    {
        var terminal = new TerminalEmulator(2, 10);

        terminal.Feed($"{Esc}(0lqqk{Esc}(B ok");

        Assert.Equal("┌──┐ ok", terminal.ScreenLine(0).Text);
    }

    [Fact]
    public void A_character_split_between_two_reads_arrives_whole()
    {
        var terminal = new TerminalEmulator(2, 10);
        var bytes = Encoding.UTF8.GetBytes("é→");

        terminal.Feed(bytes.AsSpan(0, 1));
        terminal.Feed(bytes.AsSpan(1, 3));
        terminal.Feed(bytes.AsSpan(4));

        Assert.Equal("é→", terminal.ScreenLine(0).Text);
    }

    [Fact]
    public void Keeps_the_cursor_line_in_view_when_the_screen_gets_shorter()
    {
        var terminal = new TerminalEmulator(5, 20);
        terminal.Feed("1\r\n2\r\n3\r\n4\r\n$ ");

        terminal.Resize(3, 10);

        Assert.Equal("3\n4\n$", terminal.ScreenText);
        Assert.Equal((2, 2), (terminal.CursorRow, terminal.CursorColumn));
        Assert.Equal(2, terminal.ScrollbackCount);
        Assert.Equal((3, 10), (terminal.Rows, terminal.Columns));
    }

    [Fact]
    public void Takes_the_title_from_OSC_and_ignores_other_strings()
    {
        var terminal = new TerminalEmulator(2, 20);

        terminal.Feed($"{Esc}]0;ash: ~/project\u0007{Esc}P+q544e{Esc}\\{Esc}]10;?{Esc}\\ok");

        Assert.Equal("ash: ~/project", terminal.Title);
        Assert.Equal("ok", terminal.ScreenText);
    }

    [Fact]
    public void Tracks_the_modes_full_screen_programs_set()
    {
        var terminal = new TerminalEmulator(2, 20);

        terminal.Feed($"{Esc}[?1h{Esc}[?25l{Esc}[?2004h");
        Assert.True(terminal.ApplicationCursorKeys);
        Assert.False(terminal.CursorVisible);
        Assert.True(terminal.BracketedPaste);

        terminal.Feed($"{Esc}c");
        Assert.False(terminal.ApplicationCursorKeys);
        Assert.True(terminal.CursorVisible);
        Assert.False(terminal.BracketedPaste);
    }

    [Fact]
    public void Shows_a_screen_as_busybox_top_draws_it()
    {
        var terminal = new TerminalEmulator(6, 40);

        terminal.Feed(
            $"{Esc}[H{Esc}[JMem: 812340K used, 1130984K free\r\n"
            + $"Load average: 0.08 0.12 0.10 1/312 4521\r\n"
            + $"{Esc}[7m  PID  PPID USER     STAT   VSZ %CPU COMMAND{Esc}[m\r\n"
            + "  4521     1 root     R     1640   0% top\r\n"
            + $"{Esc}[H{Esc}[JMem: 812400K used");

        Assert.Equal("Mem: 812400K used", terminal.ScreenText);
        terminal.Feed($"\r\n\r\n{Esc}[7m  PID{Esc}[m");
        Assert.True(terminal.ScreenLine(2)[2].Style.Has(TerminalAttributes.Inverse));
    }

    [Fact]
    public void Ignores_malformed_and_unknown_sequences()
    {
        var terminal = new TerminalEmulator(2, 20);

        terminal.Feed($"a{Esc}[?999h{Esc}[12;34;56;78;90;12;34;56;78;90;12;34;56;78;90;12;34;56;78;90;12;34;56;78;90;12;34;56;78;90;12;34;56;78;90zb{Esc}[=5uc{Esc}]x\u0007d\u0018e");

        Assert.Equal("abcde", terminal.ScreenText);
    }

    [Fact]
    public void Selected_text_keeps_the_line_breaks_programs_wrote()
    {
        var terminal = new TerminalEmulator(4, 10);
        terminal.Feed("first\r\nsecond line!\r\nthird");

        Assert.Equal("rst\nsecond line!\nth", terminal.GetText(0, 2, 3, 2));
        Assert.Equal("rst\nsecond line!\nth", terminal.GetText(3, 2, 0, 2));
    }
}
