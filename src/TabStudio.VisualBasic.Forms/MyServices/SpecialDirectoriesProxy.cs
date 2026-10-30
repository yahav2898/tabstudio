using System.ComponentModel;
using Microsoft.VisualBasic.FileIO;

namespace Microsoft.VisualBasic.MyServices;

/// <summary>
/// Special folders (<c>My.Computer.FileSystem.SpecialDirectories</c>), as <c>Microsoft.VisualBasic.FileIO.SpecialDirectories</c>
/// finds them on the device. A folder the device does not have throws <see cref="DirectoryNotFoundException"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class SpecialDirectoriesProxy
{
    internal SpecialDirectoriesProxy()
    {
    }

    /// <summary>The application data folder shared by every user.</summary>
    public string AllUsersApplicationData => SpecialDirectories.AllUsersApplicationData;

    /// <summary>The current user's application data folder.</summary>
    public string CurrentUserApplicationData => SpecialDirectories.CurrentUserApplicationData;

    /// <summary>The desktop folder.</summary>
    public string Desktop => SpecialDirectories.Desktop;

    /// <summary>The documents folder.</summary>
    public string MyDocuments => SpecialDirectories.MyDocuments;

    /// <summary>The music folder.</summary>
    public string MyMusic => SpecialDirectories.MyMusic;

    /// <summary>The pictures folder.</summary>
    public string MyPictures => SpecialDirectories.MyPictures;

    /// <summary>The program files folder.</summary>
    public string ProgramFiles => SpecialDirectories.ProgramFiles;

    /// <summary>The start menu's programs folder.</summary>
    public string Programs => SpecialDirectories.Programs;

    /// <summary>The temporary folder.</summary>
    public string Temp => SpecialDirectories.Temp;
}
