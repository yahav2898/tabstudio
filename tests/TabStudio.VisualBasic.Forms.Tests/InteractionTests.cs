using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using Microsoft.VisualBasic;
using TabStudio.VisualBasic.Forms.Tests.Infrastructure;

namespace TabStudio.VisualBasic.Forms.Tests;

/// <summary>
/// MsgBox and InputBox of Microsoft.VisualBasic.Core, which find TabStudio's Microsoft.VisualBasic.Forms by name as
/// they find Microsoft's on Windows, and show TabStudio's dialogs; in a console app, the console.
/// </summary>
public sealed class InteractionTests
{
    [AvaloniaFact]
    public void MsgBox_shows_a_message_box_and_returns_the_button_pressed()
    {
        MsgBoxResult? result = null;
        using var run = FormsTestRun.Start(() => new Form { Text = "Main" });

        // The message box runs a modal loop, so the call is posted rather than waited for.
        run.Post(() => result = Interaction.MsgBox("Save the changes?", MsgBoxStyle.YesNo | MsgBoxStyle.Question, "Editor"));
        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 2, FormsTestRun.Timeout), "The message box was not shown.");

        var dialog = run.Invoke(() => Form.ActiveForm!);
        Assert.Equal("Editor", run.Invoke(() => dialog.Text));
        var yes = run.Invoke(() => FormsTestRun.Descendants(dialog).OfType<Button>().Single(b => b.Text == "&Yes"));
        run.Invoke(() => yes.PerformClick());

        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 1, FormsTestRun.Timeout), "The message box did not close.");
        Assert.Equal(MsgBoxResult.Yes, run.Invoke(() => result));
    }

    [AvaloniaFact]
    public void InputBox_shows_the_default_answer_and_returns_what_the_user_typed()
    {
        string? answer = null;
        using var run = FormsTestRun.Start(() => new Form { Text = "Main" });

        run.Post(() => answer = Interaction.InputBox("What is your name?", "Greeting", "Ada"));
        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 2, FormsTestRun.Timeout), "The input box was not shown.");

        var box = run.Invoke(() => Form.ActiveForm!);
        Assert.Equal("Greeting", run.Invoke(() => box.Text));
        var text = run.Invoke(() => FormsTestRun.Descendants(box).OfType<TextBox>().Single());
        Assert.Equal("Ada", run.Invoke(() => text.Text));
        run.Invoke(() => text.Text = "Grace");
        var ok = run.Invoke(() => FormsTestRun.Descendants(box).OfType<Button>().Single(b => b.Text == "OK"));
        run.Invoke(() => ok.PerformClick());

        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 1, FormsTestRun.Timeout), "The input box did not close.");
        Assert.Equal("Grace", run.Invoke(() => answer));
    }

    [AvaloniaFact]
    public void InputBox_returns_an_empty_string_when_the_user_cancels()
    {
        string? answer = null;
        using var run = FormsTestRun.Start(() => new Form { Text = "Main" });

        run.Post(() => answer = Interaction.InputBox("What is your name?", "Greeting", "Ada"));
        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 2, FormsTestRun.Timeout), "The input box was not shown.");
        var box = run.Invoke(() => Form.ActiveForm!);
        var cancel = run.Invoke(() => FormsTestRun.Descendants(box).OfType<Button>().Single(b => b.Text == "Cancel"));
        run.Invoke(() => cancel.PerformClick());

        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 1, FormsTestRun.Timeout), "The input box did not close.");
        Assert.Equal(string.Empty, run.Invoke(() => answer));
    }

    [Fact]
    public void In_a_console_MsgBox_writes_the_prompt_and_reads_the_choice()
    {
        var output = new StringWriter();

        var result = _Interaction.ConsoleMsgBox("Save the changes?", MsgBoxStyle.YesNoCancel, "Editor", new StringReader("maybe\nn\n"), output);

        Assert.Equal(MsgBoxResult.No, result);
        Assert.StartsWith("Editor: Save the changes?", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("[Yes/No/Cancel]", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void In_a_console_MsgBox_chooses_the_default_button_without_an_answer_and_does_not_wait_for_OK()
    {
        Assert.Equal(MsgBoxResult.No, _Interaction.ConsoleMsgBox("Delete?", MsgBoxStyle.YesNo | MsgBoxStyle.DefaultButton2, null, TextReader.Null, TextWriter.Null));
        Assert.Equal(MsgBoxResult.Retry, _Interaction.ConsoleMsgBox("Failed.", MsgBoxStyle.RetryCancel, null, new StringReader("\n"), TextWriter.Null));

        var input = new StringReader("left unread");
        Assert.Equal(MsgBoxResult.Ok, _Interaction.ConsoleMsgBox("Done.", MsgBoxStyle.OkOnly, null, input, TextWriter.Null));
        Assert.Equal("left unread", input.ReadToEnd());
    }

    [Fact]
    public void In_a_console_InputBox_reads_a_line_or_gives_the_default_answer()
    {
        var output = new StringWriter();

        Assert.Equal("Grace", _Interaction.ConsoleInputBox("What is your name?", "Greeting", "Ada", new StringReader("Grace\n"), output));
        Assert.Equal("Ada", _Interaction.ConsoleInputBox("What is your name?", null, "Ada", new StringReader("\n"), TextWriter.Null));
        Assert.Equal(string.Empty, _Interaction.ConsoleInputBox("What is your name?", null, "Ada", TextReader.Null, TextWriter.Null));
        Assert.Contains("What is your name? [Ada]", output.ToString(), StringComparison.Ordinal);
    }
}
