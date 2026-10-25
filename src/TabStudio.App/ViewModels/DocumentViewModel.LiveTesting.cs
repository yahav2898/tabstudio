using AvaloniaEdit.Document;
using TabStudio.App.Editing;
using TabStudio.LiveTesting.Results;

namespace TabStudio.App.ViewModels;

/// <summary>What Live Unit Testing shows in a document: a marker beside every line with code.</summary>
public sealed partial class DocumentViewModel
{
    private bool _tracksLiveTestMarkers;

    /// <summary>The Live Unit Testing markers, or <see langword="null"/> while Live Unit Testing is off or the file has no code under test.</summary>
    public LiveTestMarkers? LiveTestMarkers { get; private set; }

    /// <summary>What the markers' popup asks of Live Unit Testing, such as running a test. Set by the workspace.</summary>
    public ILiveTestingEditor? LiveTesting { get; set; }

    /// <summary>Raised on the UI thread when the markers are shown, updated or removed.</summary>
    public event EventHandler? LiveTestMarkersChanged;

    /// <summary>Shows the coverage of this file, mapped onto the text as it is now, or removes the markers.</summary>
    /// <param name="coverage">The coverage of the file, or <see langword="null"/> to remove the markers.</param>
    public void ShowLiveTestCoverage(FileCoverage? coverage)
    {
        if (coverage is null)
        {
            if (LiveTestMarkers is null)
            {
                return;
            }

            LiveTestMarkers = null;
        }
        else
        {
            LiveTestMarkers ??= new LiveTestMarkers();
            LiveTestMarkers.Update(Document, coverage);
            if (!_tracksLiveTestMarkers)
            {
                // The markers follow every edit, so they stay on their lines while the user types.
                Document.Changed += OnDocumentChangedForLiveTesting;
                _tracksLiveTestMarkers = true;
            }
        }

        LiveTestMarkersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentChangedForLiveTesting(object? sender, DocumentChangeEventArgs e) => LiveTestMarkers?.Apply(e);
}
