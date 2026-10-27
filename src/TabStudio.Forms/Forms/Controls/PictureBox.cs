using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>Shows an image: at its size, stretched, centered or zoomed to fit.</summary>
[DefaultProperty(nameof(Image))]
public class PictureBox : Control, ISupportInitialize
{
    private static readonly object EventSizeModeChanged = new();
    private static readonly object EventLoadCompleted = new();

    private Image? _image;
    private PictureBoxSizeMode _sizeMode = PictureBoxSizeMode.Normal;
    private BorderStyle _borderStyle = BorderStyle.None;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private string? _imageLocation;

    /// <summary>Creates a picture box.</summary>
    public PictureBox()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        TabStop = false;
    }

    /// <summary>Occurs when <see cref="SizeMode"/> changes.</summary>
    public event EventHandler? SizeModeChanged
    {
        add => Events.AddHandler(EventSizeModeChanged, value);
        remove => Events.RemoveHandler(EventSizeModeChanged, value);
    }

    /// <summary>Occurs when an image loaded from <see cref="ImageLocation"/> is ready.</summary>
    public event AsyncCompletedEventHandler? LoadCompleted
    {
        add => Events.AddHandler(EventLoadCompleted, value);
        remove => Events.RemoveHandler(EventLoadCompleted, value);
    }

    /// <summary>The image.</summary>
    public Image? Image
    {
        get => _image;
        set
        {
            _image = value;
            ApplyAutoSize();
            Invalidate();
        }
    }

    /// <summary>How the image is fitted.</summary>
    public PictureBoxSizeMode SizeMode
    {
        get => _sizeMode;
        set
        {
            if (_sizeMode == value)
            {
                return;
            }

            _sizeMode = value;
            ApplyAutoSize();
            Invalidate();
            ((EventHandler?)Events[EventSizeModeChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The border.</summary>
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            UpdateClientSize();
            Invalidate();
        }
    }

    /// <summary>The path of a file to load the image from.</summary>
    public string? ImageLocation
    {
        get => _imageLocation;
        set
        {
            _imageLocation = value;
            if (!string.IsNullOrEmpty(value))
            {
                Load(value);
            }
        }
    }

    /// <summary>The image shown while one loads. Images load at once here.</summary>
    public Image? InitialImage { get; set; }

    /// <summary>The image shown when one fails to load.</summary>
    public Image? ErrorImage { get; set; }

    /// <summary>Whether images load synchronously. They always do here.</summary>
    public bool WaitOnLoad { get; set; }

    /// <inheritdoc />
    protected override Size DefaultSize => new(100, 50);

    /// <inheritdoc />
    internal override Padding NonClientInsets => new(ThemePainter.BorderWidth(_borderStyle));

    /// <summary>Loads the image from <see cref="ImageLocation"/>.</summary>
    public void Load()
    {
        if (string.IsNullOrEmpty(_imageLocation))
        {
            throw new InvalidOperationException("ImageLocation must be set.");
        }

        Load(_imageLocation);
    }

    /// <summary>Loads the image at a path or a web address, waiting for it, and raises LoadCompleted.</summary>
    /// <param name="url">A file's path, or an http or https address.</param>
    public void Load(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        _imageLocation = url;
        Exception? error = null;
        try
        {
            Image = Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile ? Download(uri) : Image.FromFile(uri?.IsFile == true ? uri.LocalPath : url);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            error = exception;
            Image = ErrorImage;
        }

        ((AsyncCompletedEventHandler?)Events[EventLoadCompleted])?.Invoke(this, new AsyncCompletedEventArgs(error, false, null));
        if (error is not null && Events[EventLoadCompleted] is null)
        {
            throw error;
        }
    }

    /// <summary>Downloads an image, keeping the app's windows responsive while it waits.</summary>
    private static Image Download(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https"))
        {
            throw new NotSupportedException($"PictureBox.Load reads files and http or https addresses, not {uri.Scheme} addresses.");
        }

        var download = Http.GetByteArrayAsync(uri);
        if (FormsRuntime.Current is { IsOnThread: true } runtime)
        {
            runtime.RunLoop(() => download.IsCompleted);
        }

        using var stream = new MemoryStream(download.GetAwaiter().GetResult());
        return Image.FromStream(stream);
    }

    /// <summary>Loads the image at <see cref="ImageLocation"/>; the app's windows stay responsive while it downloads.</summary>
    public void LoadAsync() => Load();

    /// <summary>Loads the image at a path or a web address; the app's windows stay responsive while it downloads.</summary>
    /// <param name="url">A file's path, or an http or https address.</param>
    public void LoadAsync(string url) => Load(url);

    /// <inheritdoc />
    public void BeginInit()
    {
    }

    /// <inheritdoc />
    public void EndInit()
    {
    }

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        var border = 2 * ThemePainter.BorderWidth(_borderStyle);
        return _image is null ? Size : new Size(_image.Width + border, _image.Height + border);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        if (_image is not { } image)
        {
            return;
        }

        var client = ClientRectangle;
        switch (_sizeMode)
        {
            case PictureBoxSizeMode.StretchImage:
                g.DrawImage(image, client);
                break;
            case PictureBoxSizeMode.CenterImage:
                g.DrawImage(image, new Rectangle((client.Width - image.Width) / 2, (client.Height - image.Height) / 2, image.Width, image.Height));
                break;
            case PictureBoxSizeMode.Zoom:
                g.DrawImage(image, Zoom(client, image.Size));
                break;
            default:
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
                break;
        }
    }

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g) => ThemePainter.DrawBorder(g, new Rectangle(Point.Empty, Size), _borderStyle);

    private void ApplyAutoSize()
    {
        if (_sizeMode == PictureBoxSizeMode.AutoSize && _image is not null)
        {
            Size = GetPreferredSizeCore(Size.Empty);
        }
    }
}
