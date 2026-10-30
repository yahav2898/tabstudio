using System.ComponentModel;
using System.Runtime.Serialization;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>Thrown when a single-instance app cannot hand its command line to the instance that is already running.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Serializable]
public class CantStartSingleInstanceException : Exception
{
    /// <summary>Creates the exception with the standard message.</summary>
    public CantStartSingleInstanceException()
        : base("This single-instance application could not connect to the original instance.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public CantStartSingleInstanceException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">The message.</param>
    /// <param name="inner">The cause.</param>
    public CantStartSingleInstanceException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>Creates the exception from serialized data, which .NET no longer uses.</summary>
    /// <param name="info">The data.</param>
    /// <param name="context">The context.</param>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [Obsolete("Type or member is obsolete", DiagnosticId = "SYSLIB0051")]
    protected CantStartSingleInstanceException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}

/// <summary>Thrown when a Windows Forms app with the application framework has no main form to run.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Serializable]
public class NoStartupFormException : Exception
{
    /// <summary>Creates the exception with the standard message.</summary>
    public NoStartupFormException()
        : base("A startup form has not been specified.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public NoStartupFormException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">The message.</param>
    /// <param name="inner">The cause.</param>
    public NoStartupFormException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>Creates the exception from serialized data, which .NET no longer uses.</summary>
    /// <param name="info">The data.</param>
    /// <param name="context">The context.</param>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [Obsolete("Type or member is obsolete", DiagnosticId = "SYSLIB0051")]
    protected NoStartupFormException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
