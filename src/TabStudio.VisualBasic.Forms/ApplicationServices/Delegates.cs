using System.ComponentModel;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>Handles <see cref="WindowsFormsApplicationBase.ApplyApplicationDefaults"/>.</summary>
/// <param name="sender">The application.</param>
/// <param name="e">The defaults the handler may change.</param>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public delegate void ApplyApplicationDefaultsEventHandler(object sender, ApplyApplicationDefaultsEventArgs e);

/// <summary>Handles <see cref="WindowsFormsApplicationBase.Shutdown"/>.</summary>
/// <param name="sender">The application.</param>
/// <param name="e">No data.</param>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public delegate void ShutdownEventHandler(object sender, EventArgs e);

/// <summary>Handles <see cref="WindowsFormsApplicationBase.Startup"/>.</summary>
/// <param name="sender">The application.</param>
/// <param name="e">The command line; setting Cancel ends the app before its main form is created.</param>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public delegate void StartupEventHandler(object sender, StartupEventArgs e);

/// <summary>Handles <see cref="WindowsFormsApplicationBase.StartupNextInstance"/>.</summary>
/// <param name="sender">The application.</param>
/// <param name="e">The command line of the instance that started.</param>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public delegate void StartupNextInstanceEventHandler(object sender, StartupNextInstanceEventArgs e);

/// <summary>Handles <see cref="WindowsFormsApplicationBase.UnhandledException"/>.</summary>
/// <param name="sender">The application.</param>
/// <param name="e">The exception, and whether the app ends.</param>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public delegate void UnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e);
