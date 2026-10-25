namespace TabStudio.App.Platform;

/// <summary>The size class of a device, which decides how much TabStudio keeps warm.</summary>
public enum DeviceClass
{
    /// <summary>Up to about 3 GB of memory, or a low-RAM device.</summary>
    Compact,

    /// <summary>About 4 to 6 GB of memory.</summary>
    Standard,

    /// <summary>More than about 6 GB of memory.</summary>
    Large,
}

/// <summary>
/// How TabStudio budgets memory and CPU on a particular device. Chosen once at startup from
/// <see cref="DeviceInfo"/>.
/// </summary>
/// <param name="Class">The device's size class.</param>
/// <param name="AnalyzeWholeSolution">
/// Whether the Error List covers every file in the solution after it opens. Otherwise only open files are
/// analyzed until the next build, as Visual Studio does with background analysis set to open documents.
/// </param>
/// <param name="BackgroundThreads">How many threads background analysis may use at once.</param>
/// <param name="AnalysisDelay">How long the editor waits after the last keystroke before analyzing.</param>
/// <param name="OutputCharacterLimit">How much program output the Output panel keeps before trimming the oldest.</param>
public sealed record DeviceProfile(
    DeviceClass Class,
    bool AnalyzeWholeSolution,
    int BackgroundThreads,
    TimeSpan AnalysisDelay,
    int OutputCharacterLimit)
{
    private const long Gigabyte = 1024L * 1024 * 1024;

    /// <summary>
    /// Whether only the projects being edited keep a full compiler model, and the projects they reference are
    /// seen through compact metadata images: on compact devices, where memory is what limits the solution size.
    /// </summary>
    public bool UseSkeletonReferences => Class == DeviceClass.Compact;

    /// <summary>Picks the profile for a device.</summary>
    /// <param name="device">The device's hardware.</param>
    public static DeviceProfile For(DeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Physical memory is reported a little below the marketing size (a "4 GB" tablet reports about 3.6 GB),
        // so the thresholds sit between the common sizes.
        if (device.IsLowRamDevice || device.TotalMemoryBytes < 3.5 * Gigabyte)
        {
            return new DeviceProfile(DeviceClass.Compact, AnalyzeWholeSolution: false, BackgroundThreads: 1, TimeSpan.FromMilliseconds(450), 250_000);
        }

        if (device.TotalMemoryBytes < 6.5 * Gigabyte)
        {
            return new DeviceProfile(DeviceClass.Standard, AnalyzeWholeSolution: true, Math.Clamp(device.ProcessorCount / 2, 1, 4), TimeSpan.FromMilliseconds(350), 1_000_000);
        }

        return new DeviceProfile(DeviceClass.Large, AnalyzeWholeSolution: true, Math.Clamp(device.ProcessorCount - 2, 2, 8), TimeSpan.FromMilliseconds(250), 2_000_000);
    }
}
