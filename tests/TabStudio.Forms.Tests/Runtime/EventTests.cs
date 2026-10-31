using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Tests.Infrastructure;

namespace TabStudio.Forms.Tests.Runtime;

/// <summary>Events in the order Windows Forms raises them, on an app's own Forms thread.</summary>
public sealed class EventTests
{
    [AvaloniaFact]
    public void A_tap_on_a_button_raises_Click_and_its_handler_changes_a_label()
    {
        Label label = null!;
        Button button = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(300, 200), Text = "Click test" };
            label = new Label { Location = new Point(12, 12), AutoSize = true, Text = "Waiting" };
            button = new Button { Location = new Point(12, 40), Size = new Size(75, 23), Text = "Go" };
            button.Click += (_, _) => label.Text = "Clicked";
            form.Controls.Add(label);
            form.Controls.Add(button);
            return form;
        });

        run.Tap(button);

        Assert.Equal("Clicked", run.Invoke(() => label.Text));
    }

    [AvaloniaFact]
    public void A_form_raises_Load_then_Activated_then_Shown()
    {
        var events = new List<string>();
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            form.Load += (_, _) => events.Add("Load");
            form.Activated += (_, _) => events.Add("Activated");
            form.Shown += (_, _) => events.Add("Shown");
            return form;
        });

        run.Settle();

        Assert.Equal(["Load", "Activated", "Shown"], run.Invoke(() => events.ToList()));
    }

    /// <summary>
    /// DualBrain's Solitaire, a Visual Basic app, handles the .NET Framework's Form.Closing event and keeps help in a
    /// HelpProvider; both were missing (BC30590, BC30002), so it did not build.
    /// </summary>
    [AvaloniaFact]
    public void Closing_comes_before_FormClosing_and_can_cancel_it_and_Closed_follows_FormClosed()
    {
        Form form = null!;
        var events = new List<string>();
        var cancel = true;
        using var run = FormsTestRun.Start(() =>
        {
            form = new Form();
            form.Closing += (_, e) => { events.Add("Closing"); e.Cancel = cancel; };
            form.FormClosing += (_, e) => events.Add(e.Cancel ? "FormClosing (cancelled)" : "FormClosing");
            form.FormClosed += (_, _) => events.Add("FormClosed");
            form.Closed += (_, _) => events.Add("Closed");
            var help = new HelpProvider { HelpNamespace = "solitaire.chm" };
            help.SetHelpString(form, "Deal a new game.");
            help.SetHelpNavigator(form, HelpNavigator.TableOfContents);
            Help.ShowHelp(form, help.HelpNamespace, HelpNavigator.TableOfContents);
            events.Add(help.GetHelpString(form) + " " + help.GetShowHelp(form) + " " + help.GetHelpNavigator(form));
            return form;
        });

        run.Invoke(() => form.WindowInternal!.CloseRequested());
        Assert.True(run.Invoke(() => form.Visible));
        cancel = false;
        run.Post(() => form.WindowInternal!.CloseRequested());
        Assert.True(run.Exit.Wait(TimeSpan.FromSeconds(10)));

        Assert.Equal(["Deal a new game. True TableOfContents", "Closing", "FormClosing (cancelled)", "Closing", "FormClosing", "FormClosed", "Closed"], events);
    }

    [AvaloniaFact]
    public void FormClosing_can_cancel_closing_and_the_run_ends_when_the_main_form_closes()
    {
        Form form = null!;
        var allowClose = false;
        using var run = FormsTestRun.Start(() =>
        {
            form = new Form();
            form.FormClosing += (_, e) => e.Cancel = !allowClose;
            return form;
        });

        run.Invoke(() => form.WindowInternal!.CloseRequested());
        Assert.True(run.Invoke(() => form.Visible));

        allowClose = true;
        // Closing the main form ends the message loop. A second Send used to settle Invoke can race the
        // runtime exit notification; post the close and wait for the actual exit below instead.
        run.Post(() => form.WindowInternal!.CloseRequested());

        Assert.True(run.Exit.Wait(TimeSpan.FromSeconds(10)));
        Assert.Null(run.Exit.Result.Error);
    }

    [AvaloniaFact]
    public void MessageBox_Show_returns_the_button_the_user_tapped()
    {
        Button button = null!;
        DialogResult? result = null;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            button = new Button { Text = "Ask" };
            button.Click += (_, _) => result = MessageBox.Show("Save the changes?", "Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            form.Controls.Add(button);
            return form;
        });

        // The tap opens a modal loop, so it is posted rather than waited for.
        run.Runtime.Post(() =>
        {
            var point = FormsTestRun.ToWindow(button, new Point(10, 10));
            button.Window!.PointerPressed(point, MouseButtons.Left, 1, touch: false);
            button.Window!.PointerReleased(point, MouseButtons.Left, touch: false);
        });
        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 2, TimeSpan.FromSeconds(10)), "The message box was not shown.");

        var dialog = run.Invoke(() => run.Runtime.ActiveWindow!.Form);
        Assert.Equal("Editor", run.Invoke(() => dialog.Text));
        var yes = run.Invoke(() => Descendants(dialog).OfType<Button>().Single(b => b.Text == "&Yes"));
        run.Tap(yes);

        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 1, TimeSpan.FromSeconds(10)), "The message box did not close.");
        Assert.Equal(DialogResult.Yes, run.Invoke(() => result));
    }

    [AvaloniaFact]
    public void An_unhandled_exception_in_a_handler_ends_the_app_with_it()
    {
        Button button = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            button = new Button();
            button.Click += (_, _) => throw new InvalidOperationException("Boom");
            form.Controls.Add(button);
            return form;
        });

        run.Runtime.Post(() =>
        {
            var point = FormsTestRun.ToWindow(button, new Point(5, 5));
            button.Window!.PointerPressed(point, MouseButtons.Left, 1, touch: false);
            button.Window!.PointerReleased(point, MouseButtons.Left, touch: false);
        });

        Assert.True(run.Exit.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal("Boom", run.Exit.Result.Error?.Message);
    }

    [AvaloniaFact]
    public void Application_ThreadException_handles_exceptions_and_the_app_keeps_running()
    {
        Button button = null!;
        Exception? handled = null;
        using var run = FormsTestRun.StartMain(() =>
        {
            Application.ThreadException += (_, e) => handled = e.Exception;
            var form = new Form();
            button = new Button();
            button.Click += (_, _) => throw new InvalidOperationException("Handled");
            form.Controls.Add(button);
            Application.Run(form);
            return 0;
        });
        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 1, TimeSpan.FromSeconds(10)));

        run.Tap(button);

        Assert.Equal("Handled", run.Invoke(() => handled?.Message));
        Assert.False(run.Exit.IsCompleted);
    }

    [AvaloniaFact]
    public void Typing_in_a_text_box_raises_TextChanged()
    {
        TextBox box = null!;
        var changes = 0;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            box = new TextBox { Location = new Point(10, 10) };
            box.TextChanged += (_, _) => changes++;
            form.Controls.Add(box);
            return form;
        });

        run.Tap(box);
        run.Type("abc");

        Assert.Equal("abc", run.Invoke(() => box.Text));
        Assert.Equal(1, run.Invoke(() => changes));
    }

    [AvaloniaFact]
    public void A_KeyPress_handler_can_reject_characters()
    {
        TextBox box = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            box = new TextBox();
            box.KeyPress += (_, e) => e.Handled = !char.IsDigit(e.KeyChar);
            form.Controls.Add(box);
            return form;
        });

        run.Tap(box);
        run.Type("4");
        run.Type("x");
        run.Type("2");

        Assert.Equal("42", run.Invoke(() => box.Text));
    }

    [AvaloniaFact]
    public void A_timer_ticks_on_the_forms_thread()
    {
        var ticks = 0;
        var onThread = true;
        System.Windows.Forms.Timer timer = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            timer = new System.Windows.Forms.Timer { Interval = 20 };
            timer.Tick += (_, _) =>
            {
                onThread &= !form.InvokeRequired;
                ticks++;
            };
            timer.Start();
            return form;
        });

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (run.Invoke(() => ticks) < 3 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }

        Assert.True(run.Invoke(() => ticks) >= 3);
        Assert.True(onThread);
    }

    [AvaloniaFact]
    public void Enter_clicks_the_accept_button_and_Escape_the_cancel_button()
    {
        Form form = null!;
        var log = new List<string>();
        using var run = FormsTestRun.Start(() =>
        {
            form = new Form();
            var ok = new Button { Text = "OK" };
            var cancel = new Button { Text = "Cancel", Top = 30 };
            ok.Click += (_, _) => log.Add("ok");
            cancel.Click += (_, _) => log.Add("cancel");
            var box = new TextBox { Top = 60 };
            form.Controls.AddRange([ok, cancel, box]);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            form.Shown += (_, _) => box.Focus();
            return form;
        });

        run.Key(Keys.Enter);
        run.Key(Keys.Escape);

        Assert.Equal(["ok", "cancel"], run.Invoke(() => log.ToList()));
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
