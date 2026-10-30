using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Microsoft.VisualBasic.Logging;

/// <summary>
/// The program's log (<c>My.Application.Log</c>): a trace source named <c>DefaultSource</c> that writes Information and
/// more severe entries to a <see cref="FileLogTraceListener"/> named <c>FileLog</c>, in the app's data folder on the
/// device. TabStudio has no app.config to configure it, so the defaults always apply; code can change them.
/// </summary>
public class Log
{
    private const string DefaultFileLogTraceListenerName = "FileLog";
    private const string WinAppSourceName = "DefaultSource";

    private static readonly Dictionary<TraceEventType, int> IdHash = new()
    {
        [TraceEventType.Information] = 0,
        [TraceEventType.Warning] = 1,
        [TraceEventType.Error] = 2,
        [TraceEventType.Critical] = 3,
        [TraceEventType.Start] = 4,
        [TraceEventType.Stop] = 5,
        [TraceEventType.Suspend] = 6,
        [TraceEventType.Resume] = 7,
        [TraceEventType.Verbose] = 8,
        [TraceEventType.Transfer] = 9,
    };

    private readonly TraceSource _traceSource;

    /// <summary>Creates the log of the trace source <c>DefaultSource</c>.</summary>
    public Log()
        : this(WinAppSourceName)
    {
    }

    /// <summary>Creates the log of a trace source.</summary>
    /// <param name="name">The trace source's name.</param>
    public Log(string name)
    {
        _traceSource = new TraceSource(name);
        InitializeWithDefaultsSinceNoConfigExists();
    }

    /// <summary>The log file's listener, <c>FileLog</c>, or <see langword="null"/> when it was removed.</summary>
    public FileLogTraceListener? DefaultFileLogWriter => _traceSource.Listeners[DefaultFileLogTraceListenerName] as FileLogTraceListener;

    /// <summary>The trace source the log writes to.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public TraceSource TraceSource => _traceSource;

    /// <summary>Writes an Information entry.</summary>
    /// <param name="message">The message.</param>
    public void WriteEntry(string message) => WriteEntry(message, TraceEventType.Information, TraceEventTypeToId(TraceEventType.Information));

    /// <summary>Writes an entry of a severity.</summary>
    /// <param name="message">The message.</param>
    /// <param name="severity">The severity.</param>
    public void WriteEntry(string message, TraceEventType severity) => WriteEntry(message, severity, TraceEventTypeToId(severity));

    /// <summary>Writes an entry of a severity with an identifier.</summary>
    /// <param name="message">The message.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="id">The identifier.</param>
    public void WriteEntry(string message, TraceEventType severity, int id) => _traceSource.TraceEvent(severity, id, message ?? string.Empty);

    /// <summary>Writes an exception's message as an Error entry.</summary>
    /// <param name="ex">The exception.</param>
    public void WriteException(Exception ex) => WriteException(ex, TraceEventType.Error, string.Empty, TraceEventTypeToId(TraceEventType.Error));

    /// <summary>Writes an exception's message with more information.</summary>
    /// <param name="ex">The exception.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="additionalInfo">Text written after the message.</param>
    public void WriteException(Exception ex, TraceEventType severity, string additionalInfo) =>
        WriteException(ex, severity, additionalInfo, TraceEventTypeToId(severity));

    /// <summary>Writes an exception's message with more information and an identifier.</summary>
    /// <param name="ex">The exception.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="additionalInfo">Text written after the message.</param>
    /// <param name="id">The identifier.</param>
    public void WriteException(Exception ex, TraceEventType severity, string additionalInfo, int id)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var builder = new StringBuilder(ex.Message);
        if (!string.IsNullOrEmpty(additionalInfo))
        {
            builder.Append(' ').Append(additionalInfo);
        }

        _traceSource.TraceEvent(severity, id, builder.ToString());
    }

    /// <summary>Adds the <c>FileLog</c> listener and logs Information and more severe entries, as without a configuration file.</summary>
    protected internal virtual void InitializeWithDefaultsSinceNoConfigExists()
    {
        _traceSource.Listeners.Add(new FileLogTraceListener(DefaultFileLogTraceListenerName));
        _traceSource.Switch.Level = SourceLevels.Information;
    }

    private static int TraceEventTypeToId(TraceEventType traceEventValue) => IdHash.GetValueOrDefault(traceEventValue);
}
