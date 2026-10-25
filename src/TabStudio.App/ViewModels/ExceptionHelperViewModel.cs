using System.Globalization;
using System.Text;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Debugging;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The exception helper: the box Visual Studio shows at the line that threw when an exception goes unhandled, with
/// the exception's type and message, and its details on request.
/// </summary>
public sealed partial class ExceptionHelperViewModel : ObservableObject
{
    /// <summary>Creates the helper for an unhandled exception.</summary>
    /// <param name="exception">The exception.</param>
    /// <param name="callStack">The call stack where it was thrown, from the debugger, innermost first.</param>
    /// <param name="continueCommand">Continues the program, which then ends because of the exception.</param>
    /// <param name="stopCommand">Stops debugging.</param>
    public ExceptionHelperViewModel(Exception exception, IReadOnlyList<DebugStackFrame> callStack, ICommand continueCommand, ICommand stopCommand)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(callStack);
        ArgumentNullException.ThrowIfNull(continueCommand);
        ArgumentNullException.ThrowIfNull(stopCommand);

        ExceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        Message = exception.Message;
        Details = Describe(exception, callStack);
        ContinueCommand = continueCommand;
        StopCommand = stopCommand;
    }

    /// <summary>Creates the helper for an exception a debug adapter stopped at, as it describes it.</summary>
    /// <param name="exceptionType">The exception's type, such as <c>ZeroDivisionError</c>.</param>
    /// <param name="message">Its message.</param>
    /// <param name="details">Its type, message and stack trace, for View Details and Copy Details.</param>
    /// <param name="continueCommand">Continues the program.</param>
    /// <param name="stopCommand">Stops debugging.</param>
    public ExceptionHelperViewModel(string exceptionType, string message, string details, ICommand continueCommand, ICommand stopCommand)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(continueCommand);
        ArgumentNullException.ThrowIfNull(stopCommand);

        ExceptionType = exceptionType;
        Message = message;
        Details = details;
        ContinueCommand = continueCommand;
        StopCommand = stopCommand;
    }

    /// <summary>The exception's full type name, such as <c>System.InvalidOperationException</c>.</summary>
    public string ExceptionType { get; }

    /// <summary>The exception's message.</summary>
    public string Message { get; }

    /// <summary>The message as Visual Studio quotes it: <c>'Sequence contains no elements'</c>.</summary>
    public string QuotedMessage => $"'{Message}'";

    /// <summary>The type, message, inner exceptions and call stack, for View Details and Copy Details.</summary>
    public string Details { get; }

    /// <summary>Whether the details are shown.</summary>
    [ObservableProperty]
    public partial bool IsDetailsVisible { get; private set; }

    /// <summary>Whether the helper is shown; closing it leaves the program paused.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; } = true;

    /// <summary>Continues the program.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Stops debugging.</summary>
    public ICommand StopCommand { get; }

    [RelayCommand]
    private void ToggleDetails() => IsDetailsVisible = !IsDetailsVisible;

    [RelayCommand]
    private void Close() => IsOpen = false;

    private static string Describe(Exception exception, IReadOnlyList<DebugStackFrame> callStack)
    {
        var text = new StringBuilder();
        text.Append(exception.GetType().FullName).Append(": ").AppendLine(exception.Message);
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            text.Append(" ---> ").Append(inner.GetType().FullName).Append(": ").AppendLine(inner.Message);
        }

        foreach (var frame in callStack)
        {
            text.Append("   at ").Append(frame.MethodName);
            if (frame.Location is { } location)
            {
                text.Append(" in ").Append(Path.GetFileName(location.FilePath)).Append(":line ").Append(location.Line.ToString(CultureInfo.InvariantCulture));
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }
}
