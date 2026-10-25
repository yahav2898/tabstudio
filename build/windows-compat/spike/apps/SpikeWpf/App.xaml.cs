using System.Runtime.InteropServices;
using System.Windows;

namespace SpikeWpf;

public partial class App : Application
{
    public static bool Animate { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        Console.WriteLine($"STARTED {Environment.TickCount64} {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
        if (e.Args.Contains("--hello"))
        {
            Console.WriteLine("HELLO from WPF");
            Shutdown();
            return;
        }

        Animate = e.Args.Contains("--animate");
        base.OnStartup(e);
    }
}
