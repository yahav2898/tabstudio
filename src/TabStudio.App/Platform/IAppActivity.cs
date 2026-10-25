using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

namespace TabStudio.App.Platform;

/// <summary>Whether TabStudio is in the foreground, so background work such as polling CI can rest while it is not.</summary>
public interface IAppActivity
{
    /// <summary>Whether the app is in the background: on Android, another app or the home screen covers it.</summary>
    bool IsInBackground { get; }

    /// <summary>Raised on the UI thread when <see cref="IsInBackground"/> changes.</summary>
    event EventHandler? Changed;
}

/// <summary>
/// The app's activity from Avalonia's activatable lifetime, which Android drives from the activity's start and
/// stop. On platforms without one, the app always counts as in the foreground.
/// </summary>
public sealed class AvaloniaAppActivity : IAppActivity, IDisposable
{
    private readonly IActivatableLifetime? _lifetime;

    /// <summary>Starts following the app's lifetime, when the platform has one.</summary>
    public AvaloniaAppActivity()
    {
        _lifetime = Application.Current?.TryGetFeature<IActivatableLifetime>();
        if (_lifetime is not null)
        {
            _lifetime.Activated += OnActivated;
            _lifetime.Deactivated += OnDeactivated;
        }
    }

    /// <inheritdoc />
    public bool IsInBackground { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_lifetime is not null)
        {
            _lifetime.Activated -= OnActivated;
            _lifetime.Deactivated -= OnDeactivated;
        }
    }

    private void OnActivated(object? sender, ActivatedEventArgs e) => Set(false);

    private void OnDeactivated(object? sender, ActivatedEventArgs e)
    {
        if (e.Kind == ActivationKind.Background)
        {
            Set(true);
        }
    }

    private void Set(bool inBackground)
    {
        if (IsInBackground != inBackground)
        {
            IsInBackground = inBackground;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
