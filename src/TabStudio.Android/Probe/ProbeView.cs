using System.Globalization;
using System.Runtime.InteropServices;
using Android.OS;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TabStudio.Compilation;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace TabStudio.Android.Probe;

/// <summary>
/// The probe's only page: the TabStudio header, the device, the program being compiled (colored by Roslyn),
/// its output and the steps the probe went through.
/// </summary>
/// <remarks>The UI is built in code so the temporary probe needs no XAML.</remarks>
internal sealed class ProbeView : UserControl
{
    private readonly ProbeRunner _runner;
    private readonly TextBlock _status;
    private readonly SelectableTextBlock _source;
    private readonly SelectableTextBlock _output;
    private readonly StackPanel _steps;
    private IInsetsManager? _insets;
    private bool _started;

    /// <summary>Creates the page. The probe starts when the page is first shown.</summary>
    /// <param name="runner">Runs the probe.</param>
    public ProbeView(ProbeRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;

        _status = new TextBlock
        {
            Text = "Compiling a C# program with Roslyn, on this device…",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = ProbePalette.Text,
            TextWrapping = TextWrapping.Wrap,
        };
        _source = CodeBlock(ProbeSources.Program.TrimEnd());
        _output = CodeBlock("Waiting for the program…");
        _steps = new StackPanel { Spacing = 10 };

        Background = ProbePalette.Window;
        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(32, 28),
                Spacing = 22,
                MaxWidth = 1100,
                Children =
                {
                    CreateHeader(),
                    Card(ProbePalette.Card, new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            _status,
                            new TextBlock { Text = DescribeDevice(), FontSize = 14, Foreground = ProbePalette.Muted, TextWrapping = TextWrapping.Wrap },
                        },
                    }),
                    Section("Program.cs", Card(ProbePalette.Code, _source)),
                    Section("Output", Card(ProbePalette.Code, _output)),
                    Section("Steps", Card(ProbePalette.Card, _steps)),
                },
            },
        };
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Keep the page clear of the status and navigation bars when Android draws the app edge to edge.
        _insets = TopLevel.GetTopLevel(this)?.InsetsManager;
        if (_insets is not null)
        {
            Padding = _insets.SafeAreaPadding;
            _insets.SafeAreaChanged += OnSafeAreaChanged;
        }

        if (!_started)
        {
            _started = true;
            _ = RunProbeAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_insets is not null)
        {
            _insets.SafeAreaChanged -= OnSafeAreaChanged;
            _insets = null;
        }

        base.OnUnloaded(e);
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => Padding = e.SafeAreaPadding;

    private async Task RunProbeAsync()
    {
        try
        {
            // The editor's first pass: syntax-only colors, available before Roslyn's semantic model is ready.
            var syntax = await Task.Run(() => SyntaxClassifier.Classify(ProbeSources.Program));
            await Dispatcher.UIThread.InvokeAsync(() => ShowSource(syntax));

            var report = await Task.Run(() => _runner.RunAsync(OnStep));
            await Dispatcher.UIThread.InvokeAsync(() => ShowPassed(report));
            ProbeLog.Passed(report);
        }
        catch (Exception exception)
        {
            // The verdict is logged first: CI must see it even if the page cannot show it.
            ProbeLog.Failed(exception);
            await Dispatcher.UIThread.InvokeAsync(() => ShowFailed(exception));
        }
    }

    /// <summary>Called on the probe's thread after each step.</summary>
    private void OnStep(ProbeStep step)
    {
        ProbeLog.Step(step);
        Dispatcher.UIThread.Post(() => _steps.Children.Add(CreateStepRow(step)));
    }

    private void ShowPassed(ProbeReport report)
    {
        ShowSource(report.Classifications);
        _output.Text = report.Output;
        _status.Text = $"Probe passed in {report.Duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s: Roslyn compiled and ran C# on this device.";
        _status.Foreground = ProbePalette.Passed;
    }

    private void ShowFailed(Exception exception)
    {
        _output.Text = exception.ToString();
        _output.Foreground = ProbePalette.Failed;
        _status.Text = $"Probe failed: {exception.GetType().Name}";
        _status.Foreground = ProbePalette.Failed;
    }

    /// <summary>Colors the program's source with classified ranges, the way the editor will.</summary>
    private void ShowSource(IReadOnlyList<ClassifiedRange> ranges)
    {
        var text = ProbeSources.Program.TrimEnd();
        var inlines = new InlineCollection();
        var position = 0;

        foreach (var range in ranges.OrderBy(r => r.Start))
        {
            var end = Math.Min(range.Start + range.Length, text.Length);
            if (range.Start < position || end <= range.Start)
            {
                continue;
            }

            if (range.Start > position)
            {
                inlines.Add(new Run(text[position..range.Start]));
            }

            inlines.Add(new Run(text[range.Start..end]) { Foreground = ProbePalette.ForClassification(range.Classification) });
            position = end;
        }

        if (position < text.Length)
        {
            inlines.Add(new Run(text[position..]));
        }

        _source.Inlines = inlines;
    }

    private static string DescribeDevice() =>
        $"Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt}) · {Build.Manufacturer} {Build.Model} · "
        + $"{RuntimeInformation.ProcessArchitecture} · {Environment.ProcessorCount} cores · {RuntimeInformation.FrameworkDescription}";

    private static StackPanel CreateHeader() => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 18,
        Children =
        {
            CreateLogo(64),
            new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = "TabStudio", FontSize = 34, FontWeight = FontWeight.SemiBold, Foreground = ProbePalette.Text },
                    new TextBlock { Text = "Platform probe · a temporary build until the IDE lands", FontSize = 15, Foreground = ProbePalette.Muted },
                },
            },
        },
    };

    /// <summary>The logo: the white "S" of the launcher icon on the brand-colored rounded square.</summary>
    private static Border CreateLogo(double size)
    {
        var glyph = new ShapePath
        {
            Data = Geometry.Parse("M17 5H9a3 3 0 0 0 0 6h6a3 3 0 0 1 0 6H6"),
            Stroke = ProbePalette.White,
            StrokeThickness = 2.6,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };

        // The glyph is drawn on a 24-unit grid; its centre is at (12, 11), so it moves down one unit.
        Canvas.SetTop(glyph, 1);

        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.24),
            Background = ProbePalette.Accent,
            Child = new Viewbox
            {
                Margin = new Thickness(size * 0.14),
                Child = new Canvas { Width = 24, Height = 24, Children = { glyph } },
            },
        };
    }

    private static Grid CreateStepRow(ProbeStep step)
    {
        var description = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15,
            Foreground = ProbePalette.Muted,
            Inlines = new InlineCollection
            {
                new Run("✓  ") { Foreground = ProbePalette.Passed },
                new Run(step.Name) { Foreground = ProbePalette.Text, FontWeight = FontWeight.SemiBold },
                new Run($"   {step.Detail}"),
            },
        };

        var duration = new TextBlock
        {
            Text = $"{step.Duration.TotalMilliseconds.ToString("N0", CultureInfo.InvariantCulture)} ms",
            FontFamily = ProbePalette.Monospace,
            FontSize = 14,
            Foreground = ProbePalette.Muted,
            Margin = new Thickness(16, 0, 0, 0),
        };
        Grid.SetColumn(duration, 1);

        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { description, duration } };
    }

    private static SelectableTextBlock CodeBlock(string text) => new()
    {
        Text = text,
        FontFamily = ProbePalette.Monospace,
        FontSize = 15,
        Foreground = ProbePalette.CodeText,
        TextWrapping = TextWrapping.Wrap,
    };

    private static Border Card(IBrush background, Control child) => new()
    {
        Background = background,
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(18, 16),
        Child = child,
    };

    private static StackPanel Section(string title, Control body) => new()
    {
        Spacing = 8,
        Children =
        {
            new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = ProbePalette.Muted },
            body,
        },
    };
}
