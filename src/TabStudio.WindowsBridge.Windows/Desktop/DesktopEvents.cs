using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TabStudio.WindowsBridge.Protocol;
using TabStudio.WindowsBridge.Windows.Native;

namespace TabStudio.WindowsBridge.Windows.Desktop;

/// <summary>
/// Hears what Windows tells top-level windows about the PC: sleep and wake, the session locking and unlocking, and
/// shutdown, through a hidden window on a thread of its own. The same thread keeps the PC awake on request, since
/// Windows ties that request to the thread that makes it.
/// </summary>
internal sealed unsafe class DesktopEvents : IDisposable
{
    private const uint KeepAwakeMessage = NativeMethods.WmUser + 1;
    private const uint StopMessage = NativeMethods.WmUser + 2;

    private static readonly ConcurrentDictionary<nint, DesktopEvents> Windows = new();
    private static readonly Lock RegistrationGate = new();
    private static nint _className;

    private readonly Thread _thread;
    private readonly TaskCompletionSource<nint> _window = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile BridgePcState _state = BridgePcState.Awake;

    /// <summary>Starts listening.</summary>
    public DesktopEvents()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "TabStudio Windows Bridge events" };
        _thread.Start();
        _window.Task.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>Raised on the events thread when <see cref="State"/> changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Whether the PC is awake, locked, about to sleep or shutting down.</summary>
    public BridgePcState State => _state;

    /// <summary>Keeps the PC from sleeping by itself, or lets it sleep again.</summary>
    /// <param name="keepAwake">Whether to keep it awake.</param>
    public void KeepAwake(bool keepAwake)
    {
        if (_window.Task.IsCompletedSuccessfully && _window.Task.Result != 0)
        {
            _ = NativeMethods.PostMessage(_window.Task.Result, KeepAwakeMessage, keepAwake ? 1u : 0u, 0);
        }
    }

    /// <summary>Stops listening and lets the PC sleep.</summary>
    public void Dispose()
    {
        if (_window.Task.IsCompletedSuccessfully && _window.Task.Result != 0)
        {
            _ = NativeMethods.PostMessage(_window.Task.Result, StopMessage, 0, 0);
        }

        _thread.Join(TimeSpan.FromSeconds(2));
    }

    [UnmanagedCallersOnly]
    private static nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
    {
        if (Windows.TryGetValue(window, out var events))
        {
            if (events.Handle(message, wParam))
            {
                return message == NativeMethods.WmQueryEndSession ? 1 : 0;
            }
        }

        return NativeMethods.DefWindowProc(window, message, wParam, lParam);
    }

    private static nint ClassName()
    {
        lock (RegistrationGate)
        {
            if (_className != 0)
            {
                return _className;
            }

            var name = Marshal.StringToHGlobalUni("TabStudioWindowsBridgeEvents");
            var windowClass = new NativeMethods.WindowClass
            {
                Size = (uint)sizeof(NativeMethods.WindowClass),
                WindowProcedure = (nint)(delegate* unmanaged<nint, uint, nuint, nint, nint>)&WindowProcedure,
                Instance = NativeMethods.GetModuleHandle(0),
                ClassName = name,
            };
            _ = NativeMethods.RegisterClassEx(windowClass);
            _className = name;
            return name;
        }
    }

    private void Run()
    {
        nint window = 0;
        try
        {
            // A hidden top-level window: message-only windows do not hear power broadcasts.
            window = NativeMethods.CreateWindowEx(0, ClassName(), 0, 0, 0, 0, 0, 0, 0, 0, NativeMethods.GetModuleHandle(0), 0);
            if (window != 0)
            {
                Windows[window] = this;
                _ = NativeMethods.WTSRegisterSessionNotification(window, NativeMethods.NotifyForThisSession);
            }
        }
        finally
        {
            _window.TrySetResult(window);
        }

        if (window == 0)
        {
            return;
        }

        while (NativeMethods.GetMessage(out var message, 0, 0, 0) > 0)
        {
            _ = NativeMethods.TranslateMessage(message);
            _ = NativeMethods.DispatchMessage(message);
        }

        _ = NativeMethods.SetThreadExecutionState(NativeMethods.EsContinuous);
        _ = NativeMethods.WTSUnRegisterSessionNotification(window);
        Windows.TryRemove(window, out _);
        _ = NativeMethods.DestroyWindow(window);
    }

    /// <summary>Handles one of the window's messages; returns whether it was one of the bridge's.</summary>
    private bool Handle(uint message, nuint wParam)
    {
        switch (message)
        {
            case KeepAwakeMessage:
                _ = NativeMethods.SetThreadExecutionState(wParam != 0 ? NativeMethods.EsContinuous | NativeMethods.EsSystemRequired : NativeMethods.EsContinuous);
                return true;
            case StopMessage:
                NativeMethods.PostQuitMessage(0);
                return true;
            case NativeMethods.WmPowerBroadcast when wParam == NativeMethods.PbtApmSuspend:
                SetState(BridgePcState.Sleeping);
                return true;
            case NativeMethods.WmPowerBroadcast when wParam == NativeMethods.PbtApmResumeAutomatic || wParam == NativeMethods.PbtApmResumeSuspend:
                SetState(BridgePcState.Awake);
                return true;
            case NativeMethods.WmWtsSessionChange when wParam == NativeMethods.WtsSessionLock:
                SetState(BridgePcState.Locked);
                return true;
            case NativeMethods.WmWtsSessionChange when wParam == NativeMethods.WtsSessionUnlock:
                SetState(BridgePcState.Awake);
                return true;
            case NativeMethods.WmQueryEndSession:
                SetState(BridgePcState.ShuttingDown);
                return true;
            default:
                return false;
        }
    }

    private void SetState(BridgePcState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
