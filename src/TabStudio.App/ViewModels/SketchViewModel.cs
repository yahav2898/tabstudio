using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Agent;
using TabStudio.Agent.Anthropic;
using TabStudio.App.Services.Agent;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Sketch to XAML in the XAML designer: draw a screen with a finger or a stylus, and Claude writes it as the view's
/// XAML, which the designer then previews.
/// </summary>
public sealed partial class SketchViewModel : ObservableObject, IDisposable
{
    private readonly SketchConverter _converter;
    private readonly Action _openAgentSettings;
    private CancellationTokenSource? _converting;

    internal SketchViewModel(SketchConverter converter, Action openAgentSettings)
    {
        _converter = converter;
        _openAgentSettings = openAgentSettings;
    }

    /// <summary>Raised with Claude's XAML once a sketch is turned into it.</summary>
    public event EventHandler<SketchResult>? Converted;

    /// <summary>Whether the sketch pad is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>What the screen is, in the user's words, such as "a login form".</summary>
    [ObservableProperty]
    public partial string? Hint { get; set; }

    /// <summary>Whether anything is drawn; the view sets it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MakeXamlCommand))]
    public partial bool HasStrokes { get; set; }

    /// <summary>Whether Claude is writing the XAML.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MakeXamlCommand))]
    public partial bool IsConverting { get; private set; }

    /// <summary>Why the last sketch could not be turned into XAML.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Whether the API key is missing, which the pad offers to add.</summary>
    [ObservableProperty]
    public partial bool NeedsApiKey { get; private set; }

    /// <summary>Whether there is an error to show.</summary>
    public bool HasError => Error is not null;

    /// <summary>Renders the drawing as a PNG image; the view sets it.</summary>
    public Func<byte[]>? Capture { get; set; }

    [RelayCommand]
    private void Open()
    {
        NeedsApiKey = !_converter.IsAvailable;
        Error = NeedsApiKey ? "Add your Claude API key in the Claude panel's settings to turn sketches into XAML." : null;
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        _converting?.Cancel();
        IsOpen = false;
    }

    [RelayCommand]
    private void OpenAgentSettings()
    {
        IsOpen = false;
        _openAgentSettings();
    }

    /// <summary>Sends the drawing to Claude and hands the XAML to the designer.</summary>
    [RelayCommand(CanExecute = nameof(CanMakeXaml))]
    private async Task MakeXamlAsync()
    {
        if (Capture is not { } capture)
        {
            return;
        }

        IsConverting = true;
        Error = null;
        _converting = new CancellationTokenSource();
        try
        {
            var result = await _converter.ConvertAsync(capture(), Hint, _converting.Token);
            IsOpen = false;
            Converted?.Invoke(this, result);
        }
        catch (AnthropicException ex)
        {
            Error = ex.Message;
            NeedsApiKey = !_converter.IsAvailable;
        }
        catch (HttpRequestException ex)
        {
            Error = $"Claude could not be reached: {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            // Closed while Claude was writing.
        }
        finally
        {
            _converting.Dispose();
            _converting = null;
            IsConverting = false;
        }
    }

    /// <summary>Cancels a sketch Claude is still writing, when the designer closes.</summary>
    public void Dispose() => _converting?.Cancel();

    private bool CanMakeXaml() => HasStrokes && !IsConverting;
}
