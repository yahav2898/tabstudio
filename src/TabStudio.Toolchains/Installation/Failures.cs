using TabStudio.Toolchains.Downloads;

namespace TabStudio.Toolchains.Installation;

/// <summary>Turns lower-level failures into the installer's own, keeping their messages.</summary>
internal static class Failures
{
    /// <summary>The installer's failure for a failed download.</summary>
    public static ToolchainInstallException From(DownloadException exception) => new(
        exception.Failure switch
        {
            DownloadFailure.NoNetwork => InstallFailure.NoNetwork,
            DownloadFailure.NoSpace => InstallFailure.NotEnoughSpace,
            DownloadFailure.IntegrityFailed => InstallFailure.IntegrityFailed,
            DownloadFailure.NotFound => InstallFailure.PackageNotFound,
            _ => InstallFailure.ServerError,
        },
        exception.Message,
        innerException: exception);
}
