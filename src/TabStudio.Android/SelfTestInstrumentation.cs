using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

namespace TabStudio.Android;

/// <summary>
/// Starts the complete device self test through adb instrumentation, whose shell identity permits the desktop
/// checks to inject mouse movements through Android's input system on current Android versions.
/// </summary>
/// <remarks>The instrumentation stays active while the script collects the verdict and screenshots.</remarks>
[Instrumentation(Name = "com.tabstudio.ide.SelfTestInstrumentation", TargetPackage = "com.tabstudio.ide")]
public sealed class SelfTestInstrumentation : Instrumentation
{
    /// <summary>Creates the self-test instrumentation.</summary>
    public SelfTestInstrumentation()
    {
    }

    /// <summary>Attaches the managed runner to the instrumentation Android created before the app starts.</summary>
    /// <param name="handle">The native instrumentation instance.</param>
    /// <param name="transfer">Who owns its JNI reference.</param>
    public SelfTestInstrumentation(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
    {
    }

    /// <inheritdoc />
    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        Start();
    }

    /// <inheritdoc />
    public override void OnStart()
    {
        base.OnStart();
        var context = TargetContext ?? throw new InvalidOperationException("Android did not provide the self test's target context.");
        using var intent = new Intent(context, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.NewTask);
        intent.PutExtra(MainActivity.SelfTestExtra, true);
        intent.PutExtra(MainActivity.DesktopSelfTestExtra, true);
        context.StartActivity(intent);
    }
}
