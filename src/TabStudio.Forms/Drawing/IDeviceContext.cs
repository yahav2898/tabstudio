namespace System.Drawing;

/// <summary>A surface text can be drawn on with TextRenderer: a Graphics.</summary>
public interface IDeviceContext : IDisposable
{
    /// <summary>The GDI device context. Not available on this device.</summary>
    IntPtr GetHdc();

    /// <summary>Releases the GDI device context. Not available on this device.</summary>
    void ReleaseHdc();
}
