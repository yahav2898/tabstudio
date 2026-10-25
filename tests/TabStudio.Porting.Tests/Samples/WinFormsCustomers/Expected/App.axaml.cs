using Avalonia;
using Avalonia.Markup.Xaml;
using CustomerEntry.Porting;

namespace CustomerEntry;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        WindowView.ShowMain(ApplicationLifetime, new MainForm());
        base.OnFrameworkInitializationCompleted();
    }
}
