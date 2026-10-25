using System.Collections.Specialized;
using TabStudio.App.Services;

namespace TabStudio.App.Swarm;

/// <summary>An input mirrored to a screen that could not take it, such as a tap on an element that screen lacks.</summary>
/// <param name="Screen">The screen it did not reach.</param>
/// <param name="Source">The screen it was made on.</param>
/// <param name="Input">The input.</param>
public sealed record SwarmMirrorFailure(IAppScreen Screen, IAppScreen Source, AppInput Input)
{
    /// <summary>What happened, in words, as the screens and the Swarm window show it.</summary>
    public string Message => $"{SwarmScreens.Describe(Input)} from {Source.Profile.Name} could not be delivered to {Screen.Profile.Name}.";
}

/// <summary>
/// Mirrored input: while it is on, every input a person makes on any screen of the <see cref="AppScreenRegistry"/> (the
/// devices of the swarm, the app preview and the virtual devices) is replayed on all the others, so the same screen
/// can be compared at every size. A screen that cannot take an input says so for a moment.
/// </summary>
/// <remarks>
/// A screen reports only what a person does on it (<see cref="IAppScreen.InputCaptured"/>): an input replayed on it
/// reaches its app as a person's would, but is not reported again, so mirrored input never echoes between screens.
/// UI thread only.
/// </remarks>
public sealed class SwarmInputMirror : IDisposable
{
    private readonly AppScreenRegistry _registry;
    private readonly IUiDispatcher _ui;
    private readonly HashSet<IAppScreen> _watched = [];

    /// <summary>Starts watching the registry; mirroring starts once <see cref="IsEnabled"/> is set.</summary>
    /// <param name="registry">Every screen of the app being built.</param>
    /// <param name="ui">Gets results back onto the UI thread.</param>
    public SwarmInputMirror(AppScreenRegistry registry, IUiDispatcher ui)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(ui);
        _registry = registry;
        _ui = ui;
        _registry.Screens.CollectionChanged += OnScreensChanged;
        Watch();
    }

    /// <summary>Raised when a mirrored input could not be delivered to a screen.</summary>
    public event EventHandler<SwarmMirrorFailure>? DeliveryFailed;

    /// <summary>Raised for every input mirrored from one screen to the others.</summary>
    public event EventHandler<AppInput>? Mirrored;

    /// <summary>Whether inputs are mirrored.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Stops watching the screens.</summary>
    public void Dispose()
    {
        _registry.Screens.CollectionChanged -= OnScreensChanged;
        foreach (var screen in _watched)
        {
            screen.InputCaptured -= OnInputCaptured;
        }

        _watched.Clear();
    }

    private void OnScreensChanged(object? sender, NotifyCollectionChangedEventArgs e) => Watch();

    private void Watch()
    {
        foreach (var gone in _watched.Where(s => !_registry.Screens.Contains(s)).ToList())
        {
            gone.InputCaptured -= OnInputCaptured;
            _watched.Remove(gone);
        }

        foreach (var screen in _registry.Screens)
        {
            if (_watched.Add(screen))
            {
                screen.InputCaptured += OnInputCaptured;
            }
        }
    }

    private void OnInputCaptured(object? sender, AppInput input)
    {
        if (sender is not IAppScreen source)
        {
            return;
        }

        // Screens may report from other threads.
        if (!_ui.CheckAccess())
        {
            _ui.Post(() => OnInputCaptured(source, input));
            return;
        }

        if (!IsEnabled)
        {
            return;
        }

        var targets = _registry.Screens.Where(s => !ReferenceEquals(s, source) && s.IsRunning).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        Mirrored?.Invoke(this, input);
        foreach (var target in targets)
        {
            _ = DeliverAsync(target, source, input);
        }
    }

    private async Task DeliverAsync(IAppScreen target, IAppScreen source, AppInput input)
    {
        bool delivered;
        try
        {
            delivered = await target.SendInputAsync(input, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            delivered = false;
        }

        if (delivered)
        {
            return;
        }

        var failure = new SwarmMirrorFailure(target, source, input);
        _ui.Post(() =>
        {
            (target as IAppScreenNotices)?.ShowNotice($"{SwarmScreens.Describe(input)} from {source.Profile.Name} did not arrive here: there is no such element on this screen.");
            DeliveryFailed?.Invoke(this, failure);
        });
    }
}
