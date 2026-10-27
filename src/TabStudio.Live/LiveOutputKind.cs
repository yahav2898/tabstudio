namespace TabStudio.Live;

/// <summary>What a piece of the host's Output is, which decides its color on every device.</summary>
public enum LiveOutputKind
{
    /// <summary>Standard output of the running program.</summary>
    Program,

    /// <summary>Standard error of the running program, or a crash report.</summary>
    Error,

    /// <summary>A line someone typed into the program.</summary>
    Input,

    /// <summary>A message from TabStudio, such as "Build started".</summary>
    Info,

    /// <summary>A successful outcome.</summary>
    Success,

    /// <summary>A failed outcome.</summary>
    Failure,
}
