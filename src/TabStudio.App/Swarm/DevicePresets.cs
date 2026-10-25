namespace TabStudio.App.Swarm;

/// <summary>A device a virtual screen can be: its name, kind, size and density, in portrait or landscape.</summary>
/// <param name="Name">What people call it, such as "Small phone".</param>
/// <param name="FormFactor">The kind of device.</param>
/// <param name="Width">Its width, in device-independent pixels.</param>
/// <param name="Height">Its height, in device-independent pixels.</param>
/// <param name="Scale">Physical pixels per device-independent pixel.</param>
public sealed record DevicePreset(string Name, FormFactor FormFactor, double Width, double Height, double Scale)
{
    /// <summary>Whether it is wider than tall.</summary>
    public bool IsLandscape => Width > Height;

    /// <summary>Its size, such as "360 × 640".</summary>
    public string SizeText => LayoutChecks.LayoutText.Dips(new Avalonia.Size(Width, Height));

    /// <summary>The screen it is, in the system's light theme, left to right, with the default font size.</summary>
    public ScreenProfile ToProfile() => new(Name, FormFactor, Width, Height, Scale);
}

/// <summary>
/// The virtual devices the Device Matrix offers: a small phone, a large phone, a foldable and a tablet, each in
/// portrait and in landscape, and any size the user types.
/// </summary>
public static class DevicePresets
{
    /// <summary>A small phone, 360 × 640.</summary>
    public static DevicePreset SmallPhone { get; } = new("Small phone", FormFactor.Phone, 360, 640, 2);

    /// <summary>A large phone, 412 × 915.</summary>
    public static DevicePreset LargePhone { get; } = new("Large phone", FormFactor.Phablet, 412, 915, 2.625);

    /// <summary>A foldable or phablet, unfolded, 600 × 960.</summary>
    public static DevicePreset Foldable { get; } = new("Foldable", FormFactor.Phablet, 600, 960, 2);

    /// <summary>A tablet, 800 × 1280.</summary>
    public static DevicePreset Tablet { get; } = new("Tablet", FormFactor.Tablet, 800, 1280, 2);

    /// <summary>The devices in portrait.</summary>
    public static IReadOnlyList<DevicePreset> Portrait { get; } = [SmallPhone, LargePhone, Foldable, Tablet];

    /// <summary>The same devices in landscape.</summary>
    public static IReadOnlyList<DevicePreset> Landscape { get; } = [.. Portrait.Select(Rotated)];

    /// <summary>The devices a new Device Matrix shows: a small phone, a large phone and a tablet.</summary>
    public static IReadOnlyList<DevicePreset> Defaults { get; } = [SmallPhone, LargePhone, Tablet];

    /// <summary>The smallest side a custom device may have, in device-independent pixels.</summary>
    public const double MinimumSide = 200;

    /// <summary>The largest side a custom device may have, in device-independent pixels.</summary>
    public const double MaximumSide = 2560;

    /// <summary>A device turned on its side: width and height swapped, named for its orientation.</summary>
    /// <param name="preset">The device.</param>
    public static DevicePreset Rotated(DevicePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        const string Suffix = " (landscape)";
        var name = preset.Name.EndsWith(Suffix, StringComparison.Ordinal) ? preset.Name[..^Suffix.Length] : preset.Name + Suffix;
        return preset with { Name = name, Width = preset.Height, Height = preset.Width };
    }

    /// <summary>A device of a size the user typed, of the kind its narrower side suggests.</summary>
    /// <param name="width">Its width.</param>
    /// <param name="height">Its height.</param>
    /// <param name="name">Its name, or <see langword="null"/> for one from its size.</param>
    public static DevicePreset Custom(double width, double height, string? name = null)
    {
        width = Math.Clamp(Math.Round(width), MinimumSide, MaximumSide);
        height = Math.Clamp(Math.Round(height), MinimumSide, MaximumSide);
        var narrower = Math.Min(width, height);
        var formFactor = narrower switch
        {
            < 400 => FormFactor.Phone,
            < 600 => FormFactor.Phablet,
            _ => FormFactor.Tablet,
        };
        var label = string.IsNullOrWhiteSpace(name) ? $"Custom {LayoutChecks.LayoutText.Dips(new Avalonia.Size(width, height))}" : name.Trim();
        return new DevicePreset(label, formFactor, width, height, 2);
    }
}
