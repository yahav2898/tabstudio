using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Editing;

/// <summary>
/// Characters that reverse the direction of text are drawn by name: unseen, "Trojan Source" code (CVE-2021-42574)
/// reads differently in the editor from how it compiles.
/// </summary>
public sealed class BidiControlGeneratorTests
{
    [AvaloniaFact]
    public void A_right_to_left_override_in_code_is_shown_by_name()
    {
        var profile = DeviceProfile.For(new DeviceInfo(8L * 1024 * 1024 * 1024, 4, false));
        const string Code = "var access = \"user‮ ⁦// admin⁩ ⁦\";\n";
        using var document = new DocumentViewModel(Path.Combine(Path.GetTempPath(), "trojan.cs"), new TextFileContent(Code, new UTF8Encoding(false)), null, profile);
        using var view = new CodeEditorView(document);
        var window = new Window { Content = view, Width = 800, Height = 300 };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var textView = view.Editor.TextArea.TextView;
            var line = textView.GetOrConstructVisualLine(document.Document.GetLineByNumber(1));
            var marks = line.Elements.OfType<FormattedTextElement>().Where(e => e.GetType().Name == "MarkElement").ToList();

            Assert.Equal(4, marks.Count);
            Assert.All(marks, m => Assert.Equal(1, m.DocumentLength));
            Assert.Equal("RLO", BidiControlGenerator.NameOf('‮'));
            Assert.Null(BidiControlGenerator.NameOf('א'));
        }
        finally
        {
            window.Close();
        }
    }
}
