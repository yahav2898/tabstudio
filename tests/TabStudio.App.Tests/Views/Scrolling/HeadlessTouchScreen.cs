using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// A finger on a headless window: touches go through Avalonia's own touch device, as they do on Android, so capture,
/// gesture recognizers, taps and holds behave as on a tablet. Timestamps are virtual milliseconds the test chooses.
/// </summary>
/// <remarks>
/// Avalonia's headless window only simulates a mouse and a keyboard, and its raw input types are hidden from
/// applications, so the touch device, the raw touch event and the window's input entry point are reached by
/// reflection: test code only.
/// </remarks>
internal sealed class HeadlessTouchScreen
{
    private static readonly ConstructorInfo TouchEventConstructor = typeof(RawTouchEventArgs).GetConstructor(
        [typeof(IInputDevice), typeof(ulong), typeof(IInputRoot), typeof(RawPointerEventType), typeof(Point), typeof(RawInputModifiers), typeof(long)])
        ?? throw new InvalidOperationException("Avalonia's raw touch event changed.");

    private readonly TopLevel _root;
    private readonly IInputDevice _device;
    private readonly Action<RawInputEventArgs> _input;
    private long _nextTouchId = 1;
    private long _touchId;

    /// <summary>Puts a finger on a shown window.</summary>
    /// <param name="root">The window.</param>
    public HeadlessTouchScreen(TopLevel root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
        _device = Activator.CreateInstance<TouchDevice>();
        var impl = root.PlatformImpl ?? throw new InvalidOperationException("The window is not shown.");
        _input = impl.GetType().GetProperty("Input", BindingFlags.Public | BindingFlags.Instance)?.GetValue(impl) as Action<RawInputEventArgs>
            ?? throw new InvalidOperationException("The window takes no input.");
    }

    /// <summary>The virtual clock, in milliseconds, of the next event.</summary>
    public ulong Now { get; set; } = 1_000;

    /// <summary>Puts the finger down.</summary>
    /// <param name="point">Where, in window coordinates.</param>
    public void Down(Point point)
    {
        _touchId = _nextTouchId++;
        Send(RawPointerEventType.TouchBegin, point);
    }

    /// <summary>Moves the finger.</summary>
    /// <param name="point">Where to, in window coordinates.</param>
    public void Move(Point point) => Send(RawPointerEventType.TouchUpdate, point);

    /// <summary>Lifts the finger.</summary>
    /// <param name="point">Where, in window coordinates.</param>
    public void Up(Point point) => Send(RawPointerEventType.TouchEnd, point);

    /// <summary>Advances the virtual clock.</summary>
    /// <param name="milliseconds">How long.</param>
    public void Wait(double milliseconds) => Now += (ulong)Math.Round(milliseconds);

    private void Send(RawPointerEventType type, Point point)
    {
        var args = (RawInputEventArgs)TouchEventConstructor.Invoke([_device, Now, (IInputRoot)_root, type, point, RawInputModifiers.None, _touchId]);
        _input(args);
    }
}
