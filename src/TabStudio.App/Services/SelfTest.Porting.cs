using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>Emulator proof of converting a form, executing its event code and preserving the original.</summary>
public static partial class SelfTest
{
    private static async Task<SelfTestResult> PortFormsAppAsync(MainViewModel main, ProjectTemplates templates, string? screenshots, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var solution = templates.Create(ProjectTemplates.WindowsFormsId, "SelfTestPort");
        var directory = Path.Combine(Path.GetDirectoryName(solution)!, "SelfTestPort");
        File.WriteAllText(Path.Combine(directory, "Form1.Designer.cs"), """
            namespace SelfTestPort
            {
                partial class Form1
                {
                    private Button convertButton;
                    private Label resultLabel;
                    private void InitializeComponent()
                    {
                        convertButton = new Button();
                        resultLabel = new Label();
                        convertButton.Text = "Test converted event";
                        convertButton.Location = new Point(20, 20);
                        convertButton.Size = new Size(180, 32);
                        convertButton.Click += convertButton_Click;
                        resultLabel.Text = "Ready";
                        resultLabel.Location = new Point(20, 75);
                        resultLabel.Size = new Size(220, 32);
                        Controls.Add(convertButton);
                        Controls.Add(resultLabel);
                        Text = "Ported form";
                        ClientSize = new Size(360, 180);
                    }
                }
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Form1.cs"), """
            namespace SelfTestPort
            {
                public partial class Form1 : Form
                {
                    public Form1() { InitializeComponent(); }
                    private void convertButton_Click(object? sender, EventArgs e)
                    {
                        resultLabel.Text = "Ported event works";
                    }
                }
            }
            """);
        var originals = Directory.GetFiles(directory).ToDictionary(p => p, File.ReadAllBytes);
        await main.OpenWorkspaceAsync(solution);
        var workspace = main.Workspace!;
        var summary = await workspace.PortProjectAsync(workspace.StartupProject!, "SelfTestPort.Avalonia");
        var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
        try
        {
            while (!workspace.AppPreview.IsRunning && !run.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(3))
            {
                await Task.Delay(PollInterval, cancellationToken);
            }

            if (workspace.AppPreview.View is not { } view)
            {
                return new SelfTestResult(false, "Port to Avalonia: no preview. " + LastLines(workspace), clock.Elapsed);
            }

            var button = view.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "convertButton");
            button?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var works = Texts(view).Contains("Ported event works");
            var unchanged = originals.All(f => f.Value.SequenceEqual(File.ReadAllBytes(f.Key)));
            if (screenshots is not null && TopLevel.GetTopLevel(view) is { } top)
            {
                Directory.CreateDirectory(screenshots);
                Capture(top, Path.Combine(screenshots, "port-to-avalonia.png"));
            }

            return new SelfTestResult(works && unchanged && summary.Result.Notes.Count == 0,
                $"Port to Avalonia: event={(works ? "OK" : "FAIL")}, original={(unchanged ? "unchanged" : "changed")}, {summary.Result.Notes.Count} notes", clock.Elapsed);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run.WaitAsync(cancellationToken);
        }
    }
}
