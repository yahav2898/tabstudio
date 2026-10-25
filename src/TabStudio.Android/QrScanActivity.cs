using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using TabStudio.App.Platform;
using TabStudio.Link.Security;
using AndroidManifest = Android.Manifest;
using AndroidSize = Android.Util.Size;

namespace TabStudio.Android;

/// <summary>
/// The camera, full screen, until it sees a TabStudio Link host's QR code: Camera2 shows the preview and hands frames
/// to ZXing (through <see cref="QrFrameDecoder"/>), without Google Play Services. Started by <see cref="AndroidQrScanner"/>.
/// </summary>
/// <remarks>
/// The screen keeps the rotation it had when the scanner opened, so the preview never turns while the user aims.
/// Going back, or <c>Type the code instead</c>, closes the scanner without a result.
/// </remarks>
[Activity(
    Name = "com.tabstudio.ide.QrScanActivity",
    Theme = "@style/Theme.TabStudio",
    Exported = false,
    ScreenOrientation = ScreenOrientation.Locked,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize
        | ConfigChanges.ScreenLayout | ConfigChanges.UiMode | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
        | ConfigChanges.Navigation)]
public sealed class QrScanActivity : Activity, TextureView.ISurfaceTextureListener
{
    private const int CameraPermissionRequest = 1;
    private const int MaxFrameWidth = 1280;
    private const int MaxFrameHeight = 960;
    private const string Aim = "Point the camera at the QR code in the host's TabStudio Link window.";
    private const string NotLink = "That QR code is not from TabStudio Link. Scan the one the host shows under File > TabStudio Link > Be a host.";

    private static readonly Lock Gate = new();
    private static TaskCompletionSource<string?>? _pending;
    private static QrScanActivity? _current;

    private Handler? _main;
    private HandlerThread? _frameThread;
    private Handler? _frames;
    private TextureView? _preview;
    private TextView? _hint;
    private CameraDevice? _camera;
    private CameraCaptureSession? _session;
    private ImageReader? _reader;
    private Surface? _previewSurface;
    private AndroidSize? _frameSize;
    private int _sensorOrientation = 90;
    private bool _permitted;
    private bool _opening;
    private bool _finished;
    private int _decoding;

    /// <summary>Starts waiting for a scan; a scan still waiting ends without a result.</summary>
    /// <returns>The QR code's text, or <see langword="null"/> when the user went back.</returns>
    internal static Task<string?> Begin()
    {
        var scan = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string?>? previous;
        lock (Gate)
        {
            previous = _pending;
            _pending = scan;
        }

        previous?.TrySetResult(null);
        return scan.Task;
    }

    /// <summary>Closes the scanner without a result.</summary>
    internal static void Cancel()
    {
        QrScanActivity? current;
        lock (Gate)
        {
            current = _current;
        }

        Complete(scan => scan.TrySetResult(null));
        current?.RunOnUiThread(current.Finish);
    }

    /// <inheritdoc />
    public void OnSurfaceTextureAvailable(SurfaceTexture? surface, int width, int height) => OpenCamera();

    /// <inheritdoc />
    public bool OnSurfaceTextureDestroyed(SurfaceTexture? surface)
    {
        CloseCamera();
        return true;
    }

    /// <inheritdoc />
    public void OnSurfaceTextureSizeChanged(SurfaceTexture? surface, int width, int height) => ConfigureTransform();

    /// <inheritdoc />
    public void OnSurfaceTextureUpdated(SurfaceTexture? surface)
    {
    }

    /// <inheritdoc />
    public override void OnRequestPermissionsResult(int requestCode, string[]? permissions, Permission[]? grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions!, grantResults!);
        if (requestCode != CameraPermissionRequest)
        {
            return;
        }

        if (grantResults is [Permission.Granted, ..])
        {
            _permitted = true;
            OpenCamera();
        }
        else
        {
            Fail(new UnauthorizedAccessException("TabStudio may not use the camera."));
        }
    }

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        lock (Gate)
        {
            _current = this;
        }

        _main = new Handler(Looper.MainLooper!);
        SetContentView(CreateLayout());
        _permitted = CheckSelfPermission(AndroidManifest.Permission.Camera) == Permission.Granted;
        if (!_permitted)
        {
            RequestPermissions([AndroidManifest.Permission.Camera], CameraPermissionRequest);
        }
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        _frameThread = new HandlerThread("TabStudio QR frames");
        _frameThread.Start();
        _frames = new Handler(_frameThread.Looper!);
        if (_preview?.IsAvailable == true)
        {
            OpenCamera();
        }
    }

    /// <inheritdoc />
    protected override void OnPause()
    {
        CloseCamera();
        _frameThread?.QuitSafely();
        _frameThread?.Join();
        _frameThread?.Dispose();
        _frameThread = null;
        _frames?.Dispose();
        _frames = null;
        base.OnPause();
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        lock (Gate)
        {
            if (ReferenceEquals(_current, this))
            {
                _current = null;
            }
        }

        // Going back ends the scan without a result.
        Complete(scan => scan.TrySetResult(null));
        _main?.Dispose();
        _main = null;
        base.OnDestroy();
    }

    private static void Complete(Action<TaskCompletionSource<string?>> complete)
    {
        TaskCompletionSource<string?>? scan;
        lock (Gate)
        {
            scan = _pending;
            _pending = null;
        }

        if (scan is not null)
        {
            complete(scan);
        }
    }

    private static string? ChooseCamera(CameraManager manager)
    {
        string? any = null;
        foreach (var id in manager.GetCameraIdList())
        {
            var facing = (Java.Lang.Integer?)manager.GetCameraCharacteristics(id).Get(CameraCharacteristics.LensFacing!);
            if (facing?.IntValue() == (int)LensFacing.Back)
            {
                return id;
            }

            any ??= id;
        }

        return any;
    }

    /// <summary>The largest frame size up to 1280 by 960: enough to read a code from a phone's screen at arm's length, small enough to decode several times a second.</summary>
    private static AndroidSize ChooseFrameSize(AndroidSize[]? sizes)
    {
        var fitting = (sizes ?? []).Where(s => s.Width <= MaxFrameWidth && s.Height <= MaxFrameHeight).ToList();
        return fitting.Count == 0 ? new AndroidSize(640, 480) : fitting.MaxBy(s => (long)s.Width * s.Height)!;
    }

    private FrameLayout CreateLayout()
    {
        var density = Resources?.DisplayMetrics?.Density ?? 1f;
        int Dp(int value) => (int)((value * density) + 0.5f);

        var root = new FrameLayout(this);
        root.SetFitsSystemWindows(true);
        root.SetBackgroundColor(Color.Black);

        _preview = new TextureView(this) { SurfaceTextureListener = this };
        root.AddView(_preview, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var outline = new GradientDrawable();
        outline.SetStroke(Dp(3), Color.White);
        outline.SetCornerRadius(Dp(20));
        var viewfinder = new View(this) { Background = outline };
        root.AddView(viewfinder, new FrameLayout.LayoutParams(Dp(240), Dp(240), GravityFlags.Center));

        _hint = new TextView(this) { Text = Aim, Gravity = GravityFlags.Center };
        _hint.SetTextColor(Color.White);
        _hint.SetTextSize(ComplexUnitType.Sp, 16);
        _hint.SetBackgroundColor(Color.Argb(170, 0, 0, 0));
        _hint.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
        root.AddView(_hint, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Top));

        var typeInstead = new Button(this) { Text = "Type the code instead" };
        typeInstead.Click += (_, _) => Finish();
        root.AddView(
            typeInstead,
            new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom | GravityFlags.CenterHorizontal)
            {
                BottomMargin = Dp(32),
            });
        return root;
    }

    private void OpenCamera()
    {
        if (!_permitted || _opening || _finished || _camera is not null || _frames is null || _preview?.SurfaceTexture is not { } texture)
        {
            return;
        }

        try
        {
            var manager = (CameraManager)GetSystemService(CameraService)!;
            if (ChooseCamera(manager) is not { } id)
            {
                Fail(new InvalidOperationException("this device has no camera TabStudio can use."));
                return;
            }

            var characteristics = manager.GetCameraCharacteristics(id);
            _sensorOrientation = ((Java.Lang.Integer?)characteristics.Get(CameraCharacteristics.SensorOrientation!))?.IntValue() ?? 90;
            var map = (StreamConfigurationMap?)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap!);
            var size = ChooseFrameSize(map?.GetOutputSizes((int)ImageFormatType.Yuv420888));
            _frameSize = size;
            texture.SetDefaultBufferSize(size.Width, size.Height);
            _reader = ImageReader.NewInstance(size.Width, size.Height, ImageFormatType.Yuv420888, 2);
            _reader.SetOnImageAvailableListener(new FrameListener(this), _frames);
            _opening = true;
            manager.OpenCamera(id, new CameraCallback(this), _main);
            ConfigureTransform();
        }
        catch (CameraAccessException ex)
        {
            Fail(new InvalidOperationException(ex.Message, ex));
        }
    }

    private void OnCameraOpened(CameraDevice camera)
    {
        _opening = false;
        if (_finished || _camera is not null || _reader?.Surface is not { } frames || _preview?.SurfaceTexture is not { } texture)
        {
            camera.Close();
            return;
        }

        _camera = camera;
        _previewSurface = new Surface(texture);
        var outputs = new List<OutputConfiguration> { new(_previewSurface), new(frames) };
        try
        {
            // 0 is SessionConfiguration.SESSION_REGULAR.
            camera.CreateCaptureSession(new SessionConfiguration(0, outputs, MainExecutor!, new SessionCallback(this)));
        }
        catch (CameraAccessException ex)
        {
            Fail(new InvalidOperationException(ex.Message, ex));
        }
    }

    private void OnCameraLost(CameraDevice camera, string? error)
    {
        _opening = false;
        camera.Close();
        if (ReferenceEquals(_camera, camera))
        {
            _camera = null;
        }

        if (error is not null)
        {
            Fail(new InvalidOperationException(error));
        }
    }

    private void OnSessionConfigured(CameraCaptureSession session)
    {
        if (_camera is not { } camera || _previewSurface is null || _reader?.Surface is not { } frames)
        {
            session.Close();
            return;
        }

        _session = session;
        try
        {
            var request = camera.CreateCaptureRequest(CameraTemplate.Preview);
            request.AddTarget(_previewSurface);
            request.AddTarget(frames);
            request.Set(CaptureRequest.ControlAfMode!, (int)ControlAFMode.ContinuousPicture);
            session.SetRepeatingRequest(request.Build(), null, _main);
        }
        catch (CameraAccessException ex)
        {
            Fail(new InvalidOperationException(ex.Message, ex));
        }
        catch (Java.Lang.IllegalStateException)
        {
            // The camera closed meanwhile, because the scanner is closing.
        }
    }

    /// <summary>Copies the brightness of the newest frame and decodes it on the thread pool, one frame at a time. Frame thread.</summary>
    private void OnFrame(ImageReader? reader)
    {
        var image = reader?.AcquireLatestImage();
        if (image is null)
        {
            return;
        }

        try
        {
            if (Interlocked.CompareExchange(ref _decoding, 1, 0) != 0)
            {
                return;
            }

            var plane = image.GetPlanes()![0];
            var buffer = plane.Buffer!;
            var stride = plane.RowStride;
            var width = image.Width;
            var height = image.Height;
            var frame = new byte[stride * height];
            buffer.Get(frame, 0, Math.Min(buffer.Remaining(), frame.Length));
            _ = Task.Run(() => Decode(frame, stride, width, height));
        }
        catch (Java.Lang.IllegalStateException)
        {
            // The reader closed meanwhile, because the scanner is closing.
            Volatile.Write(ref _decoding, 0);
        }
        finally
        {
            image.Close();
            image.Dispose();
        }
    }

    private void Decode(byte[] frame, int stride, int width, int height)
    {
        try
        {
            if (QrFrameDecoder.Decode(frame, stride, width, height) is { } text)
            {
                RunOnUiThread(() => OnCodeRead(text));
            }
        }
        finally
        {
            Volatile.Write(ref _decoding, 0);
        }
    }

    private void OnCodeRead(string text)
    {
        if (_finished)
        {
            return;
        }

        if (!LinkInvitation.TryParse(text, out _))
        {
            if (_hint is not null)
            {
                _hint.Text = NotLink;
            }

            return;
        }

        _finished = true;
        Complete(scan => scan.TrySetResult(text));
        Finish();
    }

    private void Fail(Exception error)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Complete(scan => scan.TrySetException(error));
        Finish();
    }

    private void CloseCamera()
    {
        _session?.Close();
        _session?.Dispose();
        _session = null;
        _camera?.Close();
        _camera?.Dispose();
        _camera = null;
        _reader?.Close();
        _reader?.Dispose();
        _reader = null;
        _previewSurface?.Release();
        _previewSurface?.Dispose();
        _previewSurface = null;
        _opening = false;
    }

    /// <summary>
    /// Fills the preview with the camera's picture, upright and undistorted. The camera already turns the picture
    /// upright for the device's natural orientation; this turns it for the screen's rotation, and crops it to fill.
    /// </summary>
    private void ConfigureTransform()
    {
        if (_preview is not { Width: > 0, Height: > 0 } view || _frameSize is not { } size)
        {
            return;
        }

        var rotation = DisplayRotationDegrees();
        var sideways = _sensorOrientation % 180 != 0;
        float naturalWidth = sideways ? size.Height : size.Width;
        float naturalHeight = sideways ? size.Width : size.Height;
        var turned = rotation % 180 != 0;
        var shownWidth = turned ? naturalHeight : naturalWidth;
        var shownHeight = turned ? naturalWidth : naturalHeight;
        var centerX = view.Width / 2f;
        var centerY = view.Height / 2f;
        var fill = Math.Max(view.Width / shownWidth, view.Height / shownHeight);

        var matrix = new Matrix();
        matrix.PostScale(naturalWidth / view.Width, naturalHeight / view.Height, centerX, centerY);
        matrix.PostRotate(-rotation, centerX, centerY);
        matrix.PostScale(fill, fill, centerX, centerY);
        view.SetTransform(matrix);
    }

    private int DisplayRotationDegrees()
    {
        SurfaceOrientation? rotation;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            rotation = Display?.Rotation;
        }
        else
        {
            rotation = WindowManager?.DefaultDisplay?.Rotation;
        }

        return rotation switch
        {
            SurfaceOrientation.Rotation90 => 90,
            SurfaceOrientation.Rotation180 => 180,
            SurfaceOrientation.Rotation270 => 270,
            _ => 0,
        };
    }

    private sealed class CameraCallback(QrScanActivity scanner) : CameraDevice.StateCallback
    {
        public override void OnOpened(CameraDevice? camera)
        {
            if (camera is not null)
            {
                scanner.OnCameraOpened(camera);
            }
        }

        public override void OnDisconnected(CameraDevice? camera)
        {
            if (camera is not null)
            {
                scanner.OnCameraLost(camera, null);
            }
        }

        public override void OnError(CameraDevice? camera, CameraError error)
        {
            if (camera is not null)
            {
                scanner.OnCameraLost(camera, error == CameraError.CameraInUse || error == CameraError.MaxCamerasInUse
                    ? "another app is using the camera."
                    : "the camera stopped with an error.");
            }
        }
    }

    private sealed class SessionCallback(QrScanActivity scanner) : CameraCaptureSession.StateCallback
    {
        public override void OnConfigured(CameraCaptureSession? session)
        {
            if (session is not null)
            {
                scanner.OnSessionConfigured(session);
            }
        }

        public override void OnConfigureFailed(CameraCaptureSession? session) =>
            scanner.Fail(new InvalidOperationException("the camera could not start a preview."));
    }

    private sealed class FrameListener(QrScanActivity scanner) : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        public void OnImageAvailable(ImageReader? reader) => scanner.OnFrame(reader);
    }
}
