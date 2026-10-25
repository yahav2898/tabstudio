using System.Text;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels.Actions;

namespace TabStudio.App.ViewModels;

/// <summary>Job logs from GitHub Actions in read-only editor tabs, so Find, selection and copying work as in any file.</summary>
public sealed partial class DocumentViewModel
{
    private DocumentViewModel(CiLogViewModel log, string tabPath, DeviceProfile profile)
        : this(tabPath, new TextFileContent(string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)), language: null, profile)
    {
        CiLog = log;
        Document.UndoStack.SizeLimit = 0;
        log.LogChanged += OnCiLogChanged;
    }

    /// <summary>Raised after the tab's text became the job's log, newly downloaded or shown differently.</summary>
    public event EventHandler? CiLogTextChanged;

    /// <summary>For a job's log tab, the log; <see langword="null"/> for a file.</summary>
    public CiLogViewModel? CiLog { get; }

    /// <summary>Whether this is a read-only tab with a job's log rather than a file.</summary>
    public bool IsCiLog => CiLog is not null;

    /// <summary>Creates a read-only tab that shows a job's log. Call on the UI thread.</summary>
    /// <param name="log">The log.</param>
    /// <param name="tabPath">The path the tab is known by; it never exists on disk, so sessions do not reopen it.</param>
    /// <param name="profile">The device profile.</param>
    public static DocumentViewModel ForCiLog(CiLogViewModel log, string tabPath, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(tabPath);
        ArgumentNullException.ThrowIfNull(profile);
        return new DocumentViewModel(log, tabPath, profile);
    }

    private void OnCiLogChanged(object? sender, EventArgs e)
    {
        if (_disposed || CiLog?.Log is not { } log)
        {
            return;
        }

        Document.Text = log.Text;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        CiLogTextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops following the log when the tab closes.</summary>
    private void DisposeCiLog()
    {
        if (CiLog is { } log)
        {
            log.LogChanged -= OnCiLogChanged;
            log.Dispose();
        }
    }
}
