using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Microsoft.VisualBasic.Logging;

/// <summary>
/// A trace listener that writes entries to a log file on the device, one line each: source, severity, identifier and
/// message, separated by <see cref="Delimiter"/>. Each entry is written to the file straight away, so the file is
/// complete while the app runs.
/// </summary>
public class FileLogTraceListener : TraceListener
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DefaultName = "FileLogTraceListener";
    private const string FileExtension = ".log";
    private const int MinFileSize = 1000;

    private readonly object _gate = new();
    private bool _append = true;
    private string? _baseFileName;
    private string? _customLocation;
    private string _delimiter = "\t";
    private Encoding _encoding = Encoding.UTF8;
    private LogFileLocation _location = LogFileLocation.LocalUserApplicationDirectory;
    private long _maxFileSize = 5_000_000L;
    private long _reserveDiskSpace = 10_000_000L;
    private bool _started;

    /// <summary>Creates a listener named <c>FileLogTraceListener</c>.</summary>
    public FileLogTraceListener()
        : this(DefaultName)
    {
    }

    /// <summary>Creates a listener.</summary>
    /// <param name="name">The listener's name.</param>
    public FileLogTraceListener(string name)
        : base(name)
    {
    }

    /// <summary>Whether entries are added to an existing log file rather than replacing it the first time the listener writes.</summary>
    public bool Append
    {
        get => _append;
        set
        {
            lock (_gate)
            {
                _append = value;
                _started = false;
            }
        }
    }

    /// <summary>Whether every entry is flushed to the file: they always are on the tablet.</summary>
    public bool AutoFlush { get; set; }

    /// <summary>The log file's name without its folder, date and extension: the app's name, unless set.</summary>
    /// <exception cref="ArgumentNullException">The name is empty.</exception>
    public string BaseFileName
    {
        get => _baseFileName ??= Path.GetFileNameWithoutExtension(Application.ExecutablePath);
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value), "The base file name of the log cannot be empty.");
            }

            _ = Path.GetFullPath(value);
            _baseFileName = value;
            _started = false;
        }
    }

    /// <summary>The folder of the log file when <see cref="Location"/> is <see cref="LogFileLocation.Custom"/>: the app's data folder, unless set.</summary>
    public string CustomLocation
    {
        get => _customLocation ??= Application.UserAppDataPath;
        set
        {
            _customLocation = Path.GetFullPath(value);
            _started = false;
        }
    }

    /// <summary>What separates the parts of an entry: a tab, unless set.</summary>
    public string Delimiter
    {
        get => _delimiter;
        set => _delimiter = value;
    }

    /// <summary>What happens to an entry that would make the file bigger than <see cref="MaxFileSize"/>, or leave less free space than <see cref="ReserveDiskSpace"/>.</summary>
    /// <exception cref="InvalidEnumArgumentException">The value is not one of the enumeration's values.</exception>
    public DiskSpaceExhaustedOption DiskSpaceExhaustedBehavior
    {
        get;
        set
        {
            if (value is < DiskSpaceExhaustedOption.ThrowException or > DiskSpaceExhaustedOption.DiscardMessages)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(DiskSpaceExhaustedOption));
            }

            field = value;
        }
    } = DiskSpaceExhaustedOption.DiscardMessages;

    /// <summary>The encoding of the log file: UTF-8, unless set.</summary>
    /// <exception cref="ArgumentNullException">The encoding is <see langword="null"/>.</exception>
    public Encoding Encoding
    {
        get => _encoding;
        set => _encoding = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The full path of the file the next entry goes to.</summary>
    public string FullLogFileName
    {
        get
        {
            lock (_gate)
            {
                return CurrentFileName();
            }
        }
    }

    /// <summary>Whether every entry ends with the device's name.</summary>
    public bool IncludeHostName { get; set; }

    /// <summary>The folder of the log file: the app's data folder of the user, unless set.</summary>
    /// <exception cref="InvalidEnumArgumentException">The value is not one of the enumeration's values.</exception>
    public LogFileLocation Location
    {
        get => _location;
        set
        {
            if (value is < LogFileLocation.TempDirectory or > LogFileLocation.Custom)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(LogFileLocation));
            }

            _location = value;
            _started = false;
        }
    }

    /// <summary>Whether a new log file starts every day or week.</summary>
    /// <exception cref="InvalidEnumArgumentException">The value is not one of the enumeration's values.</exception>
    public LogFileCreationScheduleOption LogFileCreationSchedule
    {
        get;
        set
        {
            if (value is < LogFileCreationScheduleOption.None or > LogFileCreationScheduleOption.Weekly)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(LogFileCreationScheduleOption));
            }

            field = value;
        }
    }

    /// <summary>The largest size of the log file, in bytes: 5,000,000 unless set.</summary>
    /// <exception cref="ArgumentException">The size is less than 1000 bytes.</exception>
    public long MaxFileSize
    {
        get => _maxFileSize;
        set
        {
            if (value < MinFileSize)
            {
                throw new ArgumentException($"The MaxFileSize must be at least {MinFileSize.ToString(CultureInfo.InvariantCulture)} bytes.", nameof(value));
            }

            _maxFileSize = value;
        }
    }

    /// <summary>The free space, in bytes, that writing the log leaves on the device: 10,000,000 unless set.</summary>
    /// <exception cref="ArgumentException">The space is negative.</exception>
    public long ReserveDiskSpace
    {
        get => _reserveDiskSpace;
        set
        {
            if (value < 0)
            {
                throw new ArgumentException("The ReserveDiskSpace cannot be negative.", nameof(value));
            }

            _reserveDiskSpace = value;
        }
    }

    /// <summary>Ends writing: the next entry opens the file again.</summary>
    public override void Close() => Dispose(true);

    /// <summary>Flushes the log: every entry already is.</summary>
    public override void Flush()
    {
        // Every entry is written to the file and the file closed at once.
    }

    /// <summary>Writes an entry for data.</summary>
    /// <param name="eventCache">The context of the entry.</param>
    /// <param name="source">The trace source's name.</param>
    /// <param name="eventType">The severity.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="data">The data.</param>
    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, object? data) =>
        TraceEvent(eventCache, source, eventType, id, data?.ToString() ?? string.Empty);

    /// <summary>Writes an entry for data, the items separated by <see cref="Delimiter"/>.</summary>
    /// <param name="eventCache">The context of the entry.</param>
    /// <param name="source">The trace source's name.</param>
    /// <param name="eventType">The severity.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="data">The data.</param>
    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, params object?[]? data) =>
        TraceEvent(eventCache, source, eventType, id, data is null ? string.Empty : string.Join(Delimiter, data.Select(item => item?.ToString() ?? string.Empty)));

    /// <summary>Writes an entry: source, severity, identifier, message and the options the listener asks for.</summary>
    /// <param name="eventCache">The context of the entry.</param>
    /// <param name="source">The trace source's name.</param>
    /// <param name="eventType">The severity.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="message">The message.</param>
    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        if (Filter is { } filter && !filter.ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
        {
            return;
        }

        var delimiter = Delimiter;
        var builder = new StringBuilder()
            .Append(source).Append(delimiter)
            .Append(Enum.GetName(eventType)).Append(delimiter)
            .Append(id.ToString(CultureInfo.InvariantCulture)).Append(delimiter)
            .Append(message);
        if (eventCache is not null)
        {
            if ((TraceOutputOptions & TraceOptions.Callstack) != 0)
            {
                builder.Append(delimiter).Append(eventCache.Callstack);
            }

            if ((TraceOutputOptions & TraceOptions.LogicalOperationStack) != 0)
            {
                builder.Append(delimiter).Append('"').AppendJoin(", ", eventCache.LogicalOperationStack.Cast<object?>()).Append('"');
            }

            if ((TraceOutputOptions & TraceOptions.DateTime) != 0)
            {
                builder.Append(delimiter).Append(eventCache.DateTime.ToString("u", CultureInfo.InvariantCulture));
            }

            if ((TraceOutputOptions & TraceOptions.ProcessId) != 0)
            {
                builder.Append(delimiter).Append(eventCache.ProcessId.ToString(CultureInfo.InvariantCulture));
            }

            if ((TraceOutputOptions & TraceOptions.ThreadId) != 0)
            {
                builder.Append(delimiter).Append(eventCache.ThreadId);
            }

            if ((TraceOutputOptions & TraceOptions.Timestamp) != 0)
            {
                builder.Append(delimiter).Append(eventCache.Timestamp.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (IncludeHostName)
        {
            builder.Append(delimiter).Append(Environment.MachineName);
        }

        WriteLine(builder.ToString());
    }

    /// <summary>Writes an entry with a formatted message.</summary>
    /// <param name="eventCache">The context of the entry.</param>
    /// <param name="source">The trace source's name.</param>
    /// <param name="eventType">The severity.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="format">The message's composite format.</param>
    /// <param name="args">The format's arguments.</param>
    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args) =>
        TraceEvent(eventCache, source, eventType, id, args is null || format is null ? format : string.Format(CultureInfo.InvariantCulture, format, args));

    /// <summary>Writes text to the log file.</summary>
    /// <param name="message">The text.</param>
    /// <exception cref="InvalidOperationException">The text would break the size or free-space limits and <see cref="DiskSpaceExhaustedBehavior"/> is ThrowException.</exception>
    public override void Write(string? message) => WriteToFile(message ?? string.Empty, newLine: false);

    /// <summary>Writes a line to the log file.</summary>
    /// <param name="message">The line.</param>
    /// <exception cref="InvalidOperationException">The line would break the size or free-space limits and <see cref="DiskSpaceExhaustedBehavior"/> is ThrowException.</exception>
    public override void WriteLine(string? message) => WriteToFile(message ?? string.Empty, newLine: true);

    /// <summary>The attributes a configuration file may set on the listener.</summary>
    /// <returns>Their names.</returns>
    protected override string[] GetSupportedAttributes() =>
    [
        "append", "Append", "autoflush", "AutoFlush", "autoFlush", "basefilename", "BaseFilename", "baseFilename", "BaseFileName",
        "baseFileName", "customlocation", "CustomLocation", "customLocation", "delimiter", "Delimiter", "diskspaceexhaustedbehavior",
        "DiskSpaceExhaustedBehavior", "diskSpaceExhaustedBehavior", "encoding", "Encoding", "includehostname", "IncludeHostName",
        "includeHostName", "location", "Location", "logfilecreationschedule", "LogFileCreationSchedule", "logFileCreationSchedule",
        "maxfilesize", "MaxFileSize", "maxFileSize", "reservediskspace", "ReserveDiskSpace", "reserveDiskSpace",
    ];

    /// <summary>Releases the listener: the next entry opens the file again.</summary>
    /// <param name="disposing">Whether this is called from Dispose.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                _started = false;
            }
        }

        base.Dispose(disposing);
    }

    private static DateTime WeekStart(DateTime date) => date.AddDays(-(int)date.DayOfWeek).Date;

    private string CurrentFileName()
    {
        var folder = Location switch
        {
            LogFileLocation.TempDirectory => Path.GetTempPath(),
            LogFileLocation.CommonApplicationDirectory => Application.CommonAppDataPath,
            LogFileLocation.ExecutableDirectory => Path.GetDirectoryName(Application.ExecutablePath) ?? Application.StartupPath,
            LogFileLocation.Custom => CustomLocation,
            _ => Application.UserAppDataPath,
        };
        var name = BaseFileName;
        var today = DateTime.Now.Date;
        name += LogFileCreationSchedule switch
        {
            LogFileCreationScheduleOption.Daily => "-" + today.ToString(DateFormat, CultureInfo.InvariantCulture),
            LogFileCreationScheduleOption.Weekly => "-" + WeekStart(today).ToString(DateFormat, CultureInfo.InvariantCulture),
            _ => string.Empty,
        };
        return Path.GetFullPath(Path.Combine(folder, name + FileExtension));
    }

    private void WriteToFile(string text, bool newLine)
    {
        lock (_gate)
        {
            var path = CurrentFileName();
            var size = (long)_encoding.GetByteCount(text) + (newLine ? _encoding.GetByteCount(Environment.NewLine) : 0);
            if (!ResourcesAvailable(path, size))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var mode = _append || _started ? FileMode.Append : FileMode.Create;
            using (var stream = new FileStream(path, mode, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, _encoding))
            {
                writer.Write(text);
                if (newLine)
                {
                    writer.WriteLine();
                }
            }

            _started = true;
        }
    }

    private bool ResourcesAvailable(string path, long newEntrySize)
    {
        var current = File.Exists(path) && (_append || _started) ? new FileInfo(path).Length : 0;
        if (current + newEntrySize > _maxFileSize)
        {
            return Exhausted("Unable to write to log file because writing to it would cause it to exceed the MaxFileSize value.");
        }

        if (FreeSpace(Path.GetDirectoryName(path)) is { } free && free - newEntrySize < _reserveDiskSpace)
        {
            return Exhausted("Unable to write to log file because writing to it would reduce free disk space below ReservedSpace value.");
        }

        return true;
    }

    /// <summary>Leaves an entry out, or throws, as <see cref="DiskSpaceExhaustedBehavior"/> says.</summary>
    /// <returns><see langword="false"/>: the entry is not written.</returns>
    private bool Exhausted(string message)
    {
        if (DiskSpaceExhaustedBehavior == DiskSpaceExhaustedOption.DiscardMessages)
        {
            return false;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>The free space of the file system a folder is on, or <see langword="null"/> when the device does not say.</summary>
    private static long? FreeSpace(string? folder)
    {
        try
        {
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }

            Directory.CreateDirectory(folder);
            return new DriveInfo(folder).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
