using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Windows.Capture;
using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows.Desktop;

/// <summary>
/// Plays the tablet's input into a window on the PC: with <c>SendInput</c>, after bringing the window to the front,
/// when the PC is free, or as window messages to the control under the pointer and the focused control, so a person
/// working at the PC keeps their mouse, keyboard and focus.
/// </summary>
internal sealed unsafe class WindowsInputPlayer
{
    /// <summary>The keys Windows marks as extended: the navigation block, the right-hand modifiers and others.</summary>
    private static readonly HashSet<int> ExtendedKeys = [0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2C, 0x2D, 0x2E, 0x5B, 0x5C, 0x5D, 0x6F, 0x90, 0xA3, 0xA5];

    private readonly Lock _gate = new();
    private uint _lastInjected;
    private uint _userInput = NativeMethods.GetTickCount();
    private nuint _held;
    private (uint Time, int X, int Y, BridgePointerButton Button) _lastDown;

    /// <summary>How input is played.</summary>
    public BridgeInputPolicy Policy { get; set; }

    /// <summary>How long nobody must have used the PC's mouse and keyboard before the bridge uses them.</summary>
    public TimeSpan IdleBeforeForeground { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>How the last input was played, <c>SendInput</c> or <c>window messages</c>, for the bridge's window and tests.</summary>
    public string? LastMode { get; private set; }

    /// <summary>Plays input into a window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="events">The input, positions in the window's pixels from its visible frame's top-left corner.</param>
    public void Play(nint window, IReadOnlyList<BridgeInputEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (!NativeMethods.IsWindow(window) || events.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            var bounds = WindowBounds.Visible(window);
            if (UsesForeground(window, events))
            {
                LastMode = "SendInput";
                BringToFront(window);
                foreach (var e in events)
                {
                    Send(bounds, e);
                }

                _lastInjected = NativeMethods.GetTickCount();
            }
            else
            {
                LastMode = "window messages";
                foreach (var e in events)
                {
                    Post(window, bounds, e);
                }
            }
        }
    }

    private bool UsesForeground(nint window, IReadOnlyList<BridgeInputEvent> events)
    {
        switch (Policy)
        {
            case BridgeInputPolicy.Foreground:
                return true;
            case BridgeInputPolicy.Background:
                return false;
        }

        // Input that is not the bridge's own, after its last injection, is a person at the PC.
        var last = new NativeMethods.LastInputInfo { Size = (uint)sizeof(NativeMethods.LastInputInfo) };
        if (NativeMethods.GetLastInputInfo(ref last) && unchecked((int)(last.Time - _lastInjected)) > 250)
        {
            _userInput = last.Time;
        }

        var idle = TimeSpan.FromMilliseconds(unchecked(NativeMethods.GetTickCount() - _userInput));
        var shortcut = events.Any(e => e.Kind is BridgeInputKind.KeyDown && VirtualKeys.IsModifier(e.VirtualKey) && e.VirtualKey != VirtualKeys.Shift);
        return idle >= IdleBeforeForeground || IsInFront(window) || shortcut;
    }

    private static bool IsInFront(nint window)
    {
        var foreground = NativeMethods.GetForegroundWindow();
        return foreground == window || NativeMethods.GetWindow(foreground, NativeMethods.GwOwner) == window;
    }

    /// <summary>Brings a window to the front, as its program could: by joining the input of the window in front for a moment.</summary>
    private static void BringToFront(nint window)
    {
        if (IsInFront(window))
        {
            return;
        }

        if (NativeMethods.IsIconic(window))
        {
            _ = NativeMethods.ShowWindow(window, NativeMethods.SwRestore);
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = foreground == 0 ? 0 : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var thisThread = NativeMethods.GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != thisThread && NativeMethods.AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            _ = NativeMethods.BringWindowToTop(window);
            _ = NativeMethods.SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                _ = NativeMethods.AttachThreadInput(thisThread, foregroundThread, false);
            }
        }
    }

    private static void Send(NativeMethods.Rect bounds, BridgeInputEvent e)
    {
        switch (e.Kind)
        {
            case BridgeInputKind.PointerMove:
                SendMouse(bounds, e, NativeMethods.MouseEventMove, 0);
                break;
            case BridgeInputKind.PointerDown:
                var down = e.Button switch
                {
                    BridgePointerButton.Right => NativeMethods.MouseEventRightDown,
                    BridgePointerButton.Middle => NativeMethods.MouseEventMiddleDown,
                    _ => NativeMethods.MouseEventLeftDown,
                };
                SendMouse(bounds, e, NativeMethods.MouseEventMove | down, 0);
                break;
            case BridgeInputKind.PointerUp:
                var up = e.Button switch
                {
                    BridgePointerButton.Right => NativeMethods.MouseEventRightUp,
                    BridgePointerButton.Middle => NativeMethods.MouseEventMiddleUp,
                    _ => NativeMethods.MouseEventLeftUp,
                };
                SendMouse(bounds, e, NativeMethods.MouseEventMove | up, 0);
                break;
            case BridgeInputKind.Wheel:
                if (e.DeltaY != 0)
                {
                    SendMouse(bounds, e, NativeMethods.MouseEventMove | NativeMethods.MouseEventWheel, unchecked((uint)e.DeltaY));
                }

                if (e.DeltaX != 0)
                {
                    SendMouse(bounds, e, NativeMethods.MouseEventMove | NativeMethods.MouseEventHWheel, unchecked((uint)e.DeltaX));
                }

                break;
            case BridgeInputKind.KeyDown or BridgeInputKind.KeyUp:
                SendKey((ushort)e.VirtualKey, (ushort)NativeMethods.MapVirtualKey((uint)e.VirtualKey, NativeMethods.MapVkVkToVsc), e.Kind == BridgeInputKind.KeyUp, unicode: false);
                break;
            case BridgeInputKind.Text:
                foreach (var c in e.Text ?? string.Empty)
                {
                    SendKey(0, c, up: false, unicode: true);
                    SendKey(0, c, up: true, unicode: true);
                }

                break;
        }
    }

    private static void SendMouse(NativeMethods.Rect bounds, BridgeInputEvent e, uint flags, uint data)
    {
        var left = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var width = Math.Max(2, NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen));
        var height = Math.Max(2, NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen));
        var x = bounds.Left + e.X;
        var y = bounds.Top + e.Y;
        var input = new NativeMethods.Input
        {
            Type = NativeMethods.InputMouse,
            Data = new NativeMethods.InputUnion
            {
                Mouse = new NativeMethods.MouseInput
                {
                    Dx = (int)(((long)(x - left) * 65535) / (width - 1)),
                    Dy = (int)(((long)(y - top) * 65535) / (height - 1)),
                    MouseData = data,
                    Flags = flags | NativeMethods.MouseEventAbsolute | NativeMethods.MouseEventVirtualDesk,
                },
            },
        };
        _ = NativeMethods.SendInput(1, &input, sizeof(NativeMethods.Input));
    }

    private static void SendKey(ushort virtualKey, ushort scanCode, bool up, bool unicode)
    {
        var flags = (up ? NativeMethods.KeyEventKeyUp : 0) | (unicode ? NativeMethods.KeyEventUnicode : 0) | (!unicode && ExtendedKeys.Contains(virtualKey) ? NativeMethods.KeyEventExtendedKey : 0);
        var input = new NativeMethods.Input
        {
            Type = NativeMethods.InputKeyboard,
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KeyboardInput { VirtualKey = virtualKey, ScanCode = scanCode, Flags = flags },
            },
        };
        _ = NativeMethods.SendInput(1, &input, sizeof(NativeMethods.Input));
    }

    private void Post(nint window, NativeMethods.Rect bounds, BridgeInputEvent e)
    {
        switch (e.Kind)
        {
            case BridgeInputKind.PointerMove or BridgeInputKind.PointerDown or BridgeInputKind.PointerUp:
                PostPointer(window, bounds, e);
                break;
            case BridgeInputKind.Wheel:
                var screen = PackPoint(bounds.Left + e.X, bounds.Top + e.Y);
                var (target, _) = ControlAt(window, bounds.Left + e.X, bounds.Top + e.Y);
                if (e.DeltaY != 0)
                {
                    _ = NativeMethods.PostMessage(target, NativeMethods.WmMouseWheel, PackWheel(e.DeltaY), screen);
                }

                if (e.DeltaX != 0)
                {
                    _ = NativeMethods.PostMessage(target, NativeMethods.WmMouseHWheel, PackWheel(e.DeltaX), screen);
                }

                break;
            case BridgeInputKind.KeyDown or BridgeInputKind.KeyUp:
                var focus = FocusOf(window);
                var scan = NativeMethods.MapVirtualKey((uint)e.VirtualKey, NativeMethods.MapVkVkToVsc);
                var parameter = 1L | ((long)scan << 16) | (ExtendedKeys.Contains(e.VirtualKey) ? 1L << 24 : 0);
                if (e.Kind == BridgeInputKind.KeyUp)
                {
                    parameter |= (1L << 30) | (1L << 31);
                }

                _ = NativeMethods.PostMessage(focus, e.Kind == BridgeInputKind.KeyUp ? NativeMethods.WmKeyUp : NativeMethods.WmKeyDown, (nuint)e.VirtualKey, (nint)parameter);
                break;
            case BridgeInputKind.Text:
                var textFocus = FocusOf(window);
                foreach (var c in e.Text ?? string.Empty)
                {
                    _ = NativeMethods.PostMessage(textFocus, NativeMethods.WmChar, c, 1);
                }

                break;
        }
    }

    private void PostPointer(nint window, NativeMethods.Rect bounds, BridgeInputEvent e)
    {
        var (target, client) = ControlAt(window, bounds.Left + e.X, bounds.Top + e.Y);
        var position = PackPoint(client.X, client.Y);
        var button = e.Button switch
        {
            BridgePointerButton.Left => NativeMethods.MkLButton,
            BridgePointerButton.Right => NativeMethods.MkRButton,
            BridgePointerButton.Middle => NativeMethods.MkMButton,
            _ => (nuint)0,
        };
        switch (e.Kind)
        {
            case BridgeInputKind.PointerMove:
                _ = NativeMethods.PostMessage(target, NativeMethods.WmMouseMove, _held | button, position);
                break;
            case BridgeInputKind.PointerDown:
                _held |= button;
                var now = NativeMethods.GetTickCount();
                var doubleClick = _lastDown.Button == e.Button
                    && now - _lastDown.Time <= NativeMethods.GetDoubleClickTime()
                    && Math.Abs(e.X - _lastDown.X) <= NativeMethods.GetSystemMetrics(NativeMethods.SmCxDoubleClick) / 2
                    && Math.Abs(e.Y - _lastDown.Y) <= NativeMethods.GetSystemMetrics(NativeMethods.SmCyDoubleClick) / 2
                    && (NativeMethods.GetClassLongPtr(target, NativeMethods.GclStyle) & NativeMethods.CsDoubleClicks) != 0;
                _lastDown = doubleClick ? default : (now, e.X, e.Y, e.Button);
                var message = (e.Button, doubleClick) switch
                {
                    (BridgePointerButton.Right, false) => NativeMethods.WmRButtonDown,
                    (BridgePointerButton.Right, true) => NativeMethods.WmRButtonDoubleClick,
                    (BridgePointerButton.Middle, _) => NativeMethods.WmMButtonDown,
                    (_, true) => NativeMethods.WmLButtonDoubleClick,
                    _ => NativeMethods.WmLButtonDown,
                };
                _ = NativeMethods.PostMessage(target, message, _held, position);
                break;
            case BridgeInputKind.PointerUp:
                _held &= ~button;
                var up = e.Button switch
                {
                    BridgePointerButton.Right => NativeMethods.WmRButtonUp,
                    BridgePointerButton.Middle => NativeMethods.WmMButtonUp,
                    _ => NativeMethods.WmLButtonUp,
                };
                _ = NativeMethods.PostMessage(target, up, _held, position);
                break;
        }
    }

    /// <summary>The deepest visible, enabled control under a point of the screen, and the point in its client area.</summary>
    private static (nint Control, NativeMethods.Point Client) ControlAt(nint window, int x, int y)
    {
        var current = window;
        for (var depth = 0; depth < 32; depth++)
        {
            var point = new NativeMethods.Point { X = x, Y = y };
            _ = NativeMethods.ScreenToClient(current, ref point);
            var child = NativeMethods.ChildWindowFromPointEx(current, point, NativeMethods.CwpSkipInvisible | NativeMethods.CwpSkipDisabled | NativeMethods.CwpSkipTransparent);
            if (child == 0 || child == current)
            {
                break;
            }

            current = child;
        }

        var client = new NativeMethods.Point { X = x, Y = y };
        _ = NativeMethods.ScreenToClient(current, ref client);
        return (current, client);
    }

    /// <summary>The control with the keyboard focus in a window's thread, or the window itself.</summary>
    private static nint FocusOf(nint window)
    {
        var thread = NativeMethods.GetWindowThreadProcessId(window, out _);
        var info = new NativeMethods.GuiThreadInfo { Size = (uint)sizeof(NativeMethods.GuiThreadInfo) };
        return NativeMethods.GetGUIThreadInfo(thread, ref info) && info.Focus != 0 ? info.Focus : window;
    }

    private static nint PackPoint(int x, int y) => (nint)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    private static nuint PackWheel(int delta) => (nuint)(uint)((delta & 0xFFFF) << 16);
}
