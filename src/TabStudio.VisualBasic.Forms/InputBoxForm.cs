using System.Drawing;
using System.Windows.Forms;

namespace Microsoft.VisualBasic;

/// <summary>The dialog of <c>InputBox</c>, laid out as Windows lays it out: the prompt, OK and Cancel, and a text box below.</summary>
internal sealed class InputBoxForm : Form
{
    private readonly Label _label;
    private readonly TextBox _textBox;

    public InputBoxForm(string prompt, string title, string defaultResponse, int xPos, int yPos)
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(353, 120);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Name = "VBInputBox";
        Text = title;

        _label = new Label { Name = "Label", Location = new Point(10, 10), Size = new Size(250, 70), Text = prompt, UseMnemonic = false };
        var ok = new Button { Name = "OKButton", Location = new Point(290, 12), Size = new Size(60, 22), Text = "OK", TabIndex = 2 };
        var cancel = new Button { Name = "MyCancelButton", Location = new Point(290, 40), Size = new Size(60, 22), Text = "Cancel", TabIndex = 3, DialogResult = DialogResult.Cancel };
        _textBox = new TextBox { Name = "TextBox", Location = new Point(10, 90), Size = new Size(335, 20), Text = defaultResponse, TabIndex = 1 };
        ok.Click += (_, _) =>
        {
            Output = _textBox.Text;
            Close();
        };
        cancel.Click += (_, _) => Close();
        Controls.Add(_textBox);
        Controls.Add(_label);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
        ActiveControl = _textBox;

        // A long prompt makes the dialog taller, as on Windows.
        var needed = TextRenderer.MeasureText(prompt, _label.Font, new Size(_label.Width, 0), TextFormatFlags.WordBreak);
        if (needed.Height > _label.Height)
        {
            var grow = needed.Height - _label.Height;
            _label.Height += grow;
            _textBox.Top += grow;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + grow);
        }

        if (xPos == -1 && yPos == -1)
        {
            StartPosition = FormStartPosition.CenterScreen;
        }
        else
        {
            StartPosition = FormStartPosition.Manual;
            DesktopLocation = new Point(xPos == -1 ? 600 : xPos, yPos == -1 ? 350 : yPos);
        }

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>What the user typed when they pressed OK; empty when they cancelled.</summary>
    public string Output { get; private set; } = string.Empty;
}
