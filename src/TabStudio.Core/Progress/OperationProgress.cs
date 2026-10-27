using System.Globalization;

namespace TabStudio.Core.Progress;

/// <summary>
/// Where a long operation stands, as the status bar and the operations panel say it: its stage and, when they are
/// known, how much of how much in what unit, such as "Downloading Pico SDK 120/340 MB" or "Building 37/120".
/// </summary>
/// <param name="Stage">What is happening, such as "Downloading Pico SDK", "Restoring NuGet packages" or "Configuring CMake".</param>
/// <param name="Completed">How much is done, in <paramref name="Unit"/>; <see langword="null"/> when not known.</param>
/// <param name="Total">How much there is in all; <see langword="null"/> when not known.</param>
/// <param name="Unit">The unit of the numbers: <see cref="ProgressUnit.Bytes"/> show as MB, the others as counts.</param>
public sealed record OperationProgress(string Stage, double? Completed = null, double? Total = null, ProgressUnit Unit = ProgressUnit.Items)
{
    /// <summary>How much is done, from 0 to 1, or <see langword="null"/> when there are no numbers.</summary>
    public double? Fraction => Completed is { } done && Total is { } total && total > 0 ? Math.Clamp(done / total, 0, 1) : null;

    /// <summary>"Downloading Pico SDK 120/340 MB", "Installing packages 12/40", "Building 37/120 projects", or the stage alone.</summary>
    public string Text => Numbers is { } numbers ? $"{Stage} {numbers}" : Stage;

    /// <summary>"120/340 MB", "12/40", "37/120 projects", "37%", or <see langword="null"/> without numbers.</summary>
    public string? Numbers
    {
        get
        {
            if (Completed is not { } done || Total is not { } total || total <= 0)
            {
                return null;
            }

            return Unit switch
            {
                ProgressUnit.Bytes => FormatBytes(done, total),
                ProgressUnit.Percent => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(done * 100 / total):0}%"),
                ProgressUnit.Projects => string.Create(CultureInfo.InvariantCulture, $"{done:0}/{total:0} projects"),
                _ => string.Create(CultureInfo.InvariantCulture, $"{done:0}/{total:0}"),
            };
        }
    }

    /// <summary>Megabytes for anything over a megabyte, kilobytes below, the two numbers in the same unit.</summary>
    private static string FormatBytes(double done, double total)
    {
        const double MB = 1024 * 1024;
        if (total >= MB)
        {
            var precision = total >= 10 * MB ? "0" : "0.0";
            return string.Create(CultureInfo.InvariantCulture, $"{(done / MB).ToString(precision, CultureInfo.InvariantCulture)}/{(total / MB).ToString(precision, CultureInfo.InvariantCulture)} MB");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{Math.Ceiling(done / 1024):0}/{Math.Ceiling(total / 1024):0} KB");
    }
}

/// <summary>The unit of an <see cref="OperationProgress"/>'s numbers.</summary>
public enum ProgressUnit
{
    /// <summary>Things counted one by one: packages, build steps, files.</summary>
    Items,

    /// <summary>Projects, which the text names: "Analyzing 12/40 projects".</summary>
    Projects,

    /// <summary>Bytes, shown as MB or KB.</summary>
    Bytes,

    /// <summary>A percentage that the tool itself reports, such as CMake's Makefiles' <c>[ 37%]</c>.</summary>
    Percent,
}
