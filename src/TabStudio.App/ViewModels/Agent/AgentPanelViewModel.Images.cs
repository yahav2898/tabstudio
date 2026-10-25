using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Agent.Anthropic;

namespace TabStudio.App.ViewModels.Agent;

/// <summary>A picture sent to Claude with a request, such as a screen with a layout problem outlined.</summary>
public sealed class AgentImageViewModel
{
    /// <summary>Creates a picture from a PNG image.</summary>
    /// <param name="caption">What it shows, such as "Small phone, 360 × 640".</param>
    /// <param name="png">The PNG image.</param>
    public AgentImageViewModel(string caption, byte[] png)
    {
        ArgumentNullException.ThrowIfNull(caption);
        ArgumentNullException.ThrowIfNull(png);
        Caption = caption;
        Png = png;
        using var stream = new MemoryStream(png);
        Thumbnail = new Bitmap(stream);
    }

    /// <summary>What it shows.</summary>
    public string Caption { get; }

    /// <summary>The PNG image.</summary>
    public ReadOnlyMemory<byte> Png { get; }

    /// <summary>The picture, for the panel to show.</summary>
    public Bitmap Thumbnail { get; }
}

/// <summary>
/// Pictures in the agent panel: a request can carry one, such as "Fix with Agent" on a layout problem sending the
/// screen it shows on. It waits above the input box until the request is sent, and the user can take it away.
/// </summary>
public sealed partial class AgentPanelViewModel
{
    /// <summary>The picture the next request sends, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAttachment))]
    public partial AgentImageViewModel? Attachment { get; private set; }

    /// <summary>Whether the next request sends a picture.</summary>
    public bool HasAttachment => Attachment is not null;

    /// <summary>
    /// Asks the agent something with a picture, on the user's behalf. While the agent is busy, or before it has an
    /// API key, the request and its picture wait for the user to send them.
    /// </summary>
    /// <param name="prompt">The request.</param>
    /// <param name="image">The picture.</param>
    public Task AskAsync(string prompt, AgentImageViewModel image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Attachment = image;
        return AskAsync(prompt);
    }

    /// <summary>Takes the picture away from the next request.</summary>
    [RelayCommand]
    private void RemoveAttachment() => Attachment = null;

    /// <summary>Takes the picture for a request being sent, as the message content Claude sees.</summary>
    private List<ImageBlock> TakeAttachment(out AgentImageViewModel? attachment)
    {
        attachment = Attachment;
        Attachment = null;
        return attachment is null ? [] : [new ImageBlock("image/png", attachment.Png)];
    }
}
