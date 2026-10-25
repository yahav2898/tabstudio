using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Notebooks.Markdown;
using TabStudio.Extensions.OpenVsx;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>A part of a README on the details page.</summary>
public abstract class ReadmeSegmentViewModel : ObservableObject
{
}

/// <summary>Markdown text of a README.</summary>
/// <param name="blocks">The text's blocks.</param>
public sealed class ReadmeTextViewModel(IReadOnlyList<MarkdownBlock> blocks) : ReadmeSegmentViewModel
{
    /// <summary>The text's blocks.</summary>
    public IReadOnlyList<MarkdownBlock> Blocks { get; } = blocks;
}

/// <summary>An image of a README, such as a screenshot, loaded from the package or the web.</summary>
public sealed partial class ReadmeImageViewModel : ReadmeSegmentViewModel
{
    // Screenshots are shown at most this wide; decoding them larger only costs memory.
    private const int DecodeWidth = 1600;

    private readonly Uri? _address;
    private readonly string? _path;

    /// <summary>Creates an image that loads from an address or a file.</summary>
    /// <param name="altText">The image's description.</param>
    /// <param name="address">Where it is on the web, or <see langword="null"/>.</param>
    /// <param name="path">The file in an installed extension, or <see langword="null"/>.</param>
    public ReadmeImageViewModel(string altText, Uri? address, string? path)
    {
        AltText = string.IsNullOrWhiteSpace(altText) ? "Image" : altText;
        _address = address;
        _path = path;
    }

    /// <summary>The image's description, shown when it cannot be drawn.</summary>
    public string AltText { get; }

    /// <summary>The image, once loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    public partial Bitmap? Image { get; private set; }

    /// <summary>Whether the image is shown.</summary>
    public bool HasImage => Image is not null;

    /// <summary>Whether the image could not be loaded, so its description is shown instead.</summary>
    [ObservableProperty]
    public partial bool IsUnavailable { get; private set; }

    /// <summary>Whether the image is loading.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>The image's natural width in device-independent pixels, to show it no wider.</summary>
    public double NaturalWidth => Image?.Size.Width ?? 0;

    /// <summary>Loads the image.</summary>
    /// <param name="client">The registry client, whose cache keeps downloaded images.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    public async Task LoadAsync(OpenVsxClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        IsLoading = true;
        try
        {
            Image = _path is not null ? await ExtensionImages.LoadFileAsync(_path, DecodeWidth, cancellationToken)
                : _address is not null ? await ExtensionImages.LoadAsync(client, _address, DecodeWidth, cancellationToken)
                : null;
            OnPropertyChanged(nameof(NaturalWidth));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            IsLoading = false;
        }

        IsUnavailable = Image is null;
    }
}
