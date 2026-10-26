using Avalonia;
using Avalonia.Markup.Xaml;
using WinFormsApp1.Porting;

namespace WinFormsApp1;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        WindowView.ShowMain(ApplicationLifetime, new Form1());
        base.OnFrameworkInitializationCompleted();
    }
}
