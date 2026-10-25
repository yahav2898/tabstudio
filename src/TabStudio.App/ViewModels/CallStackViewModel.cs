using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Debugging;
using TabStudio.Debugging.Symbols;

namespace TabStudio.App.ViewModels;

/// <summary>One row of the Call Stack panel: a function of the paused program and the statement it is running.</summary>
public sealed partial class StackFrameViewModel : ObservableObject
{
    /// <summary>Creates a row.</summary>
    /// <param name="index">The position in the call stack; 0 is where the program is paused.</param>
    /// <param name="frame">The frame.</param>
    public StackFrameViewModel(int index, DebugStackFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Index = index;
        MethodName = frame.MethodName;
        Location = frame.Location;
        LocationText = frame.Location is { } location
            ? $"{Path.GetFileName(location.FilePath)}, line {location.Line}"
            : "External code";
    }

    /// <summary>The position in the call stack; 0 is the frame where the program is paused.</summary>
    public int Index { get; }

    /// <summary>The function, such as <c>Greeter.Greet()</c>.</summary>
    public string MethodName { get; }

    /// <summary>The statement the frame is running, or <see langword="null"/> when it is not known.</summary>
    public DebugSourceSpan? Location { get; }

    /// <summary>"Program.cs, line 15".</summary>
    public string LocationText { get; }

    /// <summary>Whether this is the frame where the program is paused, marked with the yellow arrow.</summary>
    public bool IsTop => Index == 0;

    /// <summary>Whether the editor shows this frame: the yellow arrow for the top frame, the green arrow for a caller.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActiveCaller))]
    public partial bool IsActive { get; set; }

    /// <summary>Whether this is a caller the user chose, marked with the green arrow.</summary>
    public bool IsActiveCaller => IsActive && !IsTop;
}

/// <summary>The Call Stack panel: the functions of the paused program, innermost first. Tapping one shows its code.</summary>
public sealed partial class CallStackViewModel : ObservableObject
{
    /// <summary>The frames, innermost first.</summary>
    public ObservableCollection<StackFrameViewModel> Frames { get; } = [];

    /// <summary>The frame the editor shows.</summary>
    [ObservableProperty]
    public partial StackFrameViewModel? ActiveFrame { get; private set; }

    /// <summary>Raised when the user chooses a frame.</summary>
    public event EventHandler<StackFrameViewModel>? FrameActivated;

    /// <summary>Shows the call stack of a pause, with the top frame active.</summary>
    /// <param name="frames">The frames, innermost first.</param>
    public void Show(IReadOnlyList<DebugStackFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        Frames.Clear();
        for (var i = 0; i < frames.Count; i++)
        {
            Frames.Add(new StackFrameViewModel(i, frames[i]));
        }

        SetActive(Frames.Count > 0 ? Frames[0] : null);
    }

    /// <summary>Removes every frame, when the program runs again or the session ends.</summary>
    public void Clear()
    {
        Frames.Clear();
        ActiveFrame = null;
    }

    /// <summary>Shows a frame's code in the editor (a tap, or Enter).</summary>
    /// <param name="frame">The frame.</param>
    [RelayCommand]
    public void Activate(StackFrameViewModel? frame)
    {
        if (frame is null || !Frames.Contains(frame))
        {
            return;
        }

        SetActive(frame);
        FrameActivated?.Invoke(this, frame);
    }

    private void SetActive(StackFrameViewModel? frame)
    {
        foreach (var row in Frames)
        {
            row.IsActive = ReferenceEquals(row, frame);
        }

        ActiveFrame = frame;
    }
}
