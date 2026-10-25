using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// The device's app preview as a screen, standing in for the Preview screen of the Swarm screens feature until it
/// merges: replays inputs on the app's view by element path, reads texts, takes pictures, and lets a test tap as a
/// person, which the screen reports as <see cref="InputCaptured"/>.
/// </summary>
internal sealed class FakePreviewScreen(AppPreviewViewModel preview, string name) : IAppScreen, IAppScreenElements
{
    public event EventHandler? Changed;

    public event EventHandler<AppInput>? InputCaptured;

    public string Id { get; } = "preview:" + name;

    public AppScreenKind Kind => AppScreenKind.Preview;

    public ScreenProfile Profile { get; } = new(name, FormFactor.Phone, 412, 915, 2.6, true, TextDirection.LeftToRight, 1);

    public bool IsRunning => preview.IsRunning;

    /// <summary>The inputs replayed here, as the tablet sent them.</summary>
    public List<AppInput> Replayed { get; } = [];

    /// <summary>A person taps an element: the app takes it, and the screen reports it.</summary>
    public void PersonTaps(string elementPath) => PersonDoes(new AppInput(AppInputKind.Tap, elementPath, new Point(0.5, 0.5)));

    /// <summary>A person long-presses an element, as a recorder's check.</summary>
    public void PersonLongPresses(string elementPath) => PersonDoes(new AppInput(AppInputKind.LongPress, elementPath, new Point(0.5, 0.5)));

    public Task<bool> SendInputAsync(AppInput input, CancellationToken cancellationToken)
    {
        Replayed.Add(input);
        return Dispatcher.UIThread.InvokeAsync(() => Apply(input)).GetTask();
    }

    public Task<byte[]?> CaptureAsync(CancellationToken cancellationToken) => Dispatcher.UIThread.InvokeAsync<byte[]?>(() =>
    {
        if (preview.View is not { Bounds: { Width: >= 1, Height: >= 1 } bounds } view)
        {
            return null;
        }

        using var bitmap = new RenderTargetBitmap(new PixelSize((int)bounds.Width, (int)bounds.Height));
        bitmap.Render(view);
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }).GetTask();

    public Task<IReadOnlyList<LayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LayoutIssue>>([]);

    public Task<string?> ReadTextAsync(string elementPath, CancellationToken cancellationToken) => Dispatcher.UIThread.InvokeAsync<string?>(() =>
        preview.View is { } view && ElementPaths.Find(view, elementPath) is { } element ? ElementPaths.TextOf(element) : null).GetTask();

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void PersonDoes(AppInput input)
    {
        Apply(input);
        InputCaptured?.Invoke(this, input);
    }

    /// <summary>What a tap does to the app's elements, enough for the tests' apps: a button runs its command.</summary>
    private bool Apply(AppInput input)
    {
        if (preview.View is not { } view)
        {
            return false;
        }

        if (input.Kind is AppInputKind.Text or AppInputKind.Key)
        {
            return true;
        }

        if (ElementPaths.Find(view, input.ElementPath) is not { } element)
        {
            return false;
        }

        if (input.Kind == AppInputKind.Tap && element is Button { Command: { } command } button && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }

        return true;
    }
}
