using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SpikeWpf;

public partial class MainWindow : Window
{
    private int _clicks;

    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += (_, _) =>
        {
            var center = ClickButton.PointToScreen(new Point(ClickButton.ActualWidth / 2, ClickButton.ActualHeight / 2));
            var open = OpenButton.PointToScreen(new Point(OpenButton.ActualWidth / 2, OpenButton.ActualHeight / 2));
            Console.WriteLine($"SHOWN {Environment.TickCount64} BUTTON {(int)center.X} {(int)center.Y} OPEN {(int)open.X} {(int)open.Y} TIER {RenderCapability.Tier >> 16} SCREEN {SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}");
            if (App.Animate)
            {
                Rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });
            }
        };
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        _clicks++;
        Status.Text = $"Clicked {_clicks} times";
        ClickButton.Background = Brushes.LightGreen;
        Console.WriteLine($"CLICKED {_clicks}");
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        var second = new Window { Title = "Second window", Width = 320, Height = 160, Left = 300, Top = 300, WindowStartupLocation = WindowStartupLocation.Manual, Content = new System.Windows.Controls.TextBlock { Text = "A second top-level window", Margin = new Thickness(16) } };
        second.ContentRendered += (_, _) => Console.WriteLine("OPENED second");
        second.Show();
    }
}
