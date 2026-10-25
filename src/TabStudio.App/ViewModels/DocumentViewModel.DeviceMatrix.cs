using System.Text;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;

namespace TabStudio.App.ViewModels;

/// <summary>The Device Matrix's tab: a document page with every screen the app runs on, rather than a file.</summary>
public sealed partial class DocumentViewModel
{
    /// <summary>The tab badge of the Device Matrix.</summary>
    private static readonly FileType DeviceMatrixFileType = new("Device Matrix", "DM", BadgeKind.Markup, null, null, null);

    private DocumentViewModel(DeviceMatrixViewModel matrix, string tabPath, DeviceProfile profile)
        : this(tabPath, new TextFileContent(string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)), language: null, profile)
    {
        DeviceMatrix = matrix;
        FileType = DeviceMatrixFileType;
    }

    /// <summary>For the Device Matrix's tab, the matrix; <see langword="null"/> for other documents.</summary>
    public DeviceMatrixViewModel? DeviceMatrix { get; }

    /// <summary>Whether this is the Device Matrix's tab.</summary>
    public bool IsDeviceMatrix => DeviceMatrix is not null;

    /// <summary>Creates the Device Matrix's tab. Call on the UI thread.</summary>
    /// <param name="matrix">The matrix, which the workspace owns.</param>
    /// <param name="tabPath">A path for the tab that names no file on disk, so sessions do not reopen it.</param>
    /// <param name="profile">The device profile.</param>
    public static DocumentViewModel ForDeviceMatrix(DeviceMatrixViewModel matrix, string tabPath, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentException.ThrowIfNullOrEmpty(tabPath);
        ArgumentNullException.ThrowIfNull(profile);
        return new DocumentViewModel(matrix, tabPath, profile);
    }
}
