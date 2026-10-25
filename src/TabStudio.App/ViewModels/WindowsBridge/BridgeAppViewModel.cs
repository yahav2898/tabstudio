using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.ViewModels.WindowsBridge;

/// <summary>A program running on the PC, in the Bridge pane's picker, with its windows and dialogs.</summary>
public sealed partial class BridgeAppViewModel : ObservableObject
{
    /// <summary>Creates the row for a program that started.</summary>
    /// <param name="started">The program.</param>
    public BridgeAppViewModel(AppStarted started)
    {
        ArgumentNullException.ThrowIfNull(started);
        AppId = started.AppId;
        Name = started.Name;
        ProjectPath = started.ProjectPath;
        ProcessId = started.ProcessId;
        IsDebugging = started.IsDebugging;
    }

    /// <summary>The bridge's number for the program.</summary>
    public int AppId { get; }

    /// <summary>Its name, such as <c>HelloWpf</c>.</summary>
    public string Name { get; }

    /// <summary>The project it was built from.</summary>
    public string ProjectPath { get; }

    /// <summary>Its process on the PC.</summary>
    public int ProcessId { get; }

    /// <summary>Whether the debugger started it.</summary>
    public bool IsDebugging { get; }

    /// <summary>The picker's text, such as <c>HelloWpf (debugging)</c>.</summary>
    public string Display => IsDebugging ? $"{Name} (debugging)" : Name;

    /// <summary>Its top-level windows and dialogs, the newest last.</summary>
    public ObservableCollection<BridgeWindowOption> Windows { get; } = [];

    /// <summary>Whether it has a window to show.</summary>
    [ObservableProperty]
    public partial bool HasWindows { get; private set; }

    /// <summary>Takes the program's windows as the bridge lists them.</summary>
    /// <param name="windows">The windows.</param>
    /// <returns>The windows that are new since the last list.</returns>
    public IReadOnlyList<BridgeWindowOption> SetWindows(IReadOnlyList<BridgeWindowInfo> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        var known = Windows.Select(w => w.Id).ToHashSet();
        var options = windows.Select(w => new BridgeWindowOption(w)).ToList();
        if (!options.SequenceEqual(Windows))
        {
            Windows.Clear();
            foreach (var option in options)
            {
                Windows.Add(option);
            }
        }

        HasWindows = Windows.Count > 0;
        return [.. options.Where(o => !known.Contains(o.Id))];
    }

    /// <inheritdoc />
    public override string ToString() => Display;
}

/// <summary>A window or dialog of a program on the PC, in the Bridge pane's window picker.</summary>
/// <param name="Window">The window.</param>
public sealed record BridgeWindowOption(BridgeWindowInfo Window)
{
    /// <summary>The window's handle on the PC.</summary>
    public long Id => Window.Id;

    /// <summary>The picker's text: its title, or what it is when it has none.</summary>
    public string Display => Window.Title.Length > 0 ? Window.Title : Window.IsDialog ? "Untitled dialog" : "Untitled window";

    /// <summary>Its size, such as <c>800 × 600</c>.</summary>
    public string SizeText => $"{Window.Width} × {Window.Height}";

    /// <inheritdoc />
    public override string ToString() => Display;
}
