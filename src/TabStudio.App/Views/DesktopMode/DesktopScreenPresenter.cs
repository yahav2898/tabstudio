using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using TabStudio.App.Input;
using TabStudio.App.ViewModels.DesktopMode;

namespace TabStudio.App.Views.DesktopMode;

/// <summary>
/// Moves the IDE between the tablet and the external display as desktop mode turns on and off: the same views, so
/// the open documents keep their caret, scroll position, selection and undo history, and every panel stays as it was.
/// It also keeps the keyboard with the IDE on the display while the tablet is the companion.
/// </summary>
internal sealed class DesktopScreenPresenter : IDisposable
{
    private readonly Panel _tablet;
    private readonly Control _ide;
    private readonly DesktopModeViewModel _desktop;
    private readonly DesktopScreenView _screen = new();
    private TopLevel? _tabletTopLevel;
    private double _presentedScale;
    private bool _presented;
    private bool _disposed;

    /// <summary>Starts following desktop mode.</summary>
    /// <param name="tablet">The tablet's root panel, which holds the IDE while desktop mode is off.</param>
    /// <param name="ide">The IDE.</param>
    /// <param name="desktop">Desktop mode.</param>
    public DesktopScreenPresenter(Panel tablet, Control ide, DesktopModeViewModel desktop)
    {
        _tablet = tablet;
        _ide = ide;
        _desktop = desktop;
        _desktop.PropertyChanged += OnDesktopPropertyChanged;
        _desktop.KeyboardFocusRequested += OnKeyboardFocusRequested;
        _desktop.Displays.PresentationEnded += OnPresentationEnded;
        _tablet.AttachedToVisualTree += OnTabletAttached;
        Apply();
    }

    /// <summary>The display's view while it shows the IDE, otherwise <see langword="null"/>.</summary>
    public DesktopScreenView? Screen => _presented ? _screen : null;

    /// <summary>Brings the IDE back to the tablet and stops following desktop mode.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_presented)
        {
            Return();
        }

        _disposed = true;
        _desktop.PropertyChanged -= OnDesktopPropertyChanged;
        _desktop.KeyboardFocusRequested -= OnKeyboardFocusRequested;
        _desktop.Displays.PresentationEnded -= OnPresentationEnded;
        _tablet.AttachedToVisualTree -= OnTabletAttached;
    }

    /// <summary>Moves the IDE to where desktop mode says it belongs.</summary>
    private void Apply()
    {
        if (_disposed)
        {
            return;
        }

        if (_desktop.IsActive && !_presented && _tablet.IsAttachedToVisualTree())
        {
            Present();
        }
        else if (!_desktop.IsActive && _presented)
        {
            Return();
        }
    }

    private void Present()
    {
        _tabletTopLevel = TopLevel.GetTopLevel(_tablet);

        // Taken out of the tablet and put on the display in one go, with no layout in between, so nothing is rebuilt.
        _tablet.Children.Remove(_ide);
        _screen.Show(_ide);
        var scale = _desktop.EffectiveScale;
        if (!_desktop.Displays.Present(_screen, scale))
        {
            _screen.Show(null);
            _tablet.Children.Insert(0, _ide);
            _desktop.OnPresentationFailed();
            return;
        }

        _presented = true;
        _presentedScale = scale;
        _tabletTopLevel?.AddHandler(InputElement.KeyDownEvent, OnTabletKeyDown, RoutingStrategies.Bubble);
        Dispatcher.UIThread.Post(_screen.RestoreKeyboardFocus, DispatcherPriority.Background);
    }

    private void Return()
    {
        _presented = false;
        _tabletTopLevel?.RemoveHandler(InputElement.KeyDownEvent, OnTabletKeyDown);
        _desktop.Displays.Dismiss();
        _screen.Show(null);
        _tablet.Children.Insert(0, _ide);
        Dispatcher.UIThread.Post(FocusEditorOnTablet, DispatcherPriority.Background);
    }

    /// <summary>Shows the IDE on the display again, in a new presentation: at a new size, or after the system ended the old one.</summary>
    private void PresentAgain()
    {
        _desktop.Displays.Dismiss();
        var scale = _desktop.EffectiveScale;
        if (_desktop.Displays.Present(_screen, scale))
        {
            _presentedScale = scale;
            Dispatcher.UIThread.Post(_screen.RestoreKeyboardFocus, DispatcherPriority.Background);
        }
        else
        {
            // Desktop mode ends, which brings the IDE back.
            _desktop.OnPresentationFailed();
        }
    }

    private void OnDesktopPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopModeViewModel.IsActive))
        {
            Apply();
        }
        else if (e.PropertyName == nameof(DesktopModeViewModel.EffectiveScale) && _presented && Math.Abs(_desktop.EffectiveScale - _presentedScale) > 0.001)
        {
            PresentAgain();
        }
    }

    /// <summary>
    /// The system ended the presentation while the display is still there, as Android does when the display's size
    /// changes: show the IDE again at the new size.
    /// </summary>
    private void OnPresentationEnded(object? sender, EventArgs e)
    {
        if (_presented && _desktop.IsActive && _desktop.HasDisplay && !_desktop.Displays.IsPresenting)
        {
            PresentAgain();
        }
    }

    private void OnTabletAttached(object? sender, VisualTreeAttachmentEventArgs e) => Apply();

    private void OnKeyboardFocusRequested(object? sender, EventArgs e)
    {
        if (_presented)
        {
            _screen.RestoreKeyboardFocus();
        }
    }

    /// <summary>
    /// A key typed on the tablet's keyboard while nothing has the focus: the IDE on the display gets the focus, and
    /// the key's shortcut, if it has one, runs there.
    /// </summary>
    private void OnTabletKeyDown(object? sender, KeyEventArgs e)
    {
        if (_presented && !e.Handled && ReferenceEquals(e.Source, _tabletTopLevel))
        {
            _screen.RestoreKeyboardFocus();
            e.Handled = _screen.RunKeyBinding(e);
        }
    }

    /// <summary>Back on the tablet, the active editor gets the keyboard, unless the user works by touch.</summary>
    private void FocusEditorOnTablet()
    {
        foreach (var editor in _ide.GetVisualDescendants().OfType<TextArea>())
        {
            if (editor.IsEffectivelyVisible)
            {
                InputModality.FocusUnlessTouch(editor);
                return;
            }
        }
    }
}
