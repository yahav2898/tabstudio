namespace TabStudio.Swarm;

/// <summary>An exception on a device, as the tablet shows it: its type, message and stack trace.</summary>
/// <param name="Type">The exception's full type name, such as <c>System.InvalidOperationException</c>.</param>
/// <param name="Message">Its message.</param>
/// <param name="StackTrace">Where it was thrown, with inner exceptions, as .NET prints it.</param>
public sealed record SwarmFault(string Type, string Message, string StackTrace)
{
    /// <summary>Describes an exception.</summary>
    /// <param name="exception">The exception.</param>
    public static SwarmFault From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new SwarmFault(exception.GetType().FullName ?? exception.GetType().Name, exception.Message, exception.ToString());
    }
}
