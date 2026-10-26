namespace TabStudio.App.Platform;

/// <summary>Installs a user-built APK with Android's system confirmation and launches its main activity.</summary>
public interface IAndroidAppInstaller
{
    /// <summary>Installs, waits for the result, launches, and returns the installed package name.</summary>
    Task<string> InstallAndLaunchAsync(string apk, CancellationToken cancellationToken);
}
