#nullable enable

using System;
using Uno.UI.Hosting;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;

namespace TabStudio.UnoHost;

/// <summary>What happened to a pointer, as TabStudio reports it.</summary>
public enum PreviewPointerAction
{
    /// <summary>It moved.</summary>
    Moved,

    /// <summary>A finger touched, or a button went down.</summary>
    Pressed,

    /// <summary>The finger lifted, or the button went up.</summary>
    Released,

    /// <summary>A mouse or pen came over the preview.</summary>
    Entered,

    /// <summary>A mouse or pen left the preview.</summary>
    Exited,

    /// <summary>The system took the pointer away, as when a gesture becomes a scroll of the IDE.</summary>
    Cancelled,

    /// <summary>The mouse wheel turned, or a touchpad scrolled.</summary>
    Wheel,
}

/// <summary>
/// The pointer input of the app preview: touch, mouse and pen events from TabStudio, raised on Uno's UI thread as the
/// input of the host's window.
/// </summary>
internal sealed class PreviewPointerInput : IUnoCorePointerInputSource
{
    private readonly IXamlRootHost _host;
    private Point _position;

    public PreviewPointerInput(IXamlRootHost host) => _host = host;

#pragma warning disable CS0067 // Capture is TabStudio's: the preview keeps receiving a pointer that went down on it.
    public event TypedEventHandler<object, PointerEventArgs>? PointerCaptureLost;
#pragma warning restore CS0067
    public event TypedEventHandler<object, PointerEventArgs>? PointerEntered;
    public event TypedEventHandler<object, PointerEventArgs>? PointerExited;
    public event TypedEventHandler<object, PointerEventArgs>? PointerMoved;
    public event TypedEventHandler<object, PointerEventArgs>? PointerPressed;
    public event TypedEventHandler<object, PointerEventArgs>? PointerReleased;
    public event TypedEventHandler<object, PointerEventArgs>? PointerWheelChanged;
    public event TypedEventHandler<object, PointerEventArgs>? PointerCancelled;

    public bool HasCapture => false;

    public CoreCursor PointerCursor { get; set; } = new(CoreCursorType.Arrow, 0);

    public Point PointerPosition => _position;

    public void SetPointerCapture()
    {
    }

    public void ReleasePointerCapture()
    {
    }

    public void SetPointerCapture(PointerIdentifier pointer)
    {
    }

    public void ReleasePointerCapture(PointerIdentifier pointer)
    {
    }

    /// <summary>Raises one pointer event. Any thread.</summary>
    public void Raise(
        PreviewPointerAction action,
        uint pointerId,
        PointerDeviceType device,
        double x,
        double y,
        bool left,
        bool right,
        bool middle,
        int wheelDelta,
        bool horizontalWheel,
        VirtualKeyModifiers modifiers,
        ulong timestamp)
    {
        var position = new Point(x, y);
        var properties = new PointerPointProperties
        {
            IsPrimary = true,
            IsInRange = action is not PreviewPointerAction.Exited and not PreviewPointerAction.Cancelled,
            IsLeftButtonPressed = left,
            IsRightButtonPressed = right,
            IsMiddleButtonPressed = middle,
            MouseWheelDelta = wheelDelta,
            IsHorizontalMouseWheel = horizontalWheel,
            PointerUpdateKind = UpdateKind(action, left, right, middle, device),
        };

        var point = new PointerPoint(
            frameId: (uint)(timestamp & uint.MaxValue),
            timestamp: timestamp,
            device: PointerDevice.For(device),
            pointerId: pointerId,
            rawPosition: position,
            position: position,
            isInContact: device == PointerDeviceType.Touch ? action is PreviewPointerAction.Pressed or PreviewPointerAction.Moved : left || right || middle,
            properties: properties);
        var args = new PointerEventArgs(point, modifiers);

        TypedEventHandler<object, PointerEventArgs>? handler = action switch
        {
            PreviewPointerAction.Moved => PointerMoved,
            PreviewPointerAction.Pressed => PointerPressed,
            PreviewPointerAction.Released => PointerReleased,
            PreviewPointerAction.Entered => PointerEntered,
            PreviewPointerAction.Exited => PointerExited,
            PreviewPointerAction.Cancelled => PointerCancelled,
            _ => PointerWheelChanged,
        };

        if (_host.RootElement is { } root)
        {
            _ = root.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                _position = position;
                handler?.Invoke(this, args);
            });
        }
    }

    private static PointerUpdateKind UpdateKind(PreviewPointerAction action, bool left, bool right, bool middle, PointerDeviceType device) => action switch
    {
        PreviewPointerAction.Pressed when right && device != PointerDeviceType.Touch => PointerUpdateKind.RightButtonPressed,
        PreviewPointerAction.Pressed when middle => PointerUpdateKind.MiddleButtonPressed,
        PreviewPointerAction.Pressed => PointerUpdateKind.LeftButtonPressed,
        PreviewPointerAction.Released when right && device != PointerDeviceType.Touch => PointerUpdateKind.RightButtonReleased,
        PreviewPointerAction.Released when middle => PointerUpdateKind.MiddleButtonReleased,
        PreviewPointerAction.Released or PreviewPointerAction.Cancelled => PointerUpdateKind.LeftButtonReleased,
        _ => PointerUpdateKind.Other,
    };
}

/// <summary>
/// The keyboard input of the app preview: keys and text from TabStudio, including the tablet's on-screen keyboard,
/// raised on Uno's UI thread. A character arrives with its key press, as Uno's text boxes expect.
/// </summary>
internal sealed class PreviewKeyboardInput : IUnoKeyboardInputSource
{
    private readonly IXamlRootHost _host;

    public PreviewKeyboardInput(IXamlRootHost host) => _host = host;

    public event TypedEventHandler<object, KeyEventArgs>? KeyDown;
    public event TypedEventHandler<object, KeyEventArgs>? KeyUp;
#pragma warning disable CS0067 // Characters arrive with their key presses.
    public event TypedEventHandler<object, CharacterReceivedEventArgs>? CharacterReceived;
#pragma warning restore CS0067

    /// <summary>Raises a key press or release. Any thread.</summary>
    public void RaiseKey(bool down, VirtualKey key, VirtualKeyModifiers modifiers, uint scanCode, char? character)
    {
        var status = new CorePhysicalKeyStatus { ScanCode = scanCode, RepeatCount = 1, IsKeyReleased = !down };
        var args = new KeyEventArgs("keyboard", key, modifiers, status, down ? character : null);
        Dispatch(() => (down ? KeyDown : KeyUp)?.Invoke(this, args));
    }

    /// <summary>Types text, as the on-screen keyboard or an input method commits it: a key press per character.</summary>
    public void RaiseText(string text, VirtualKeyModifiers modifiers)
    {
        foreach (var character in text)
        {
            RaiseKey(true, VirtualKey.None, modifiers, 0, character);
            RaiseKey(false, VirtualKey.None, modifiers, 0, null);
        }
    }

    private void Dispatch(Action raise)
    {
        if (_host.RootElement is { } root)
        {
            _ = root.Dispatcher.RunAsync(CoreDispatcherPriority.High, () => raise());
        }
    }
}

/// <summary>Input for windows that are not shown: an app's own windows, whose content the preview shows instead.</summary>
internal sealed class DetachedPointerInput : IUnoCorePointerInputSource
{
#pragma warning disable CS0067 // Nothing is ever raised: the window is not on screen.
    public event TypedEventHandler<object, PointerEventArgs>? PointerCaptureLost;
    public event TypedEventHandler<object, PointerEventArgs>? PointerEntered;
    public event TypedEventHandler<object, PointerEventArgs>? PointerExited;
    public event TypedEventHandler<object, PointerEventArgs>? PointerMoved;
    public event TypedEventHandler<object, PointerEventArgs>? PointerPressed;
    public event TypedEventHandler<object, PointerEventArgs>? PointerReleased;
    public event TypedEventHandler<object, PointerEventArgs>? PointerWheelChanged;
    public event TypedEventHandler<object, PointerEventArgs>? PointerCancelled;
#pragma warning restore CS0067

    public bool HasCapture => false;

    public CoreCursor PointerCursor { get; set; } = new(CoreCursorType.Arrow, 0);

    public Point PointerPosition => default;

    public void SetPointerCapture()
    {
    }

    public void ReleasePointerCapture()
    {
    }

    public void SetPointerCapture(PointerIdentifier pointer)
    {
    }

    public void ReleasePointerCapture(PointerIdentifier pointer)
    {
    }
}

/// <summary>Keyboard input for windows that are not shown.</summary>
internal sealed class DetachedKeyboardInput : IUnoKeyboardInputSource
{
#pragma warning disable CS0067 // Nothing is ever raised: the window is not on screen.
    public event TypedEventHandler<object, KeyEventArgs>? KeyDown;
    public event TypedEventHandler<object, KeyEventArgs>? KeyUp;
    public event TypedEventHandler<object, CharacterReceivedEventArgs>? CharacterReceived;
#pragma warning restore CS0067
}
