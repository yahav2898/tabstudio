using System.Globalization;

namespace TabStudio.App.Services;

/// <summary>
/// Keeps a report of every error nothing else handled, in the app's state folder, so a failure on a tablet can be
/// reported with what actually went wrong instead of "it closed".
/// </summary>
public sealed class CrashLog
{
    /// <summary>How many reports are kept; older ones are deleted.</summary>
    public const int Capacity = 20;

    private readonly Lock _gate = new();

    /// <summary>Creates the log in the app's state folder.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public CrashLog(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Folder = Path.Combine(paths.State, "Crashes");
        _running = Path.Combine(paths.State, "running");
        _ended = Path.Combine(Folder, "ended");
    }

    private readonly string _running;
    private readonly string _ended;

    private static string Version => typeof(CrashLog).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>
    /// Notes that TabStudio is in front and in use, until <see cref="MarkStopped"/>: if the note is still there at the
    /// next start, the app ended without going to the background first, which is a crash nothing could write down
    /// (a native crash, Android ending an app that stopped responding, or running out of memory).
    /// </summary>
    public void MarkRunning() => TryWrite(_running, Version);

    /// <summary>Notes that TabStudio went to the background or closed, where Android may end it at any time.</summary>
    public void MarkStopped() => TryDelete(_running);

    /// <summary>
    /// Writes the report of an error that ends the app, and notes it, so the next start says that TabStudio closed
    /// and why instead of starting as if nothing happened.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <param name="source">Where it was caught, such as "UI thread (the app ended)".</param>
    /// <returns>The report's path, or <see langword="null"/> when it could not be written (a full disk).</returns>
    public string? WriteFatal(Exception exception, string source)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var report = Write(exception, source);
        var reason = exception.GetType().Name + ": " + exception.Message.ReplaceLineEndings(" ");
        TryWrite(_ended, reason + "\n" + report);
        return report;
    }

    /// <summary>
    /// How the last run ended, if it did not end as Android or the user ended it: the error and its report when one
    /// was written, otherwise only that it ended in front. Taken once; <see langword="null"/> after a normal end or an
    /// update.
    /// </summary>
    public UnexpectedExit? TakeLastExit()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_ended))
                {
                    var lines = File.ReadAllText(_ended).Split('\n', 2);
                    var report = lines.Length > 1 && lines[1].Trim().Length > 0 ? lines[1].Trim() : null;
                    return new UnexpectedExit(lines[0].Trim(), report);
                }

                // A run of another version ended in front because it was updated, not because it crashed.
                return File.Exists(_running) && File.ReadAllText(_running).Trim() == Version ? new UnexpectedExit(null, null) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
            finally
            {
                DeleteCore(_ended);
                DeleteCore(_running);
            }
        }
    }

    private void TryWrite(string path, string text)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: without the note the next start says nothing, as before.
            }
        }
    }

    private void TryDelete(string path)
    {
        lock (_gate)
        {
            DeleteCore(path);
        }
    }

    private static void DeleteCore(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A stale note only makes the next start mention an end that was normal.
        }
    }

    /// <summary>The folder the reports are written to, one file each.</summary>
    public string Folder { get; }

    /// <summary>
    /// Whether the app can go on after this error: anything but the runtime's own failures, which leave it in a state
    /// that cannot be trusted.
    /// </summary>
    /// <param name="exception">The error.</param>
    public static bool IsRecoverable(Exception exception) => exception switch
    {
        null => false,
        OutOfMemoryException or InsufficientExecutionStackException or StackOverflowException or AccessViolationException
            or InvalidProgramException or BadImageFormatException or TypeInitializationException => false,
        AggregateException aggregate => aggregate.InnerExceptions.All(IsRecoverable),
        _ => true,
    };

    /// <summary>Writes a report, best effort.</summary>
    /// <param name="exception">The error.</param>
    /// <param name="source">Where it was caught, such as "UI thread".</param>
    /// <returns>The report's path, or <see langword="null"/> when it could not be written (a full disk).</returns>
    public string? Write(Exception exception, string source)
    {
        ArgumentNullException.ThrowIfNull(exception);
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                var file = Path.Combine(Folder, $"crash-{stamp}.txt");
                File.WriteAllText(file, $"TabStudio {Version}\n{DateTime.UtcNow:O} UTC\n{source}\n{Environment.OSVersion}\n\n{exception}\n");
                foreach (var old in Directory.GetFiles(Folder, "crash-*.txt").Order(StringComparer.Ordinal).SkipLast(Capacity))
                {
                    File.Delete(old);
                }

                return file;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Errors another handler took as its own, such as those of the user's Avalonia app running in the preview: they
    /// end that app, not TabStudio, and are not TabStudio's to report.
    /// </summary>
    public static System.Runtime.CompilerServices.ConditionalWeakTable<Exception, object> Claimed { get; } = new();

    /// <summary>The reports, newest first.</summary>
    public IReadOnlyList<string> GetReports()
    {
        try
        {
            return Directory.Exists(Folder) ? Directory.GetFiles(Folder, "crash-*.txt").OrderDescending(StringComparer.Ordinal).ToList() : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

/// <summary>How TabStudio's last run ended when it ended by itself.</summary>
/// <param name="Reason">The error that ended it, such as <c>OutOfMemoryException: …</c>; <see langword="null"/> when nothing could write it down.</param>
/// <param name="Report">The crash report, when one was written.</param>
public sealed record UnexpectedExit(string? Reason, string? Report)
{
    /// <summary>What the user is told at the next start.</summary>
    public string Message => (Reason is null
            ? "TabStudio closed unexpectedly last time, while it was in use."
            : $"TabStudio closed unexpectedly last time ({Reason}).")
        + " Unsaved changes are restored with the workspace."
        + (Report is null ? string.Empty : $" Details: {Report}");
}
