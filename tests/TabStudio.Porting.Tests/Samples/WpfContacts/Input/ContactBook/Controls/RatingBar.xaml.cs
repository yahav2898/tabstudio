using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ContactBook.Controls
{
    /// <summary>Five stars; tapping one sets the rating.</summary>
    public partial class RatingBar : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(RatingBar),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RatingBar));

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

        public event RoutedEventHandler ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
                    Cursor = Cursors.Hand,
                    Tag = i,
                };
                star.MouseLeftButtonUp += (sender, _) => Value = (int)((FrameworkElement)sender).Tag;
                Stars.Children.Add(star);
            }
        }
    }
}
