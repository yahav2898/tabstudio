namespace TabStudio.App.ViewModels;

/// <summary>Starting the background analysis of the whole solution, one at a time.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>Cancels the whole-solution analysis in progress, when a newer one starts.</summary>
    private CancellationTokenSource? _solutionAnalysis;

    /// <summary>
    /// Analyzes every project in the background for the Error List, cancelling an analysis already running. That one
    /// saw the solution as it was when it started, such as before a package restore gave a Blazor app ASP.NET Core,
    /// and would replace the newer results with its errors if it finished last.
    /// </summary>
    private void StartSolutionAnalysis()
    {
        if (_background is not { } background)
        {
            return;
        }

        _solutionAnalysis?.Cancel();
        _solutionAnalysis?.Dispose();
        Operations.Find(AnalysisOperation)?.Complete(global::TabStudio.Core.Progress.OperationOutcome.Cancelled);
        _solutionAnalysis = CancellationTokenSource.CreateLinkedTokenSource(background.Token);
        _ = AnalyzeSolutionAsync(_solutionAnalysis.Token);
    }
}
