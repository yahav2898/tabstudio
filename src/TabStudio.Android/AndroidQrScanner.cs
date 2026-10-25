using Android.Content;
using Android.Content.PM;
using TabStudio.App.Platform;
using AndroidActivity = Android.App.Activity;

namespace TabStudio.Android;

/// <summary>Scans the TabStudio Link host's QR code with the camera, in <see cref="QrScanActivity"/>.</summary>
/// <param name="activity">The activity that starts the scanner.</param>
internal sealed class AndroidQrScanner(Func<AndroidActivity> activity) : IQrScanner
{
    /// <inheritdoc />
    public bool IsAvailable => activity().PackageManager?.HasSystemFeature(PackageManager.FeatureCameraAny) == true;

    /// <inheritdoc />
    public async Task<string?> ScanAsync(CancellationToken cancellationToken)
    {
        var scan = QrScanActivity.Begin();
        var host = activity();
        host.StartActivity(new Intent(host, typeof(QrScanActivity)));
        using (cancellationToken.Register(QrScanActivity.Cancel))
        {
            return await scan.ConfigureAwait(true);
        }
    }
}
