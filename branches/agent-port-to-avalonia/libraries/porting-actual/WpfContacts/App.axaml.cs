using Avalonia;
using Avalonia.Markup.Xaml;
using ContactBook.Porting;

namespace ContactBook
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            // StartupUri: the first window, a window on the desktop and the whole screen on phones and tablets.
            WindowView.ShowMain(ApplicationLifetime, new MainWindow());
            base.OnFrameworkInitializationCompleted();
        }
    }
}
