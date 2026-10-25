using Avalonia.Controls;
using Avalonia.Threading;

namespace TabStudio.App.Previews;

/// <summary>Another instance of a running app's main view, in its own copy of the app's theme.</summary>
/// <param name="Root">The control to show: the theme, with the view inside.</param>
/// <param name="MainView">The app's main view, with its own view model.</param>
public sealed record AppViewInstance(Control Root, Control MainView);

/// <summary>
/// More instances of the running app, for virtual devices: Avalonia allows one <see cref="Avalonia.Application"/>
/// per process, so each is the app's main view again, created the way the app creates it.
/// </summary>
public sealed partial class AvaloniaAppHost
{
    /// <summary>The app's main view in the app preview, inside its theme.</summary>
    public Control? MainView => AppTheme.ContentOf(View);

    /// <summary>
    /// Creates another instance of the app's main view, as a second device would: the app's <c>App</c> is started
    /// again with a single-view lifetime, so its <c>OnFrameworkInitializationCompleted</c> creates the view and its
    /// view model as it always does, and the view is put in a fresh copy of the app's theme. UI thread only.
    /// </summary>
    /// <returns>The view, which the caller shows and lets go of; disposing the host does not.</returns>
    /// <exception cref="InvalidOperationException">The app has no view to show, or its startup code threw.</exception>
    public AppViewInstance CreateView()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var types = LoadableTypes(_program.EntryAssembly);
        var app = StartApplication(types);
        var view = app.Lifetime.MainView
            ?? FromMainWindow(_program.EntryAssembly, types, out _)
            ?? throw new InvalidOperationException("The app has no main view to show.");
        var theme = new AppTheme(app.Instance) { Content = view };
        return new AppViewInstance(theme.Root, view);
    }

    /// <summary>Lets go of an instance's view model, once its view is no longer shown.</summary>
    /// <param name="instance">The instance.</param>
    public static void Release(AppViewInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        instance.MainView.DataContext = null;
        if (instance.Root is Decorator decorator)
        {
            decorator.Child = null;
        }
    }
}
