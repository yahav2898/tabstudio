namespace TabStudio.Core.Workspaces;

/// <summary>
/// A platform workload or toolset a project needs to build, which TabStudio does not have on a tablet (see
/// <see cref="ProjectClassifier.RequiredWorkload"/>).
/// </summary>
public enum PlatformWorkload
{
    /// <summary>The project needs none: TabStudio builds it as it is.</summary>
    None,

    /// <summary>A .NET MAUI project (<c>UseMaui</c>), which .NET's MAUI workload builds.</summary>
    Maui,

    /// <summary>
    /// The Android, iOS, Mac or Tizen head of a cross-platform app (<c>net10.0-android</c>, <c>net10.0-ios</c>), which
    /// .NET's workload for that platform builds.
    /// </summary>
    MobilePlatform,

    /// <summary>A Xamarin.iOS or Xamarin.Android project, which Xamarin's tools in Visual Studio build.</summary>
    Xamarin,

    /// <summary>A Universal Windows Platform app, which Visual Studio's UWP tools build, on Windows.</summary>
    Uwp,
}
