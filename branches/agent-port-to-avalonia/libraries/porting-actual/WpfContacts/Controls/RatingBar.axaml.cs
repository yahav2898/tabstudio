using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ContactBook.Controls
{
    /// <summary>Five stars; tapping one sets the rating.</summary>
    public partial class RatingBar : UserControl
    {
        public static readonly StyledProperty<int> ValueProperty = AvaloniaProperty.Register<RatingBar, int>(nameof(Value), 0, defaultBindingMode: BindingMode.TwoWay);

        public static readonly RoutedEvent<RoutedEventArgs> ValueChangedEvent = RoutedEvent.Register<RatingBar, RoutedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

        static RatingBar()
        {
            ValueProperty.Changed.AddClassHandler<RatingBar>(OnValueChanged);
        }

        public RatingBar()
        {
            InitializeComponent();
            ShowStars();
        }

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public event EventHandler<RoutedEventArgs> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        private static void OnValueChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var bar = (RatingBar)d;
            bar.ShowStars();
            bar.RaiseEvent(new RoutedEventArgs(ValueChangedEvent));
        }

        private void ShowStars()
        {
            Stars.Children.Clear();
            for (var i = 1; i <= 5; i++)
            {
                var star = new TextBlock
                {
                    Text = i <= Value ? "★" : "☆",
                    FontSize = 18,
                    Foreground = i <= Value ? Brushes.Goldenrod : Brushes.Gray,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = i,
                };
                // TODO(port): MouseLeftButtonUp → PointerReleased. Avalonia raises it for every mouse button, the pen and touch; check e.InitialPressMouseButton == MouseButton.Left if only the left button counts.
                star.PointerReleased += (sender, _) => Value = (int)((Control)sender).Tag;
                Stars.Children.Add(star);
            }
        }
    }
}
