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

    /// <summary>
    /// The browser head of a cross-platform app compiled ahead of time or with native code to WebAssembly
    /// (<c>RunAOTCompilation</c>, <c>WasmBuildNative</c>), which .NET's <c>wasm-tools</c> workload builds.
    /// </summary>
    WebAssemblyTools,

    /// <summary>
    /// A project only for a .NET newer than the .NET 10 TabStudio compiles with, such as <c>net11.0</c>, which needs that
    /// .NET's SDK, as the .NET 10 SDK reports with <c>NETSDK1045</c>.
    /// </summary>
    NewerDotNet,
}
