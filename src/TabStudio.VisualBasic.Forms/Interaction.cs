using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;
using TabStudio.Forms.Runtime;

namespace Microsoft.VisualBasic;

/// <summary>
/// What <c>Microsoft.VisualBasic.Interaction.MsgBox</c> and <c>InputBox</c> of Microsoft.VisualBasic.Core call: .NET
/// looks this type up by name in "Microsoft.VisualBasic.Forms" and invokes its <c>MsgBox</c> and <c>InputBox</c>.
/// </summary>
/// <remarks>
/// In a Windows Forms app the dialogs are TabStudio's message box and an input box on the app's UI thread, also when
/// called from another thread. A console app has no window, so the prompt goes to its console and the answer, when
/// there is a choice, is read from it.
/// </remarks>
internal static class _Interaction
{
    private const int ButtonsMask = 0xF;
    private const int IconMask = 0xF0;
    private const int DefaultButtonMask = 0xF00;

    /// <summary>Shows a message box and returns the button pressed, as <c>MsgBox</c> does.</summary>
    /// <param name="Prompt">The message; converted to text as Visual Basic converts values.</param>
    /// <param name="Buttons">The buttons, icon and default button.</param>
    /// <param name="Title">The title; the app's name when <see langword="null"/>.</param>
    /// <returns>The button pressed.</returns>
    public static MsgBoxResult MsgBox(object? Prompt, MsgBoxStyle Buttons, object? Title)
    {
        var vbHost = HostServices.VBHost;
        var buttons = (int)Buttons;
        if ((buttons & ButtonsMask) > (int)MsgBoxStyle.RetryCancel
            || (buttons & IconMask) > (int)MsgBoxStyle.Information
            || (buttons & DefaultButtonMask) > (int)MsgBoxStyle.DefaultButton3)
        {
            buttons = (int)MsgBoxStyle.OkOnly;
        }

        string? prompt;
        try
        {
            prompt = Prompt is null ? null : (string?)Conversions.ChangeType(Prompt, typeof(string));
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            throw new ArgumentException("Argument 'Prompt' cannot be converted to type 'String'.", nameof(Prompt), ex);
        }

        string? title;
        try
        {
            title = Title is null ? vbHost?.GetWindowTitle() ?? DefaultTitle() : Conversions.ToString(Title);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            throw new ArgumentException("Argument 'Title' cannot be converted to type 'String'.", nameof(Title), ex);
        }

        if (FormsRuntime.Current is null)
        {
            return ConsoleMsgBox(prompt ?? string.Empty, (MsgBoxStyle)buttons, Title is null ? null : title, ConsoleInput(), Console.Out);
        }

        return OnUiThread(
            () => (MsgBoxResult)MessageBox.Show(
                vbHost?.GetParentWindow(),
                prompt,
                title,
                (MessageBoxButtons)(buttons & ButtonsMask),
                (MessageBoxIcon)(buttons & IconMask),
                (MessageBoxDefaultButton)(buttons & DefaultButtonMask),
                (MessageBoxOptions)(buttons & unchecked((int)0xFFFFF000))),
            MsgBoxResult.Cancel);
    }

    /// <summary>Asks for text and returns it, or an empty string when the user cancels, as <c>InputBox</c> does.</summary>
    /// <param name="Prompt">The question.</param>
    /// <param name="Title">The title; the app's name when empty.</param>
    /// <param name="DefaultResponse">The text the box starts with.</param>
    /// <param name="XPos">The dialog's left on the screen, or -1 to center it.</param>
    /// <param name="YPos">The dialog's top on the screen, or -1 to center it.</param>
    /// <returns>The text typed, or an empty string.</returns>
    public static string InputBox(string? Prompt, string? Title, string? DefaultResponse, int XPos, int YPos)
    {
        var vbHost = HostServices.VBHost;
        var explicitTitle = !string.IsNullOrEmpty(Title);
        var title = explicitTitle ? Title! : vbHost?.GetWindowTitle() ?? DefaultTitle();
        if (FormsRuntime.Current is null)
        {
            return ConsoleInputBox(Prompt ?? string.Empty, explicitTitle ? title : null, DefaultResponse ?? string.Empty, ConsoleInput(), Console.Out);
        }

        return OnUiThread(
            () =>
            {
                using var box = new InputBoxForm(Prompt ?? string.Empty, title, DefaultResponse ?? string.Empty, XPos, YPos);
                box.ShowDialog(vbHost?.GetParentWindow());
                return box.Output;
            },
            string.Empty);
    }

    /// <summary>
    /// A message box in a console: writes the title and prompt and, when there is a choice, reads it, such as <c>y</c>
    /// or <c>No</c>. An empty line, or the end of the input, chooses the default button.
    /// </summary>
    internal static MsgBoxResult ConsoleMsgBox(string prompt, MsgBoxStyle buttons, string? title, TextReader input, TextWriter output)
    {
        MsgBoxResult[] choices = ((int)buttons & ButtonsMask) switch
        {
            (int)MsgBoxStyle.OkCancel => [MsgBoxResult.Ok, MsgBoxResult.Cancel],
            (int)MsgBoxStyle.AbortRetryIgnore => [MsgBoxResult.Abort, MsgBoxResult.Retry, MsgBoxResult.Ignore],
            (int)MsgBoxStyle.YesNoCancel => [MsgBoxResult.Yes, MsgBoxResult.No, MsgBoxResult.Cancel],
            (int)MsgBoxStyle.YesNo => [MsgBoxResult.Yes, MsgBoxResult.No],
            (int)MsgBoxStyle.RetryCancel => [MsgBoxResult.Retry, MsgBoxResult.Cancel],
            _ => [MsgBoxResult.Ok],
        };
        var defaultIndex = ((int)buttons & DefaultButtonMask) switch
        {
            (int)MsgBoxStyle.DefaultButton2 => 1,
            (int)MsgBoxStyle.DefaultButton3 => 2,
            _ => 0,
        };
        var fallback = choices[Math.Min(defaultIndex, choices.Length - 1)];
        output.WriteLine(string.IsNullOrEmpty(title) ? prompt : $"{title}: {prompt}");
        if (choices.Length == 1)
        {
            output.Flush();
            return fallback;
        }

        var names = string.Join("/", choices.Select(ChoiceName));
        while (true)
        {
            output.Write($"[{names}] ");
            output.Flush();
            var line = input.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(line))
            {
                return fallback;
            }

            foreach (var choice in choices)
            {
                if (ChoiceName(choice).StartsWith(line, StringComparison.OrdinalIgnoreCase))
                {
                    return choice;
                }
            }
        }
    }

    /// <summary>
    /// An input box in a console: writes the title, prompt and default answer, and reads a line. An empty line gives
    /// the default answer; the end of the input gives an empty string, as Cancel does.
    /// </summary>
    internal static string ConsoleInputBox(string prompt, string? title, string defaultResponse, TextReader input, TextWriter output)
    {
        if (!string.IsNullOrEmpty(title))
        {
            output.WriteLine(title);
        }

        output.Write(defaultResponse.Length > 0 ? $"{prompt} [{defaultResponse}] " : $"{prompt} ");
        output.Flush();
        var line = input.ReadLine();
        return line is null ? string.Empty : line.Length == 0 ? defaultResponse : line;
    }

    private static string ChoiceName(MsgBoxResult choice) => choice == MsgBoxResult.Ok ? "OK" : choice.ToString();

    /// <summary>The title of a dialog the code gives none: the name of the app TabStudio runs.</summary>
    private static string DefaultTitle() => FormsRuntime.Current?.EntryAssembly?.GetName().Name ?? Application.ProductName;

    /// <summary>The console's input; on Android it throws until TabStudio connects it to a running program.</summary>
    private static TextReader ConsoleInput()
    {
        try
        {
            return Console.In;
        }
        catch (PlatformNotSupportedException)
        {
            return TextReader.Null;
        }
    }

    /// <summary>Shows a dialog on the app's UI thread, also when called from another thread of the app, and waits for it.</summary>
    private static T OnUiThread<T>(Func<T> dialog, T whenStopped)
    {
        if (FormsRuntime.Current is { IsOnThread: false } runtime)
        {
            var result = whenStopped;
            return runtime.Send(() => result = dialog(), Timeout.InfiniteTimeSpan) ? result : whenStopped;
        }

        return dialog();
    }
}
