using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>A dialog shown with <see cref="ShowDialog()"/>, which returns whether the user confirmed it.</summary>
public abstract class CommonDialog : Component
{
    private static readonly object EventHelpRequest = new();

    /// <summary>Creates a dialog.</summary>
    protected CommonDialog()
    {
    }

    /// <summary>Occurs when the user asks for help.</summary>
    public event EventHandler? HelpRequest
    {
        add => Events.AddHandler(EventHelpRequest, value);
        remove => Events.RemoveHandler(EventHelpRequest, value);
    }

    /// <summary>Data about the dialog.</summary>
    public object? Tag { get; set; }

    /// <summary>Shows the dialog and waits for the user.</summary>
    /// <returns>OK when the user confirmed it, Cancel otherwise.</returns>
    public DialogResult ShowDialog() => ShowDialog(null);

    /// <summary>Shows the dialog over a window and waits for the user.</summary>
    /// <param name="owner">The window.</param>
    /// <returns>OK when the user confirmed it, Cancel otherwise.</returns>
    public DialogResult ShowDialog(IWin32Window? owner) => RunDialog(owner?.Handle ?? IntPtr.Zero) ? DialogResult.OK : DialogResult.Cancel;

    /// <summary>Restores the dialog's settings to their defaults.</summary>
    public abstract void Reset();

    /// <summary>Raises HelpRequest.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnHelpRequest(EventArgs e) => ((EventHandler?)Events[EventHelpRequest])?.Invoke(this, e);

    /// <summary>Shows the dialog.</summary>
    /// <param name="hwndOwner">The owner window's handle.</param>
    /// <returns>Whether the user confirmed it.</returns>
    protected abstract bool RunDialog(IntPtr hwndOwner);

    /// <summary>Waits on the app's UI thread, with its message loop running, for a task.</summary>
    private protected static T Wait<T>(Task<T> task)
    {
        if (FormsRuntime.Current is { IsOnThread: true } runtime)
        {
            runtime.RunLoop(() => task.IsCompleted);
            runtime.ThrowIfCrashed();
        }

        return task.IsCompletedSuccessfully ? task.Result : default!;
    }
}

/// <summary>
/// A dialog to pick a file. In TabStudio it opens the device's storage picker; the app gets a path it can read or
/// write as on Windows, and a saved file is written back to where the user picked.
/// </summary>
[DefaultEvent(nameof(FileOk))]
[DefaultProperty(nameof(FileName))]
public abstract class FileDialog : CommonDialog
{
    private static readonly object EventFileOk = new();
    private string[] _fileNames = [];

    /// <summary>Creates a file dialog.</summary>
    private protected FileDialog()
    {
    }

    /// <summary>Occurs when the user picked a file; handlers can cancel it.</summary>
    public event CancelEventHandler? FileOk
    {
        add => Events.AddHandler(EventFileOk, value);
        remove => Events.RemoveHandler(EventFileOk, value);
    }

    /// <summary>Whether the default extension is added to a name without one.</summary>
    public bool AddExtension { get; set; } = true;

    /// <summary>Whether the file must exist.</summary>
    public virtual bool CheckFileExists { get; set; }

    /// <summary>Whether the folder must exist.</summary>
    public bool CheckPathExists { get; set; } = true;

    /// <summary>The extension added to a name without one, such as "txt".</summary>
    [AllowNull]
    public string DefaultExt
    {
        get => field ?? string.Empty;
        set => field = value?.TrimStart('.');
    }

    /// <summary>Whether shortcuts are followed. Kept for compatibility.</summary>
    public bool DereferenceLinks { get; set; } = true;

    /// <summary>The picked file's path, or the suggested name.</summary>
    [AllowNull]
    public string FileName
    {
        get => _fileNames.Length > 0 ? _fileNames[0] : string.Empty;
        set => _fileNames = string.IsNullOrEmpty(value) ? [] : [value];
    }

    /// <summary>The picked files' paths.</summary>
    public string[] FileNames => [.. _fileNames];

    /// <summary>The file types the dialog offers, as "Text files|*.txt|All files|*.*".</summary>
    [AllowNull]
    public string Filter
    {
        get => field ?? string.Empty;
        set
        {
            if (!string.IsNullOrEmpty(value) && value.Split('|').Length % 2 != 0)
            {
                throw new ArgumentException("Filter string you provided is not valid. The filter string must contain a description of the filter, followed by the vertical bar (|) and the filter pattern.", nameof(value));
            }

            field = value;
        }
    }

    /// <summary>The file type selected first, from 1.</summary>
    public int FilterIndex { get; set; } = 1;

    /// <summary>The folder the dialog starts in.</summary>
    [AllowNull]
    public string InitialDirectory
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>Whether the current folder is restored after the dialog. Kept for compatibility.</summary>
    public bool RestoreDirectory { get; set; }

    /// <summary>Whether a help button shows. Kept for compatibility.</summary>
    public bool ShowHelp { get; set; }

    /// <summary>Whether extensions with several dots are recognized. Kept for compatibility.</summary>
    public bool SupportMultiDottedExtensions { get; set; }

    /// <summary>The dialog's title.</summary>
    [AllowNull]
    public string Title
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>Whether names are checked for valid characters. Kept for compatibility.</summary>
    public bool ValidateNames { get; set; } = true;

    /// <summary>Whether the Vista-style dialog is used. Kept for compatibility.</summary>
    public bool AutoUpgradeEnabled { get; set; } = true;

    /// <summary>The identifier of the dialog's remembered state. Kept for compatibility.</summary>
    public Guid? ClientGuid { get; set; }

    /// <summary>Whether the dialog is for saving.</summary>
    private protected abstract bool IsSave { get; }

    /// <summary>Whether several files can be picked.</summary>
    private protected virtual bool AllowsMultiple => false;

    /// <inheritdoc />
    public override void Reset()
    {
        _fileNames = [];
        Filter = null;
        FilterIndex = 1;
        InitialDirectory = null;
        Title = null;
        DefaultExt = null;
        AddExtension = true;
        CheckPathExists = true;
    }

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()}: Title: {Title}, FileName: {FileName}";

    /// <summary>Raises FileOk.</summary>
    /// <param name="e">The event data.</param>
    protected void OnFileOk(CancelEventArgs e) => ((CancelEventHandler?)Events[EventFileOk])?.Invoke(this, e);

    /// <inheritdoc />
    protected override bool RunDialog(IntPtr hwndOwner)
    {
        if (FormsRuntime.Current is not { } runtime)
        {
            throw new InvalidOperationException("A file dialog can only be shown while the app runs.");
        }

        var suggested = Path.GetFileName(FileName);
        if (IsSave && suggested.Length > 0 && AddExtension && Path.GetExtension(suggested).Length == 0 && DefaultExt.Length > 0)
        {
            suggested += "." + DefaultExt;
        }

        var request = new FileRequest(IsSave, Title, Filter, FilterIndex, suggested, AllowsMultiple, Folder: false, InitialDirectory);
        var picked = Wait(runtime.Presenter.PickFilesAsync(request));
        if (picked is not { Length: > 0 })
        {
            return false;
        }

        var previous = _fileNames;
        _fileNames = picked;
        var ok = new CancelEventArgs();
        OnFileOk(ok);
        if (ok.Cancel)
        {
            _fileNames = previous;
            return false;
        }

        return true;
    }
}

/// <summary>A dialog to pick files to open.</summary>
public sealed class OpenFileDialog : FileDialog
{
    /// <summary>Creates an open file dialog.</summary>
    public OpenFileDialog()
    {
        CheckFileExists = true;
    }

    /// <summary>Whether several files can be picked.</summary>
    public bool Multiselect { get; set; }

    /// <summary>Whether the read-only check box is checked. Kept for compatibility.</summary>
    public bool ReadOnlyChecked { get; set; }

    /// <summary>Whether a read-only check box shows. Kept for compatibility.</summary>
    public bool ShowReadOnly { get; set; }

    /// <summary>Whether a preview pane shows. Kept for compatibility.</summary>
    public bool ShowPreview { get; set; }

    /// <summary>The picked file's name, without its folder.</summary>
    public string SafeFileName => Path.GetFileName(FileName);

    /// <summary>The picked files' names, without their folders.</summary>
    public string[] SafeFileNames => [.. FileNames.Select(Path.GetFileName).OfType<string>()];

    /// <inheritdoc />
    private protected override bool IsSave => false;

    /// <inheritdoc />
    private protected override bool AllowsMultiple => Multiselect;

    /// <summary>Opens the picked file to read.</summary>
    public Stream OpenFile()
    {
        if (FileName.Length == 0)
        {
            throw new ArgumentNullException(nameof(FileName), "FileName cannot be empty.");
        }

        return new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}

/// <summary>A dialog to pick where to save a file.</summary>
public sealed class SaveFileDialog : FileDialog
{
    /// <summary>Creates a save file dialog.</summary>
    public SaveFileDialog()
    {
    }

    /// <summary>Whether the user is asked to create a file that does not exist. Kept for compatibility.</summary>
    public bool CreatePrompt { get; set; }

    /// <summary>Whether the user is asked to replace a file that exists. The device's picker asks.</summary>
    public bool OverwritePrompt { get; set; } = true;

    /// <inheritdoc />
    private protected override bool IsSave => true;

    /// <summary>Opens the picked file to write, replacing what it has.</summary>
    public Stream OpenFile()
    {
        if (FileName.Length == 0)
        {
            throw new ArgumentNullException(nameof(FileName), "FileName cannot be empty.");
        }

        return new FileStream(FileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    }
}

/// <summary>A dialog to pick a folder, through the device's storage picker.</summary>
[DefaultProperty(nameof(SelectedPath))]
public sealed class FolderBrowserDialog : CommonDialog
{
    /// <summary>Creates a folder dialog.</summary>
    public FolderBrowserDialog()
    {
    }

    /// <summary>The picked folder.</summary>
    [AllowNull]
    public string SelectedPath
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>The text shown above the folders.</summary>
    [AllowNull]
    public string Description
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>Whether the description is the dialog's title.</summary>
    public bool UseDescriptionForTitle { get; set; }

    /// <summary>Whether a New Folder button shows. Kept for compatibility.</summary>
    public bool ShowNewFolderButton { get; set; } = true;

    /// <summary>The folder the dialog starts in.</summary>
    [AllowNull]
    public string InitialDirectory
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>The root folder. Kept for compatibility.</summary>
    public Environment.SpecialFolder RootFolder { get; set; } = Environment.SpecialFolder.Desktop;

    /// <inheritdoc />
    public override void Reset()
    {
        SelectedPath = null;
        Description = null;
        InitialDirectory = null;
    }

    /// <inheritdoc />
    protected override bool RunDialog(IntPtr hwndOwner)
    {
        if (FormsRuntime.Current is not { } runtime)
        {
            throw new InvalidOperationException("A folder dialog can only be shown while the app runs.");
        }

        var request = new FileRequest(Save: false, Description, string.Empty, 1, string.Empty, Multiselect: false, Folder: true, InitialDirectory.Length > 0 ? InitialDirectory : SelectedPath);
        var picked = Wait(runtime.Presenter.PickFilesAsync(request));
        if (picked is not { Length: > 0 })
        {
            return false;
        }

        SelectedPath = picked[0];
        return true;
    }
}
