using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Hosting;

namespace TabStudio.Forms.Tests.Hosting;

/// <summary>
/// The desktop the app preview shows a running app on: windows in their frames, the pointer and the keyboard reaching
/// the app's controls, the native text box over the field with focus, and windows moved, closed and scaled to fit.
/// </summary>
public sealed class DesktopTests
{
    [AvaloniaFact]
    public void The_main_form_opens_centered_with_its_title_and_a_click_reaches_its_button()
    {
        Label? label = null;
        using var app = HostedApp.Start(() =>
        {
            var form = new Form { Text = "Hello, Forms", ClientSize = new Size(360, 180) };
            label = new Label { Text = "Not clicked yet", Location = new Point(20, 20), AutoSize = true };
            var button = new Button { Text = "Click me", Location = new Point(20, 60), Size = new Size(100, 28) };
            button.Click += (_, _) => label.Text = "Clicked";
            form.Controls.AddRange([label, button]);
            Application.Run(form);
            return 0;
        });

        var frame = Assert.Single(app.Desktop.Windows);
        Assert.Equal("Hello, Forms", frame.Title);
        Assert.Equal(new Size(360, 180), frame.Client.Size);
        Assert.Equal(1.0, app.Desktop.Scale);
        var desktop = app.Desktop.DesktopSize;
        Assert.InRange(frame.Frame.Left + (frame.Frame.Width / 2) - (desktop.Width / 2), -2, 2);
        // As on Windows, the form's bounds are centered, invisible resize borders included, which puts the visible
        // window a few pixels above the middle.
        Assert.InRange(frame.Frame.Top + (frame.Frame.Height / 2) - (desktop.Height / 2), -5, 0);

        app.Click(new Point(frame.Client.X + 70, frame.Client.Y + 74));
        app.WaitFor(() => app.Read(() => label!.Text) == "Clicked", "The click never reached the button.");
        app.Screenshot("hosting-running-form");
    }

    [AvaloniaFact]
    public void Text_typed_in_the_native_editor_reaches_the_text_box_through_its_KeyPress_handler()
    {
        TextBox? box = null;
        using var app = HostedApp.Start(() =>
        {
            var form = new Form { Text = "Letters only", ClientSize = new Size(300, 120) };
            box = new TextBox { Location = new Point(20, 20), Width = 200 };
            box.KeyPress += (_, e) => e.Handled = char.IsDigit(e.KeyChar);
            form.Controls.Add(box);
            Application.Run(form);
            return 0;
        });

        var frame = app.Desktop.Windows[0];
        app.Click(new Point(frame.Client.X + 60, frame.Client.Y + 30));
        app.WaitFor(() => app.Desktop.Windows[0].Editor is not null, "The text box never got the native editor.");
        app.Type("a1b2c");
        app.WaitFor(() => app.Read(() => box!.Text) == "abc", "The typed text never reached the text box.");

        // The app's own change replaces what the editor shows.
        app.Runtime.Post(() => box!.Text = "Replaced");
        app.WaitFor(() => app.Desktop.Windows[0].Editor?.Text == "Replaced", "The app's text never reached the editor.");
    }

    [AvaloniaFact]
    public void Dragging_the_title_bar_moves_the_window_and_its_close_button_ends_the_run()
    {
        using var app = HostedApp.Start(() =>
        {
            Application.Run(new Form { Text = "Drag me", ClientSize = new Size(300, 150) });
            return 7;
        });

        var before = app.Desktop.Windows[0].Frame;
        app.Drag(new Point(before.X + 100, before.Y + 15), new Point(before.X + 160, before.Y + 55));
        app.WaitFor(() => app.Desktop.Windows[0].Frame.Location == new Point(before.X + 60, before.Y + 40), "The window never moved with the drag.");

        var frame = app.Desktop.Windows[0];
        var close = ChromeGeometry.Button(frame, WindowPart.Close);
        app.Click(new Point(close.X + (close.Width / 2), close.Y + (close.Height / 2)));
        app.WaitFor(() => app.Host.Completion.IsCompleted, "Closing the main form never ended the run.");
        Assert.Equal(7, app.Host.Completion.Result.ExitCode);
        Assert.Null(app.Host.Completion.Result.Error);
    }

    [AvaloniaFact]
    public void A_form_larger_than_the_pane_scales_the_desktop_to_fit_and_stays_centered()
    {
        using var app = HostedApp.Start(
            () =>
            {
                Application.Run(new Form { Text = "Big", ClientSize = new Size(1000, 700) });
                return 0;
            },
            width: 600,
            height: 450);

        app.WaitFor(() => app.Desktop.Scale < 1, "The desktop never scaled down.");
        app.Settle();
        var frame = app.Desktop.Windows[0].Frame;
        var desktop = app.Desktop.DesktopSize;
        Assert.True(frame.Width <= desktop.Width && frame.Height <= desktop.Height, $"{frame} does not fit {desktop}.");
        app.WaitFor(() => Math.Abs(app.Desktop.Windows[0].Frame.Left + (frame.Width / 2) - (desktop.Width / 2)) <= 2, "The form was not centered again.");
    }

    [AvaloniaFact]
    public void A_message_box_blocks_its_owner_until_its_button_is_pressed()
    {
        DialogResult? result = null;
        using var app = HostedApp.Start(() =>
        {
            var form = new Form { Text = "Owner", ClientSize = new Size(400, 200) };
            var button = new Button { Text = "Ask", Location = new Point(20, 20) };
            button.Click += (_, _) => result = MessageBox.Show(form, "Save the changes?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            form.Controls.Add(button);
            Application.Run(form);
            return 0;
        });

        var owner = app.Desktop.Windows[0];
        app.Click(new Point(owner.Client.X + 50, owner.Client.Y + 30));
        app.WaitFor(() => app.Desktop.Windows.Count == 2, "The message box never showed.");
        var box = app.Desktop.Windows[1];
        Assert.True(box.IsModal);
        Assert.Equal("Question", box.Title);
        Assert.True(app.Desktop.Windows[0].IsBlocked);
        app.Screenshot("hosting-message-box");

        // A press on the blocked owner does nothing but bring the message box forward.
        app.Click(new Point(owner.Client.X + 50, owner.Client.Y + 30));
        Assert.Equal(2, app.Desktop.Windows.Count);

        // N is the No button's mnemonic: typed on a button, it presses No, as in Windows.
        app.Type("n");
        app.WaitFor(() => app.Desktop.Windows.Count == 1, "The message box never closed.");
        Assert.Equal(DialogResult.No, app.Read(() => result));
    }

    [AvaloniaFact]
    public void Minimizing_a_window_shows_its_button_on_the_desktop_which_restores_it()
    {
        using var app = HostedApp.Start(() =>
        {
            Application.Run(new Form { Text = "Shrink", ClientSize = new Size(300, 150) });
            return 0;
        });

        var frame = app.Desktop.Windows[0];
        var minimize = ChromeGeometry.Button(frame, WindowPart.Minimize);
        app.Click(new Point(minimize.X + 10, minimize.Y + 10));
        app.WaitFor(() => app.Desktop.Windows[0].IsMinimized, "The window never minimized.");
        app.Screenshot("hosting-minimized");

        var chip = OverlayView.MinimizedBounds(0, app.Desktop.DesktopSize);
        app.Click(new Point(chip.X + 40, chip.Y + 10));
        app.WaitFor(() => !app.Desktop.Windows[0].IsMinimized, "The window never restored.");
    }
}
