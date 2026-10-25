namespace TabStudio.App.ViewModels;

/// <summary>Analyzing a document again when its project changes while its text does not.</summary>
public sealed partial class DocumentViewModel
{
    /// <summary>
    /// Runs classification and diagnostics again now, cancelling any analysis in progress: for when the document's
    /// project changed under it, such as after a package restore, new source generators, a reload or another build
    /// configuration. An analysis that started before the change sees the project as it was (a Blazor app's
    /// <c>Program.cs</c> without ASP.NET Core, until its restore), so unlike <see cref="AnalyzeNowAsync"/> this never
    /// reuses it.
    /// </summary>
    public Task AnalyzeAgainAsync()
    {
        if (_disposed || _language is null)
        {
            return Task.CompletedTask;
        }

        var token = RestartAnalysis();
        _analysisEdits = _edits;
        _analysisTask = AnalyzeAsync(TimeSpan.Zero, token);
        return _analysisTask;
    }
}
