using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.Recording;

namespace TabStudio.App.ViewModels.Swarm;

/// <summary>A screen the recorder can record on.</summary>
/// <param name="Screen">The screen.</param>
public sealed record UiTestScreenOption(IAppScreen Screen)
{
    /// <summary>Its name and size, such as "Pixel 7 · 412 × 915".</summary>
    public string Name => $"{Screen.Profile.Name} · {SwarmScreens.DescribeSize(Screen.Profile.Width, Screen.Profile.Height)}";
}

/// <summary>A recorded step in the recorder's list.</summary>
/// <param name="Number">Its number, from 1.</param>
/// <param name="Step">The step.</param>
public sealed record UiTestStepRow(int Number, UiTestStep Step)
{
    /// <summary>The step in words.</summary>
    public string Text => Step.Description;

    /// <summary>Whether it checks the screen rather than giving it input.</summary>
    public bool IsCheck => Step.IsCheck;
}

/// <summary>
/// The UI test recorder, in the Swarm window: records what a person does on one screen (a device of the swarm, the app
/// preview or a virtual device) as the steps of a test; a long press adds a check of what the pressed element shows. Save
/// writes the test as C# into the solution's test project, where it runs in the Test Explorer, on every screen and in CI.
/// </summary>
public sealed partial class UiTestRecorderViewModel : ObservableObject, IDisposable
{
    private readonly AppScreenRegistry _registry;
    private readonly IUiDispatcher _ui;
    private readonly Func<UiTestRecording, Task<string?>> _save;
    private readonly List<UiTestStep> _steps = [];
    private IAppScreen? _recorded;

    /// <summary>Creates the recorder.</summary>
    /// <param name="registry">Every screen the app being built runs on.</param>
    /// <param name="ui">Gets inputs onto the UI thread.</param>
    /// <param name="save">Saves a recording as a test, returning why it could not, or <see langword="null"/>.</param>
    public UiTestRecorderViewModel(AppScreenRegistry registry, IUiDispatcher ui, Func<UiTestRecording, Task<string?>> save)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(save);
        _registry = registry;
        _ui = ui;
        _save = save;
        _registry.Screens.CollectionChanged += OnScreensChanged;
        RefreshScreens();
    }

    /// <summary>Whether the recorder is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Whether inputs are being recorded.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand), nameof(SaveCommand))]
    public partial bool IsRecording { get; private set; }

    /// <summary>The screens the app runs on, to record on.</summary>
    public ObservableCollection<UiTestScreenOption> Screens { get; } = [];

    /// <summary>Whether any screen runs the app.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool HasScreens { get; private set; }

    /// <summary>The screen to record on.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial UiTestScreenOption? SelectedScreen { get; set; }

    /// <summary>The test's name, such as "Counting three taps"; its class and method are named after it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string TestName { get; set; } = "Counting three taps";

    /// <summary>The steps so far.</summary>
    public ObservableCollection<UiTestStepRow> Steps { get; } = [];

    /// <summary>Whether any step was recorded.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool HasSteps { get; private set; }

    /// <summary>What the recorder does, or what it just did.</summary>
    [ObservableProperty]
    public partial string? Status { get; private set; }

    /// <summary>Why the test could not be saved, or a check not added.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Whether the recorder is saving the test.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>The steps recorded so far, for tests.</summary>
    internal IReadOnlyList<UiTestStep> RecordedSteps => _steps;

    /// <summary>Shows the recorder, and starts recording on the first screen that runs the app when one does.</summary>
    /// <param name="start">Whether to start recording right away.</param>
    public void Open(bool start)
    {
        RefreshScreens();
        IsOpen = true;
        Error = null;
        Status = HasScreens ? "Choose the screen, then tap Record and use the app there." : "Run the app first: on the swarm, in the app preview or on a virtual device.";
        if (start && StartCommand.CanExecute(null))
        {
            StartCommand.Execute(null);
        }
    }

    /// <summary>Stops recording without keeping the steps.</summary>
    public void Dispose()
    {
        _registry.Screens.CollectionChanged -= OnScreensChanged;
        StopWatching();
    }

    /// <summary>
    /// Starts recording what a person does on the chosen screen. The app starts again there first, when the screen can,
    /// so the recording begins where the test will: on the app's first screen, fresh.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (SelectedScreen?.Screen is not { } screen)
        {
            return;
        }

        StopWatching();
        _steps.Clear();
        Steps.Clear();
        HasSteps = false;
        Error = null;
        _recorded = screen;
        IsRecording = true;
        if (screen is IAppScreenRestart restart)
        {
            Status = $"Starting the app again on {screen.Profile.Name}, so the test begins where it will begin…";
            if (!await restart.RestartAppAsync(CancellationToken.None))
            {
                IsRecording = false;
                Error = $"The app did not start again on {screen.Profile.Name}. Run it there, then record again.";
                return;
            }
        }

        if (!IsRecording || !ReferenceEquals(_recorded, screen))
        {
            return;
        }

        screen.InputCaptured += OnInputCaptured;
        Status = $"Recording on {screen.Profile.Name}. Use the app there; long-press an element to check what it shows.";
    }

    private bool CanStart() => !IsRecording && SelectedScreen is not null;

    /// <summary>Stops recording, keeping the steps to save.</summary>
    [RelayCommand(CanExecute = nameof(IsRecording))]
    private void Stop()
    {
        StopWatching();
        IsRecording = false;
        Status = HasSteps ? "Name the test and save it." : "Nothing was recorded.";
    }

    /// <summary>Writes the test into the solution's test project.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var screen = _recorded ?? SelectedScreen?.Screen;
        if (screen is null)
        {
            return;
        }

        StopWatching();
        IsRecording = false;
        IsSaving = true;
        Error = null;
        try
        {
            var recording = new UiTestRecording(TestName.Trim(), screen.Profile, [.. _steps]);
            Error = await _save(recording);
            if (Error is null)
            {
                Status = $"Saved \"{recording.Name}\". It is in the Test Explorer, ready to run on every screen.";
                _steps.Clear();
                Steps.Clear();
                HasSteps = false;
                IsOpen = false;
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanSave() => HasSteps && !IsSaving && !string.IsNullOrWhiteSpace(TestName);

    /// <summary>Throws the recording away and closes the recorder.</summary>
    [RelayCommand]
    private void Discard()
    {
        StopWatching();
        IsRecording = false;
        _steps.Clear();
        Steps.Clear();
        HasSteps = false;
        Error = null;
        Status = null;
        IsOpen = false;
    }

    /// <summary>Removes a step, such as a tap made by mistake.</summary>
    /// <param name="row">The step's row.</param>
    [RelayCommand]
    private void RemoveStep(UiTestStepRow? row)
    {
        if (row is null || row.Number < 1 || row.Number > _steps.Count)
        {
            return;
        }

        _steps.RemoveAt(row.Number - 1);
        ShowSteps();
    }

    private void OnInputCaptured(object? sender, AppInput input)
    {
        if (!_ui.CheckAccess())
        {
            _ui.Post(() => OnInputCaptured(sender, input));
            return;
        }

        if (!IsRecording || !ReferenceEquals(sender, _recorded))
        {
            return;
        }

        if (input.Kind == AppInputKind.LongPress)
        {
            _ = AddCheckAsync(input.ElementPath);
            return;
        }

        Add(UiTestStep.From(input));
    }

    /// <summary>Adds a step, joining typed text and scrolls of the same element into one step.</summary>
    internal void Add(UiTestStep step)
    {
        if (_steps.Count > 0)
        {
            var last = _steps[^1];
            if (step.Kind == UiTestStepKind.Type && last.Kind == UiTestStepKind.Type)
            {
                _steps[^1] = last with { Text = last.Text + step.Text };
                ShowSteps();
                return;
            }

            if (step.Kind == UiTestStepKind.Scroll && last.Kind == UiTestStepKind.Scroll && last.ElementPath == step.ElementPath)
            {
                _steps[^1] = last with { ScrollX = last.ScrollX + step.ScrollX, ScrollY = last.ScrollY + step.ScrollY };
                ShowSteps();
                return;
            }
        }

        _steps.Add(step);
        ShowSteps();
    }

    /// <summary>A long press: reads what the element shows and adds a check that it shows it.</summary>
    private async Task AddCheckAsync(string elementPath)
    {
        if (_recorded is not IAppScreenElements elements)
        {
            Error = $"{_recorded?.Profile.Name} cannot read what its elements show, so a long press cannot add a check there.";
            return;
        }

        var text = await elements.ReadTextAsync(elementPath, CancellationToken.None);
        if (text is null)
        {
            Error = $"There is nothing to check at {ElementPaths.ShortName(elementPath)}.";
            return;
        }

        Error = null;
        Add(UiTestStep.Check(elementPath, text));
    }

    private void ShowSteps()
    {
        Steps.Clear();
        for (var i = 0; i < _steps.Count; i++)
        {
            Steps.Add(new UiTestStepRow(i + 1, _steps[i]));
        }

        HasSteps = _steps.Count > 0;
    }

    private void StopWatching()
    {
        if (_recorded is not null)
        {
            _recorded.InputCaptured -= OnInputCaptured;
        }
    }

    private void OnScreensChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshScreens();

    private void RefreshScreens()
    {
        var selected = SelectedScreen?.Screen;
        Screens.Clear();
        foreach (var screen in _registry.Screens)
        {
            Screens.Add(new UiTestScreenOption(screen));
        }

        HasScreens = Screens.Count > 0;
        SelectedScreen = Screens.FirstOrDefault(s => ReferenceEquals(s.Screen, selected))
            ?? Screens.FirstOrDefault(s => s.Screen.IsRunning && s.Screen.Kind == AppScreenKind.Device)
            ?? Screens.FirstOrDefault(s => s.Screen.IsRunning)
            ?? Screens.FirstOrDefault();
    }
}
