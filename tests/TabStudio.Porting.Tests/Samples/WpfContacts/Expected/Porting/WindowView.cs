using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace ContactBook.Porting;

/// <summary>
/// A window of the app, ported to Avalonia. On the desktop it opens in a window of its own. Phones and tablets have
/// no windows: there the main window fills the screen, and the others open over it like dialogs.
/// </summary>
/// <remarks>
/// <see cref="Layoutable.Width"/>, <see cref="Layoutable.Height"/> and their minimums and maximums are the window's
/// size, as they were in the original app; the content itself always fills the window or the screen.
/// </remarks>
public abstract class WindowView : UserControl
{
    /// <summary>The window's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<WindowView, string?>(nameof(Title));

    /// <summary>Whether the window takes the size of its content.</summary>
    public static readonly StyledProperty<SizeToContent> SizeToContentProperty =
        AvaloniaProperty.Register<WindowView, SizeToContent>(nameof(SizeToContent));

    /// <summary>Whether the user can resize the window.</summary>
    public static readonly StyledProperty<bool> CanResizeProperty =
        AvaloniaProperty.Register<WindowView, bool>(nameof(CanResize), true);

    /// <summary>Where the window opens.</summary>
    public static readonly StyledProperty<WindowStartupLocation> WindowStartupLocationProperty =
        AvaloniaProperty.Register<WindowView, WindowStartupLocation>(nameof(WindowStartupLocation));

    /// <summary>Whether the window has a button in the taskbar.</summary>
    public static readonly StyledProperty<bool> ShowInTaskbarProperty =
        AvaloniaProperty.Register<WindowView, bool>(nameof(ShowInTaskbar), true);

    /// <summary>Whether the window stays above other windows.</summary>
    public static readonly StyledProperty<bool> TopmostProperty =
        AvaloniaProperty.Register<WindowView, bool>(nameof(Topmost));

    /// <summary>The window's icon.</summary>
    public static readonly StyledProperty<WindowIcon?> IconProperty =
        AvaloniaProperty.Register<WindowView, WindowIcon?>(nameof(Icon));

    /// <summary>Whether the window is normal, minimized or maximized.</summary>
    public static readonly StyledProperty<WindowState> WindowStateProperty =
        AvaloniaProperty.Register<WindowView, WindowState>(nameof(WindowState));

    private Window? _window;
    private ScreenHost? _screen;
    private TaskCompletionSource? _modal;
    private bool _isOpen;
    private bool _isClosing;

    /// <summary>Creates the window; cancel buttons close it when it is a dialog.</summary>
    protected WindowView()
    {
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
    }

    /// <summary>Raised when the window is about to close; set <see cref="CancelEventArgs.Cancel"/> to keep it open.</summary>
    public event CancelEventHandler? Closing;

    /// <summary>Raised after the window closed.</summary>
    public event EventHandler? Closed;

    /// <summary>The app's main window, the one the app started with.</summary>
    public static WindowView? MainWindow { get; private set; }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="SizeToContentProperty"/>
    public SizeToContent SizeToContent
    {
        get => GetValue(SizeToContentProperty);
        set => SetValue(SizeToContentProperty, value);
    }

    /// <inheritdoc cref="CanResizeProperty"/>
    public bool CanResize
    {
        get => GetValue(CanResizeProperty);
        set => SetValue(CanResizeProperty, value);
    }

    /// <inheritdoc cref="WindowStartupLocationProperty"/>
    public WindowStartupLocation WindowStartupLocation
    {
        get => GetValue(WindowStartupLocationProperty);
        set => SetValue(WindowStartupLocationProperty, value);
    }

    /// <inheritdoc cref="ShowInTaskbarProperty"/>
    public bool ShowInTaskbar
    {
        get => GetValue(ShowInTaskbarProperty);
        set => SetValue(ShowInTaskbarProperty, value);
    }

    /// <inheritdoc cref="TopmostProperty"/>
    public bool Topmost
    {
        get => GetValue(TopmostProperty);
        set => SetValue(TopmostProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public WindowIcon? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="WindowStateProperty"/>
    public WindowState WindowState
    {
        get => GetValue(WindowStateProperty);
        set => SetValue(WindowStateProperty, value);
    }

    /// <summary>The window this one belongs to, such as the window that opens a dialog.</summary>
    public WindowView? Owner { get; set; }

    /// <summary>Whether the window is open.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Whether the window is open as a dialog, which its opener waits for.</summary>
    public bool IsDialog => _modal is not null;

    /// <summary>The desktop window it is shown in, or <see langword="null"/> on phones and tablets.</summary>
    public Window? DesktopWindow => _window;

    /// <summary>Whether the app runs with windows, as on the desktop.</summary>
    protected static bool HasWindows => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    /// <summary>
    /// Shows the app's first window: as the main window on the desktop, or as the screen on phones and tablets.
    /// Call it from <c>OnFrameworkInitializationCompleted</c>.
    /// </summary>
    /// <param name="lifetime">The app's lifetime.</param>
    /// <param name="window">The window.</param>
    public static void ShowMain(IApplicationLifetime? lifetime, WindowView window)
    {
        ArgumentNullException.ThrowIfNull(window);
        MainWindow = window;
        switch (lifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = window.CreateWindow();
                window._isOpen = true;
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new ScreenHost(window);
                window._isOpen = true;
                break;
        }
    }

    /// <summary>Ends the app, as closing its last window does on the desktop.</summary>
    public static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>The window an element is in.</summary>
    /// <param name="element">The element.</param>
    public static WindowView? Of(StyledElement? element) =>
        element is null ? null : element as WindowView ?? element.FindLogicalAncestorOfType<WindowView>();

    /// <summary>Opens the window without waiting for it to close.</summary>
    public void Show()
    {
        if (_isOpen)
        {
            Activate();
            return;
        }

        if (MainWindow is null)
        {
            // The app's first window, opened from code: it becomes the main window, or the screen on phones.
            ShowMain(Application.Current?.ApplicationLifetime, this);
            _window?.Show();
            return;
        }

        if (HasWindows)
        {
            var window = CreateWindow();
            if ((Owner ?? MainWindow)?._window is { } owner && !ReferenceEquals(owner, window))
            {
                window.Show(owner);
            }
            else
            {
                window.Show();
            }
        }
        else
        {
            ScreenOf(Owner)?.Open(this, modal: false);
        }

        _isOpen = true;
    }

    /// <summary>Brings the window to the front.</summary>
    public void Activate() => _window?.Activate();

    /// <summary>Closes the window, unless a <see cref="Closing"/> handler keeps it open.</summary>
    public void Close()
    {
        if (!_isOpen || _isClosing)
        {
            return;
        }

        var closing = new CancelEventArgs();
        OnClosing(closing);
        if (closing.Cancel)
        {
            return;
        }

        _isClosing = true;
        if (_window is { } window)
        {
            window.Close();
        }
        else
        {
            _screen?.Remove(this);
            Finish();
        }
    }

    /// <summary>Opens the window as a dialog and waits until it closes.</summary>
    protected Task ShowModalAsync()
    {
        if (_isOpen)
        {
            throw new InvalidOperationException("The window is already open.");
        }

        var modal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _modal = modal;
        _isOpen = true;
        if (HasWindows)
        {
            var window = CreateWindow();
            var owner = (Owner ?? MainWindow)?._window;
            if (owner is not null && owner.IsVisible)
            {
                _ = window.ShowDialog(owner);
            }
            else
            {
                window.Show();
            }
        }
        else if (ScreenOf(Owner) is { } screen)
        {
            screen.Open(this, modal: true);
        }
        else
        {
            Finish();
        }

        return modal.Task;
    }

    /// <summary>Raises <see cref="Closing"/>.</summary>
    /// <param name="e">Set <see cref="CancelEventArgs.Cancel"/> to keep the window open.</param>
    protected virtual void OnClosing(CancelEventArgs e) => Closing?.Invoke(this, e);

    /// <summary>Raises <see cref="Closed"/>.</summary>
    /// <param name="e">Empty.</param>
    protected virtual void OnClosed(EventArgs e) => Closed?.Invoke(this, e);

    /// <summary>Called when a cancel button (<see cref="Button.IsCancel"/>) of the window is clicked.</summary>
    protected virtual void OnCancelButtonClick()
    {
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { IsCancel: true } button && Of(button) == this)
        {
            OnCancelButtonClick();
        }
    }

    /// <summary>The screen windows open on, on phones and tablets.</summary>
    private static ScreenHost? ScreenOf(WindowView? owner) => owner?._screen ?? MainWindow?._screen ?? ScreenHost.Current;

    /// <summary>Puts the view in a desktop window that takes its size and settings.</summary>
    private Window CreateWindow()
    {
        var window = new Window
        {
            SizeToContent = SizeToContent,
            CanResize = CanResize,
            WindowStartupLocation = WindowStartupLocation,
            ShowInTaskbar = ShowInTaskbar,
            Topmost = Topmost,
            WindowState = WindowState,
            MinWidth = MinWidth,
            MinHeight = MinHeight,
            MaxWidth = MaxWidth,
            MaxHeight = MaxHeight,
        };

        if (!double.IsNaN(Width))
        {
            window.Width = Width;
        }

        if (!double.IsNaN(Height))
        {
            window.Height = Height;
        }

        if (Icon is { } icon)
        {
            window.Icon = icon;
        }

        // The view fills the window: its size was the window's.
        ClearValue(WidthProperty);
        ClearValue(HeightProperty);
        ClearValue(MinWidthProperty);
        ClearValue(MinHeightProperty);
        ClearValue(MaxWidthProperty);
        ClearValue(MaxHeightProperty);

        window.Bind(Window.TitleProperty, this.GetObservable(TitleProperty));
        window.Content = this;
        window.Closing += (_, e) =>
        {
            if (_isClosing)
            {
                return;
            }

            // Closed by the user, with the window's close button.
            var closing = new CancelEventArgs();
            OnClosing(closing);
            e.Cancel = closing.Cancel;
            _isClosing = !closing.Cancel;
        };
        window.Closed += (_, _) => Finish();
        _window = window;
        return window;
    }

    /// <summary>Opens in a screen, on phones and tablets.</summary>
    internal void AttachTo(ScreenHost screen) => _screen = screen;

    private void Finish()
    {
        if (!_isOpen)
        {
            return;
        }

        _isOpen = false;
        _isClosing = false;
        if (_window is { } window)
        {
            window.Content = null;
            _window = null;
        }

        _screen = null;
        var modal = _modal;
        _modal = null;
        OnClosed(EventArgs.Empty);
        if (modal is not null)
        {
            Dispatcher.UIThread.Post(() => modal.TrySetResult());
        }
    }
}
