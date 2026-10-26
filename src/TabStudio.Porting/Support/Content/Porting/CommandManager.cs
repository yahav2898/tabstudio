using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace $rootnamespace$.Porting;

/// <summary>
/// WPF's <c>CommandManager.RequerySuggested</c>, ported: raised after the user taps, clicks, types or moves the
/// focus, so commands that subscribe to it check again whether they can run, as they did in WPF.
/// </summary>
/// <remarks>WPF holds the handlers weakly; here they stay until removed, which suits commands of view models.</remarks>
public static class CommandManager
{
    private static bool _pending;

    static CommandManager()
    {
        InputElement.PointerReleasedEvent.AddClassHandler<InputElement>((_, _) => InvalidateRequerySuggested(), RoutingStrategies.Bubble, handledEventsToo: true);
        InputElement.KeyUpEvent.AddClassHandler<InputElement>((_, _) => InvalidateRequerySuggested(), RoutingStrategies.Bubble, handledEventsToo: true);
        InputElement.GotFocusEvent.AddClassHandler<InputElement>((_, _) => InvalidateRequerySuggested(), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Raised when commands should check again whether they can run.</summary>
    public static event EventHandler? RequerySuggested;

    /// <summary>Asks every command to check again whether it can run, once the current input has been handled.</summary>
    public static void InvalidateRequerySuggested()
    {
        if (_pending)
        {
            return;
        }

        _pending = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _pending = false;
                RequerySuggested?.Invoke(null, EventArgs.Empty);
            },
            DispatcherPriority.Background);
    }
}
