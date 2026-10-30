namespace Microsoft.VisualBasic.Logging;

/// <summary>What <see cref="FileLogTraceListener"/> does when a log entry would break its size or free-space limits.</summary>
public enum DiskSpaceExhaustedOption
{
    /// <summary>Throws an exception.</summary>
    ThrowException = 0,

    /// <summary>Leaves the entry out.</summary>
    DiscardMessages = 1,
}

/// <summary>Whether <see cref="FileLogTraceListener"/> starts a new log file every day or week.</summary>
public enum LogFileCreationScheduleOption
{
    /// <summary>One log file.</summary>
    None = 0,

    /// <summary>A log file a day, with the date in its name.</summary>
    Daily = 1,

    /// <summary>A log file a week, with the date of the week's first day in its name.</summary>
    Weekly = 2,
}

/// <summary>The folder <see cref="FileLogTraceListener"/> writes its log file to.</summary>
public enum LogFileLocation
{
    /// <summary>The temporary folder.</summary>
    TempDirectory = 0,

    /// <summary>The app's data folder of the user: <c>Application.UserAppDataPath</c>.</summary>
    LocalUserApplicationDirectory = 1,

    /// <summary>The app's data folder shared by every user: <c>Application.CommonAppDataPath</c>.</summary>
    CommonApplicationDirectory = 2,

    /// <summary>The app's folder: its project's folder in TabStudio.</summary>
    ExecutableDirectory = 3,

    /// <summary>The folder <see cref="FileLogTraceListener.CustomLocation"/> names.</summary>
    Custom = 4,
}
