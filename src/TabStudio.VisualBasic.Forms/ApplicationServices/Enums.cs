namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>How a Windows Forms app authenticates its user.</summary>
public enum AuthenticationMode
{
    /// <summary>The signed-in user: on the tablet, the device's user.</summary>
    Windows = 0,

    /// <summary>The app sets the principal itself.</summary>
    ApplicationDefined = 1,
}

/// <summary>When a Windows Forms app with the application framework ends.</summary>
public enum ShutdownMode
{
    /// <summary>When the main form closes.</summary>
    AfterMainFormCloses = 0,

    /// <summary>When the last open form closes.</summary>
    AfterAllFormsClose = 1,
}
