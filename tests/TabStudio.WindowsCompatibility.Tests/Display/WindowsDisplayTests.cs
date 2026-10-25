using TabStudio.Testing;
using TabStudio.WindowsCompatibility.Display;
using TabStudio.WindowsCompatibility.Display.X11;
using TabStudio.WindowsCompatibility.Input;
using TabStudio.WindowsCompatibility.Tests.Infrastructure;

namespace TabStudio.WindowsCompatibility.Tests.Display;

/// <summary>The desktop's frames and input, against a fake X server and a synthetic framebuffer.</summary>
public sealed class WindowsDisplayTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Connects_with_the_cookie_and_finds_xtest_and_damage()
    {
        var cookie = X11Cookie.Create();
        await using var server = new FakeXServer(_temp.Resolve("X1"), cookie.Data.ToArray());
        using var framebuffer = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 320, 240);

        await using var display = await WindowsDisplay.ConnectAsync(new DisplayConnection(X11Endpoint.Unix(server.SocketPath), framebuffer.Path, cookie), (320, 240), TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.True(display.ReportsDamage);
        Assert.Equal((320, 240), display.ScreenSize);
        await WaitUntilAsync(() => server.DamageCreated == 1);
    }

    [Fact]
    public async Task A_wrong_cookie_is_refused_at_once()
    {
        await using var server = new FakeXServer(_temp.Resolve("X2"), X11Cookie.Create().Data.ToArray());
        using var framebuffer = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 320, 240);

        var refused = await Assert.ThrowsAsync<X11Exception>(() => WindowsDisplay.ConnectAsync(
            new DisplayConnection(X11Endpoint.Unix(server.SocketPath), framebuffer.Path, X11Cookie.Create()), (320, 240), TimeSpan.FromSeconds(30), CancellationToken.None));

        Assert.True(refused.Refused);
        Assert.Contains("refused", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pointer_input_reaches_the_server_in_order_through_xtest()
    {
        await using var server = new FakeXServer(_temp.Resolve("X3"));
        using var framebuffer = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 320, 240);
        await using var display = await Connect(server, framebuffer);

        display.MovePointer(120, 84);
        display.Button(PointerButton.Left, pressed: true);
        display.Button(PointerButton.Left, pressed: false);
        display.Wheel(horizontal: -1, vertical: 2);
        await display.FlushInputAsync();

        Assert.Equal(
            [new FakeInput(6, 0, 120, 84), new FakeInput(4, 1, 0, 0), new FakeInput(5, 1, 0, 0),
             new FakeInput(4, 4, 0, 0), new FakeInput(5, 4, 0, 0), new FakeInput(4, 4, 0, 0), new FakeInput(5, 4, 0, 0),
             new FakeInput(4, 6, 0, 0), new FakeInput(5, 6, 0, 0)],
            server.Inputs);
    }

    [Fact]
    public async Task Keys_and_text_use_the_keyboard_mapping_and_borrow_spare_keys()
    {
        await using var server = new FakeXServer(_temp.Resolve("X4"));
        using var framebuffer = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 320, 240);
        await using var display = await Connect(server, framebuffer);

        display.Key(WindowsKey.Control, pressed: true);
        display.Key(WindowsKey.Control, pressed: false);
        display.Type("aAש");
        await display.FlushInputAsync();

        var inputs = server.Inputs.ToList();

        // Ctrl is key 37; "a" is key 38, "A" the same key with Shift (50), and "ש" borrows the highest spare key code.
        Assert.Equal(new FakeInput(2, 37, 0, 0), inputs[0]);
        Assert.Equal(new FakeInput(3, 37, 0, 0), inputs[1]);
        Assert.Equal([new FakeInput(2, 38, 0, 0), new FakeInput(3, 38, 0, 0)], inputs[2..4]);
        Assert.Equal([new FakeInput(2, 50, 0, 0), new FakeInput(2, 38, 0, 0), new FakeInput(3, 38, 0, 0), new FakeInput(3, 50, 0, 0)], inputs[4..8]);
        var (keycode, keysym) = Assert.Single(server.Remapped);
        Assert.Equal(FakeXServer.LastKeycode, keycode);
        Assert.Equal(0x01000000u | 'ש', keysym);
        Assert.Equal([new FakeInput(2, keycode, 0, 0), new FakeInput(3, keycode, 0, 0)], inputs[8..10]);
    }

    [Fact]
    public async Task A_damage_event_reports_the_changed_area_and_its_pixels_come_from_the_framebuffer()
    {
        await using var server = new FakeXServer(_temp.Resolve("X5"));
        using var framebuffer = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 320, 240);
        await using var display = await Connect(server, framebuffer);
        var frames = new List<PixelRect>();
        display.FrameChanged += (_, area) =>
        {
            lock (frames)
            {
                frames.Add(area);
            }
        };
        await WaitUntilAsync(() => Count(frames) > 0);

        framebuffer.Fill(10, 20, 30, 40, blue: 1, green: 2, red: 3);
        await server.SendDamageAsync(10, 20, 30, 40);
        await WaitUntilAsync(() => Snapshot(frames).Any(f => f == new PixelRect(10, 20, 30, 40)));

        var pixels = new byte[320 * 240 * 4];
        display.CopyPixels(new PixelRect(10, 20, 30, 40), pixels, 320 * 4);
        var offset = ((20 * 320) + 10) * 4;
        Assert.Equal(new byte[] { 1, 2, 3 }, pixels[offset..(offset + 3)]);
        Assert.Equal(0, pixels[0]);
        await WaitUntilAsync(() => Volatile.Read(ref server.DamageCleared) > 0);
    }

    [Fact]
    public async Task Resizing_the_desktop_configures_wines_desktop_window()
    {
        await using var server = new FakeXServer(_temp.Resolve("X6"));
        using var framebuffer = new SyntheticFramebuffer(_temp.Resolve("Xvfb_screen0"), 1280, 800);
        await using var display = await Connect(server, framebuffer, (1024, 720));

        await display.ResizeDesktopAsync(800, 600);
        await WaitUntilAsync(() => !server.Configured.IsEmpty);

        Assert.Equal((FakeXServer.DesktopWindow, 0, 0, 800, 600), Assert.Single(server.Configured));
        Assert.Equal((800, 600), display.DesktopSize);
    }

    private static Task<WindowsDisplay> Connect(FakeXServer server, SyntheticFramebuffer framebuffer, (int, int)? desktop = null) =>
        WindowsDisplay.ConnectAsync(new DisplayConnection(X11Endpoint.Unix(server.SocketPath), framebuffer.Path, null), desktop ?? (framebuffer.Width, framebuffer.Height), TimeSpan.FromSeconds(10), CancellationToken.None);

    private static int Count<T>(List<T> list)
    {
        lock (list)
        {
            return list.Count;
        }
    }

    private static List<T> Snapshot<T>(List<T> list)
    {
        lock (list)
        {
            return [.. list];
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not come true in time.");
            await Task.Delay(20);
        }
    }
}
