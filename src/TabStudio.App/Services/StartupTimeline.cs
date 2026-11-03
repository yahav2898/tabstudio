using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TabStudio.App.Services;

/// <summary>
/// When each part of TabStudio's start finished, from the moment the platform first ran TabStudio's code (on Android,
/// the activity's creation; Android's own part before it is added from the process's start time): the styles, the
/// services, the root view model, the root view, the first frame drawn and the start screen or the restored workspace
/// ready. A few timestamps, kept in every run; the self test and the tests report them.
/// </summary>
public static class StartupTimeline
{
    private static readonly Lock Gate = new();
    private static readonly List<(string Name, TimeSpan At)> Marks = [];
    private static long _origin;
    private static bool _reported;
    private static bool _firstFrame;
    private static bool _started;

    /// <summary>Raised once, with <see cref="Summary"/>, when the first frame is drawn and the start has finished.</summary>
    public static event Action<string>? Reported;

    /// <summary>How long the platform ran before TabStudio's code, such as Android starting the process and the runtime.</summary>
    public static TimeSpan? BeforeOrigin { get; set; }

    /// <summary>The marks so far: what finished, and when, from the origin.</summary>
    public static IReadOnlyList<(string Name, TimeSpan At)> All
    {
        get
        {
            lock (Gate)
            {
                return [.. Marks];
            }
        }
    }

    /// <summary>Records that a part of the start finished now. The first mark is the origin.</summary>
    /// <param name="name">What finished, such as "services".</param>
    public static void Mark(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var now = Stopwatch.GetTimestamp();
        lock (Gate)
        {
            if (_origin == 0)
            {
                _origin = now;
            }

            Marks.Add((name, Stopwatch.GetElapsedTime(_origin, now)));
        }
    }

    /// <summary>The time of a mark, if it was made.</summary>
    /// <param name="name">The mark.</param>
    public static TimeSpan? At(string name)
    {
        lock (Gate)
        {
            foreach (var mark in Marks)
            {
                if (mark.Name == name)
                {
                    return mark.At;
                }
            }
        }

        return null;
    }

    /// <summary>Marks the first frame a view's window draws, once the view is in it.</summary>
    /// <param name="view">The root view.</param>
    public static void MarkFirstFrame(Control view)
    {
        ArgumentNullException.ThrowIfNull(view);
        void Request()
        {
            if (TopLevel.GetTopLevel(view) is not { } topLevel)
            {
                return;
            }

            // The frame's callback runs before its layout and render pass; a job queued at render priority runs after.
            topLevel.RequestAnimationFrame(_ => Dispatcher.UIThread.Post(
                () =>
                {
                    _firstFrame = true;
                    Mark("first frame");
                    ReportWhenDone();
                },
                DispatcherPriority.Render));
        }

        if (TopLevel.GetTopLevel(view) is not null)
        {
            Request();
            return;
        }

        void OnAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        {
            view.AttachedToVisualTree -= OnAttached;
            Request();
        }

        view.AttachedToVisualTree += OnAttached;
    }

    /// <summary>Marks the end of the start: the start screen shown, or the last workspace restored.</summary>
    /// <param name="name">What was ready.</param>
    public static void MarkStarted(string name)
    {
        _started = true;
        Mark(name);
        ReportWhenDone();
    }

    /// <summary>The marks as one line: each part's time since the one before, and the total.</summary>
    public static string Summary()
    {
        var marks = All;
        var culture = CultureInfo.InvariantCulture;
        var parts = new List<string>();
        if (BeforeOrigin is { } before)
        {
            parts.Add(string.Create(culture, $"process start to {(marks.Count > 0 ? marks[0].Name : "origin")} {before.TotalMilliseconds:0} ms"));
        }

        for (var i = 1; i < marks.Count; i++)
        {
            parts.Add(string.Create(culture, $"{marks[i].Name} +{(marks[i].At - marks[i - 1].At).TotalMilliseconds:0} ms"));
        }

        var total = (BeforeOrigin ?? TimeSpan.Zero) + (marks.Count > 0 ? marks[^1].At : TimeSpan.Zero);
        var firstFrame = At("first frame") is { } frame ? string.Create(culture, $"first frame at {((BeforeOrigin ?? TimeSpan.Zero) + frame).TotalMilliseconds:0} ms, ") : string.Empty;
        return string.Create(culture, $"TabStudio startup: {firstFrame}ready at {total.TotalMilliseconds:0} ms; {string.Join(", ", parts)}");
    }

    private static void ReportWhenDone()
    {
        if (!_firstFrame || !_started || _reported)
        {
            return;
        }

        _reported = true;
        Reported?.Invoke(Summary());
    }
}
