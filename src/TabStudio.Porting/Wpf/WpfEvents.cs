using System.Collections.Frozen;

namespace TabStudio.Porting.Wpf;

/// <summary>A WPF event and its Avalonia counterpart.</summary>
/// <param name="Name">The Avalonia event.</param>
/// <param name="ArgsType">The type of the Avalonia handler's event arguments, when it differs from WPF's.</param>
/// <param name="Note">What a person should check, when the events are not exactly the same.</param>
internal sealed record EventMapping(string Name, string? ArgsType, string? Note = null);

/// <summary>WPF's events that Avalonia names differently, after the migration guide's event table.</summary>
internal static class WpfEvents
{
    private const string AnyButton = "Avalonia raises it for every mouse button, the pen and touch";

    /// <summary>Events with an Avalonia counterpart of another name.</summary>
    public static readonly FrozenDictionary<string, EventMapping> Renamed = new Dictionary<string, EventMapping>(StringComparer.Ordinal)
    {
        ["MouseLeftButtonDown"] = new("PointerPressed", "PointerPressedEventArgs", AnyButton + "; check e.GetCurrentPoint(this).Properties.IsLeftButtonPressed if only the left button counts"),
        ["MouseLeftButtonUp"] = new("PointerReleased", "PointerReleasedEventArgs", AnyButton + "; check e.InitialPressMouseButton == MouseButton.Left if only the left button counts"),
        ["MouseRightButtonDown"] = new("PointerPressed", "PointerPressedEventArgs", AnyButton + "; check e.GetCurrentPoint(this).Properties.IsRightButtonPressed, or use a ContextMenu or ContextRequested"),
        ["MouseRightButtonUp"] = new("PointerReleased", "PointerReleasedEventArgs", AnyButton + "; check e.InitialPressMouseButton == MouseButton.Right, or use a ContextMenu or ContextRequested"),
        ["MouseDown"] = new("PointerPressed", "PointerPressedEventArgs"),
        ["MouseUp"] = new("PointerReleased", "PointerReleasedEventArgs"),
        ["MouseMove"] = new("PointerMoved", "PointerEventArgs"),
        ["MouseEnter"] = new("PointerEntered", "PointerEventArgs"),
        ["MouseLeave"] = new("PointerExited", "PointerEventArgs"),
        ["MouseWheel"] = new("PointerWheelChanged", "PointerWheelEventArgs", "e.Delta is a Vector now: e.Delta.Y is 1 per notch, not 120"),
        ["MouseDoubleClick"] = new("DoubleTapped", "TappedEventArgs"),
        ["PasswordChanged"] = new("TextChanged", "TextChangedEventArgs"),
        ["TextInput"] = new("TextInput", "TextInputEventArgs"),
        ["ContextMenuOpening"] = new("ContextRequested", "ContextRequestedEventArgs"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Events Avalonia does not have under any name, with what to do instead.</summary>
    public static readonly FrozenDictionary<string, string> Unsupported = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["StateChanged"] = "Avalonia windows report their state through the WindowState property; observe it with GetObservable",
        ["LocationChanged"] = "Avalonia windows raise PositionChanged on the desktop only",
        ["SourceInitialized"] = "Avalonia windows have no Win32 source; use Opened or Loaded",
        ["RequestNavigate"] = "Avalonia has no hyperlinks inside text; a HyperlinkButton opens its NavigateUri itself",
        ["DataContextChanged"] = "Avalonia raises DataContextChanged with plain EventArgs; subscribe to it in code",
        ["IsVisibleChanged"] = "Observe IsVisible with GetObservable(IsVisibleProperty) in code",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Whether an event is a WPF tunneling event, such as <c>PreviewMouseDown</c>.</summary>
    /// <param name="name">The event.</param>
    public static bool IsPreview(string name) => name.StartsWith("Preview", StringComparison.Ordinal) && name.Length > 7 && char.IsUpper(name[7]);

    /// <summary>The Avalonia event a WPF tunneling event becomes when it is handled with the Tunnel strategy.</summary>
    /// <param name="name">The WPF event, such as <c>PreviewKeyDown</c>.</param>
    public static string TunnelTarget(string name)
    {
        var bubbling = name["Preview".Length..];
        return Renamed.TryGetValue(bubbling, out var renamed) ? renamed.Name : bubbling;
    }
}
