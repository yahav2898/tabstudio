using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Styling;

namespace TabStudio.App.Swarm;

/// <summary>
/// The screen of a virtual device: a frame of the device's size that draws the app as that device would, in its
/// system theme, its text direction and its font scale.
/// </summary>
/// <remarks>
/// The frame plays the device's system: a <see cref="ThemeVariantScope"/> set to light or dark, which an app that
/// follows the system theme follows; the flow direction, mirrored for right to left; the default font size of
/// Avalonia's theme on a device, times the font scale, with <see cref="ScreenFontScale"/> scaling the sizes the app
/// sets itself. Whatever the app draws outside the frame is clipped, as a screen's edge does.
/// </remarks>
public sealed class VirtualDeviceFrame : Decorator
{
    /// <summary>The default font size of Avalonia's Fluent theme, which a device's top-level control sets.</summary>
    public const double DefaultFontSize = 14;

    private readonly ThemeVariantScope _system = new();

    /// <summary>Creates an empty frame.</summary>
    public VirtualDeviceFrame()
    {
        ScreenFontScale.Install();
        ClipToBounds = true;
        Child = _system;
    }

    /// <summary>Whether its device was removed, so no place shows the frame again.</summary>
    public bool IsRetired { get; private set; }

    /// <summary>The app drawn on the screen: the root of its view, in its theme.</summary>
    public Control? App
    {
        get => _system.Child;
        set => _system.Child = value;
    }

    /// <summary>Takes the frame out of wherever it is shown, for good, as its device is removed.</summary>
    public void Retire()
    {
        IsRetired = true;
        App = null;
        ScreenPlacement.Detach(this);
    }

    /// <summary>Makes the frame the screen a profile describes.</summary>
    /// <param name="profile">The screen.</param>
    public void Apply(ScreenProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Width = profile.Width;
        Height = profile.Height;
        _system.RequestedThemeVariant = profile.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        _system.FlowDirection = profile.Direction == TextDirection.RightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
        ScreenFontScale.SetScale(_system, profile.FontScale);
        TextElement.SetFontSize(_system, DefaultFontSize * profile.FontScale);
    }
}
