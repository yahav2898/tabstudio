using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using VbFileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

namespace Microsoft.VisualBasic.MyServices;

/// <summary>
/// The device's files and folders (<c>My.Computer.FileSystem</c>), through
/// <c>Microsoft.VisualBasic.FileIO.FileSystem</c>. Paths are the device's: the project's folder, the app's data
/// folders and the storage TabStudio may use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class FileSystemProxy
{
    private SpecialDirectoriesProxy? _specialDirectoriesProxy;

    internal FileSystemProxy()
    {
    }

    /// <summary>The current folder of the process.</summary>
    public string CurrentDirectory
    {
        get => VbFileSystem.CurrentDirectory;
        set => VbFileSystem.CurrentDirectory = value;
    }

    /// <summary>The device's drives: its mounted file systems.</summary>
    public ReadOnlyCollection<DriveInfo> Drives => VbFileSystem.Drives;

    /// <summary>Special folders, such as the temporary folder and the user's documents.</summary>
    public SpecialDirectoriesProxy SpecialDirectories => _specialDirectoriesProxy ??= new SpecialDirectoriesProxy();

    /// <summary>Combines a folder and a relative path.</summary>
    /// <param name="baseDirectory">The folder.</param>
    /// <param name="relativePath">The relative path.</param>
    public string CombinePath(string baseDirectory, string relativePath) => VbFileSystem.CombinePath(baseDirectory, relativePath);

    /// <summary>Copies a folder into a new one.</summary>
    /// <param name="sourceDirectoryName">The folder.</param>
    /// <param name="destinationDirectoryName">The copy.</param>
    public void CopyDirectory(string sourceDirectoryName, string destinationDirectoryName) =>
        VbFileSystem.CopyDirectory(sourceDirectoryName, destinationDirectoryName);

    /// <summary>Copies a folder.</summary>
    /// <param name="sourceDirectoryName">The folder.</param>
    /// <param name="destinationDirectoryName">The copy.</param>
    /// <param name="overwrite">Whether to replace existing files.</param>
    public void CopyDirectory(string sourceDirectoryName, string destinationDirectoryName, bool overwrite) =>
        VbFileSystem.CopyDirectory(sourceDirectoryName, destinationDirectoryName, overwrite);

    /// <summary>Copies a folder, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="sourceDirectoryName">The folder.</param>
    /// <param name="destinationDirectoryName">The copy.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    public void CopyDirectory(string sourceDirectoryName, string destinationDirectoryName, FileIO.UIOption showUI) =>
        VbFileSystem.CopyDirectory(sourceDirectoryName, destinationDirectoryName, showUI);

    /// <summary>Copies a folder, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="sourceDirectoryName">The folder.</param>
    /// <param name="destinationDirectoryName">The copy.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    /// <param name="onUserCancel">What cancelling does.</param>
    public void CopyDirectory(string sourceDirectoryName, string destinationDirectoryName, FileIO.UIOption showUI, FileIO.UICancelOption onUserCancel) =>
        VbFileSystem.CopyDirectory(sourceDirectoryName, destinationDirectoryName, showUI, onUserCancel);

    /// <summary>Copies a file to a new one.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="destinationFileName">The copy.</param>
    public void CopyFile(string sourceFileName, string destinationFileName) => VbFileSystem.CopyFile(sourceFileName, destinationFileName);

    /// <summary>Copies a file.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="destinationFileName">The copy.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    public void CopyFile(string sourceFileName, string destinationFileName, bool overwrite) =>
        VbFileSystem.CopyFile(sourceFileName, destinationFileName, overwrite);

    /// <summary>Copies a file, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="destinationFileName">The copy.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    public void CopyFile(string sourceFileName, string destinationFileName, FileIO.UIOption showUI) =>
        VbFileSystem.CopyFile(sourceFileName, destinationFileName, showUI);

    /// <summary>Copies a file, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="destinationFileName">The copy.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    /// <param name="onUserCancel">What cancelling does.</param>
    public void CopyFile(string sourceFileName, string destinationFileName, FileIO.UIOption showUI, FileIO.UICancelOption onUserCancel) =>
        VbFileSystem.CopyFile(sourceFileName, destinationFileName, showUI, onUserCancel);

    /// <summary>Creates a folder and its parents.</summary>
    /// <param name="directory">The folder.</param>
    public void CreateDirectory(string directory) => VbFileSystem.CreateDirectory(directory);

    /// <summary>Deletes a folder.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="onDirectoryNotEmpty">Whether to delete its contents or throw when it has some.</param>
    public void DeleteDirectory(string directory, FileIO.DeleteDirectoryOption onDirectoryNotEmpty) =>
        VbFileSystem.DeleteDirectory(directory, onDirectoryNotEmpty);

    /// <summary>Deletes a folder, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    /// <param name="recycle">Whether to send it to the recycle bin, which the device does not have.</param>
    public void DeleteDirectory(string directory, FileIO.UIOption showUI, FileIO.RecycleOption recycle) =>
        VbFileSystem.DeleteDirectory(directory, showUI, recycle);

    /// <summary>Deletes a folder, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    /// <param name="recycle">Whether to send it to the recycle bin, which the device does not have.</param>
    /// <param name="onUserCancel">What cancelling does.</param>
    public void DeleteDirectory(string directory, FileIO.UIOption showUI, FileIO.RecycleOption recycle, FileIO.UICancelOption onUserCancel) =>
        VbFileSystem.DeleteDirectory(directory, showUI, recycle, onUserCancel);

    /// <summary>Deletes a file.</summary>
    /// <param name="file">The file.</param>
    public void DeleteFile(string file) => VbFileSystem.DeleteFile(file);

    /// <summary>Deletes a file, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="file">The file.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    /// <param name="recycle">Whether to send it to the recycle bin, which the device does not have.</param>
    public void DeleteFile(string file, FileIO.UIOption showUI, FileIO.RecycleOption recycle) => VbFileSystem.DeleteFile(file, showUI, recycle);

    /// <summary>Deletes a file, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="file">The file.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    /// <param name="recycle">Whether to send it to the recycle bin, which the device does not have.</param>
    /// <param name="onUserCancel">What cancelling does.</param>
    public void DeleteFile(string file, FileIO.UIOption showUI, FileIO.RecycleOption recycle, FileIO.UICancelOption onUserCancel) =>
        VbFileSystem.DeleteFile(file, showUI, recycle, onUserCancel);

    /// <summary>Whether a folder exists.</summary>
    /// <param name="directory">The folder.</param>
    public bool DirectoryExists(string directory) => VbFileSystem.DirectoryExists(directory);

    /// <summary>Whether a file exists.</summary>
    /// <param name="file">The file.</param>
    public bool FileExists(string file) => VbFileSystem.FileExists(file);

    /// <summary>The files of a folder that contain a text.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="containsText">The text.</param>
    /// <param name="ignoreCase">Whether case is ignored.</param>
    /// <param name="searchType">Whether subfolders are searched.</param>
    public ReadOnlyCollection<string> FindInFiles(string directory, string containsText, bool ignoreCase, FileIO.SearchOption searchType) =>
        VbFileSystem.FindInFiles(directory, containsText, ignoreCase, searchType);

    /// <summary>The files of a folder whose names match wildcards and that contain a text.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="containsText">The text.</param>
    /// <param name="ignoreCase">Whether case is ignored.</param>
    /// <param name="searchType">Whether subfolders are searched.</param>
    /// <param name="fileWildcards">The wildcards, such as <c>*.txt</c>.</param>
    public ReadOnlyCollection<string> FindInFiles(string directory, string containsText, bool ignoreCase, FileIO.SearchOption searchType, params string[] fileWildcards) =>
        VbFileSystem.FindInFiles(directory, containsText, ignoreCase, searchType, fileWildcards);

    /// <summary>The folders in a folder.</summary>
    /// <param name="directory">The folder.</param>
    public ReadOnlyCollection<string> GetDirectories(string directory) => VbFileSystem.GetDirectories(directory);

    /// <summary>The folders in a folder whose names match wildcards.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="searchType">Whether subfolders are searched.</param>
    /// <param name="wildcards">The wildcards.</param>
    public ReadOnlyCollection<string> GetDirectories(string directory, FileIO.SearchOption searchType, params string[] wildcards) =>
        VbFileSystem.GetDirectories(directory, searchType, wildcards);

    /// <summary>A folder's information.</summary>
    /// <param name="directory">The folder.</param>
    public DirectoryInfo GetDirectoryInfo(string directory) => VbFileSystem.GetDirectoryInfo(directory);

    /// <summary>A drive's information.</summary>
    /// <param name="drive">The drive.</param>
    public DriveInfo GetDriveInfo(string drive) => VbFileSystem.GetDriveInfo(drive);

    /// <summary>A file's information.</summary>
    /// <param name="file">The file.</param>
    public FileInfo GetFileInfo(string file) => VbFileSystem.GetFileInfo(file);

    /// <summary>The files in a folder.</summary>
    /// <param name="directory">The folder.</param>
    public ReadOnlyCollection<string> GetFiles(string directory) => VbFileSystem.GetFiles(directory);

    /// <summary>The files in a folder whose names match wildcards.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="searchType">Whether subfolders are searched.</param>
    /// <param name="wildcards">The wildcards, such as <c>*.txt</c>.</param>
    public ReadOnlyCollection<string> GetFiles(string directory, FileIO.SearchOption searchType, params string[] wildcards) =>
        VbFileSystem.GetFiles(directory, searchType, wildcards);

    /// <summary>The name of a file or folder, without its folder.</summary>
    /// <param name="path">The path.</param>
    public string GetName(string path) => VbFileSystem.GetName(path);

    /// <summary>The folder of a path.</summary>
    /// <param name="path">The path.</param>
    public string GetParentPath(string path) => VbFileSystem.GetParentPath(path);

    /// <summary>Creates an empty temporary file and returns its path.</summary>
    public string GetTempFileName() => VbFileSystem.GetTempFileName();

    /// <summary>Moves a folder to a new place.</summary>
    /// <param name="sourceDirectoryName">The folder.</param>
    /// <param name="destinationDirectoryName">The new place.</param>
    public void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName) =>
        VbFileSystem.MoveDirectory(sourceDirectoryName, destinationDirectoryName);

    /// <summary>Moves a folder.</summary>
    /// <param name="sourceDirectoryName">The folder.</param>
    /// <param name="destinationDirectoryName">The new place.</param>
    /// <param name="overwrite">Whether to replace existing files.</param>
    public void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName, bool overwrite) =>
        VbFileSystem.MoveDirectory(sourceDirectoryName, destinationDirectoryName, overwrite);

    /// <summary>Moves a folder, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="sourceDirectoryName">The folder.</param>
    /// <param name="destinationDirectoryName">The new place.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    public void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName, FileIO.UIOption showUI) =>
        VbFileSystem.MoveDirectory(sourceDirectoryName, destinationDirectoryName, showUI);

    /// <summary>Moves a folder, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="sourceDirectoryName">The folder.</param>
    /// <param name="destinationDirectoryName">The new place.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    /// <param name="onUserCancel">What cancelling does.</param>
    public void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName, FileIO.UIOption showUI, FileIO.UICancelOption onUserCancel) =>
        VbFileSystem.MoveDirectory(sourceDirectoryName, destinationDirectoryName, showUI, onUserCancel);

    /// <summary>Moves a file to a new place.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="destinationFileName">The new place.</param>
    public void MoveFile(string sourceFileName, string destinationFileName) => VbFileSystem.MoveFile(sourceFileName, destinationFileName);

    /// <summary>Moves a file.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="destinationFileName">The new place.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    public void MoveFile(string sourceFileName, string destinationFileName, bool overwrite) =>
        VbFileSystem.MoveFile(sourceFileName, destinationFileName, overwrite);

    /// <summary>Moves a file, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="destinationFileName">The new place.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    public void MoveFile(string sourceFileName, string destinationFileName, FileIO.UIOption showUI) =>
        VbFileSystem.MoveFile(sourceFileName, destinationFileName, showUI);

    /// <summary>Moves a file, with Windows' dialogs where .NET shows them.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="destinationFileName">The new place.</param>
    /// <param name="showUI">Which dialogs to show.</param>
    /// <param name="onUserCancel">What cancelling does.</param>
    public void MoveFile(string sourceFileName, string destinationFileName, FileIO.UIOption showUI, FileIO.UICancelOption onUserCancel) =>
        VbFileSystem.MoveFile(sourceFileName, destinationFileName, showUI, onUserCancel);

    /// <summary>Opens a delimited or fixed-width text file for reading field by field.</summary>
    /// <param name="file">The file.</param>
    public FileIO.TextFieldParser OpenTextFieldParser(string file) => VbFileSystem.OpenTextFieldParser(file);

    /// <summary>Opens a delimited text file for reading field by field.</summary>
    /// <param name="file">The file.</param>
    /// <param name="delimiters">The delimiters, such as a comma.</param>
    public FileIO.TextFieldParser OpenTextFieldParser(string file, params string[] delimiters) => VbFileSystem.OpenTextFieldParser(file, delimiters);

    /// <summary>Opens a fixed-width text file for reading field by field.</summary>
    /// <param name="file">The file.</param>
    /// <param name="fieldWidths">The fields' widths.</param>
    public FileIO.TextFieldParser OpenTextFieldParser(string file, params int[] fieldWidths) => VbFileSystem.OpenTextFieldParser(file, fieldWidths);

    /// <summary>Opens a text file for reading.</summary>
    /// <param name="file">The file.</param>
    public StreamReader OpenTextFileReader(string file) => VbFileSystem.OpenTextFileReader(file);

    /// <summary>Opens a text file for reading in an encoding.</summary>
    /// <param name="file">The file.</param>
    /// <param name="encoding">The encoding.</param>
    public StreamReader OpenTextFileReader(string file, Encoding encoding) => VbFileSystem.OpenTextFileReader(file, encoding);

    /// <summary>Opens a text file for writing.</summary>
    /// <param name="file">The file.</param>
    /// <param name="append">Whether to add to its end rather than replace it.</param>
    public StreamWriter OpenTextFileWriter(string file, bool append) => VbFileSystem.OpenTextFileWriter(file, append);

    /// <summary>Opens a text file for writing in an encoding.</summary>
    /// <param name="file">The file.</param>
    /// <param name="append">Whether to add to its end rather than replace it.</param>
    /// <param name="encoding">The encoding.</param>
    public StreamWriter OpenTextFileWriter(string file, bool append, Encoding encoding) => VbFileSystem.OpenTextFileWriter(file, append, encoding);

    /// <summary>Reads a file's bytes.</summary>
    /// <param name="file">The file.</param>
    public byte[] ReadAllBytes(string file) => VbFileSystem.ReadAllBytes(file);

    /// <summary>Reads a text file.</summary>
    /// <param name="file">The file.</param>
    public string ReadAllText(string file) => VbFileSystem.ReadAllText(file);

    /// <summary>Reads a text file in an encoding.</summary>
    /// <param name="file">The file.</param>
    /// <param name="encoding">The encoding.</param>
    public string ReadAllText(string file, Encoding encoding) => VbFileSystem.ReadAllText(file, encoding);

    /// <summary>Renames a folder.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="newName">Its new name, without a folder.</param>
    public void RenameDirectory(string directory, string newName) => VbFileSystem.RenameDirectory(directory, newName);

    /// <summary>Renames a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="newName">Its new name, without a folder.</param>
    public void RenameFile(string file, string newName) => VbFileSystem.RenameFile(file, newName);

    /// <summary>Writes bytes to a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="data">The bytes.</param>
    /// <param name="append">Whether to add them to its end rather than replace it.</param>
    public void WriteAllBytes(string file, byte[] data, bool append) => VbFileSystem.WriteAllBytes(file, data, append);

    /// <summary>Writes text to a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">The text.</param>
    /// <param name="append">Whether to add it to its end rather than replace it.</param>
    public void WriteAllText(string file, string text, bool append) => VbFileSystem.WriteAllText(file, text, append);

    /// <summary>Writes text to a file in an encoding.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">The text.</param>
    /// <param name="append">Whether to add it to its end rather than replace it.</param>
    /// <param name="encoding">The encoding.</param>
    public void WriteAllText(string file, string text, bool append, Encoding encoding) => VbFileSystem.WriteAllText(file, text, append, encoding);
}
