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
                var version = typeof(CrashLog).Assembly.GetName().Version?.ToString() ?? "unknown";
                File.WriteAllText(file, $"TabStudio {version}\n{DateTime.UtcNow:O} UTC\n{source}\n{Environment.OSVersion}\n\n{exception}\n");
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
