namespace System.Drawing.Imaging;

/// <summary>How the pixels of a bitmap are locked: for reading, writing or both.</summary>
[Flags]
public enum ImageLockMode
{
    /// <summary>The pixels are read.</summary>
    ReadOnly = 1,

    /// <summary>The pixels are written.</summary>
    WriteOnly = 2,

    /// <summary>The pixels are read and written.</summary>
    ReadWrite = ReadOnly | WriteOnly,

    /// <summary>The caller's memory holds the pixels.</summary>
    UserInputBuffer = 4,
}
