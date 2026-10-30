using System.Text;
using TabStudio.App.Platform;
using TabStudio.App.ViewModels.LongOperations;
using TabStudio.Core.Progress;
using TabStudio.Core.Workspaces;
using TabStudio.Toolchains.Installation;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Long operations in the status bar and its panel (<see cref="OperationsViewModel"/>): toolchain and SDK installs from
/// the Toolchain Store's progress, the project setup, NuGet restore, loading and analyzing C#, and builds with the
/// numbers their tools print. While packages restore or the solution is analyzed, the Error List says "Analyzing…"
/// rather than counts that are about to change; the projects of open documents are analyzed first.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>The key of the C# language service's loading.</summary>
    private const string LanguageOperation = "language";

    /// <summary>The key of the NuGet restore.</summary>
    private const string RestoreOperation = "restore";

    /// <summary>The key of the solution's analysis.</summary>
    private const string AnalysisOperation = "analysis";

    /// <summary>The key of the project setup.</summary>
    private const string SetupOperation = "setup";

    /// <summary>The long operations running now, for the status bar and its panel.</summary>
    public OperationsViewModel Operations { get; private set; } = null!;

    /// <summary>Creates the operations. Called once by the constructor, after the Actions window made the app's activity.</summary>
    private void InitializeOperations(INotifications? notifications, IAppActivity? appActivity)
    {
        Operations = new OperationsViewModel(_ui, notifications, appActivity ?? _ownAppActivity);
        Operations.Tracker.Changed += OnOperationChanged;
        _diskChangesActivity = appActivity ?? _ownAppActivity;
        if (_diskChangesActivity is not null)
        {
            _diskChangesActivity.Changed += OnAppActivityChangedForDiskChanges;
        }
    }

    private Platform.IAppActivity? _diskChangesActivity;

    /// <summary>Follows the Toolchain Store's installs. Called once, after the toolchains are created.</summary>
    private void InitializeToolchainOperations() => _toolchains.Manager.StatusChanged += OnToolchainOperationStatus;

    private void DisposeOperations()
    {
        if (_diskChangesActivity is not null)
        {
            _diskChangesActivity.Changed -= OnAppActivityChangedForDiskChanges;
        }

        _toolchains.Manager.StatusChanged -= OnToolchainOperationStatus;
        Operations.Tracker.Changed -= OnOperationChanged;
        Operations.Dispose();
    }

    /// <summary>The Error List says "Analyzing…" while what it lists is about to change.</summary>
    private void OnOperationChanged(object? sender, TrackedOperation operation)
    {
        var analysis = Operations.Find(AnalysisOperation);
        ErrorList.PendingText = analysis?.Progress.Numbers is { } numbers ? "Analyzing… " + numbers
            : analysis is not null || Operations.Find(RestoreOperation) is not null || Operations.Find(LanguageOperation) is not null ? "Analyzing…"
            : null;
    }

    /// <summary>The project setup's stage, as its status bar text says it, with Cancel.</summary>
    partial void OnProjectSetupTextChanged(string? value)
    {
        if (Operations is null)
        {
            return;
        }

        if (value is null)
        {
            Operations.Find(SetupOperation)?.Complete();
            return;
        }

        var operation = Operations.Begin(SetupOperation, "Setting up projects", new OperationProgress(value), () => CancelProjectSetupCommand.Execute(null));
        operation.Log(value);
    }

    /// <summary>A toolchain or SDK the Toolchain Store installs, updates or removes, with its download's size and its packages' count.</summary>
    private void OnToolchainOperationStatus(object? sender, ToolchainStatus status) => _ui.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        var key = "toolchain:" + status.Toolchain.Id;
        if (!status.IsBusy)
        {
            Operations.Find(key)?.Complete(status.Error is null ? OperationOutcome.Succeeded : OperationOutcome.Failed);
            return;
        }

        var name = status.Toolchain.Name;
        var title = status.State switch
        {
            ToolchainState.Updating => $"Updating {name}",
            ToolchainState.Removing => $"Removing {name}",
            _ => $"Installing {name}",
        };
        var id = status.Toolchain.Id;
        Action? cancel = status.State is ToolchainState.Installing or ToolchainState.Updating ? () => ToolchainStore.Cancel(id) : null;
        var operation = Operations.Begin(key, title, ProgressOf(status), cancel);
        if (status.Progress?.Message is { Length: > 0 } message && (operation.Details.Count == 0 || operation.Details[^1] != message))
        {
            operation.Log(message);
        }
    });

    /// <summary>"Downloading Pico SDK 120/340 MB", "Installing packages 12/40", or the installer's own words.</summary>
    internal static OperationProgress ProgressOf(ToolchainStatus status) => status.Progress switch
    {
        { Stage: InstallStage.Downloading, BytesTotal: > 0 } p => new OperationProgress($"Downloading {status.Toolchain.Name}", p.BytesDone, p.BytesTotal, ProgressUnit.Bytes),
        { Stage: InstallStage.Installing, StepsTotal: > 0 } p => new OperationProgress("Installing packages", p.StepsDone, p.StepsTotal),
        { Stage: InstallStage.Configuring, Message: { Length: > 0 } m } => new OperationProgress(m),
        { Message: { Length: > 0 } m } p => new OperationProgress(m, p.Fraction is { } f ? Math.Round(f * 100) : null, p.Fraction is null ? null : 100, ProgressUnit.Percent),
        _ => new OperationProgress($"Installing {status.Toolchain.Name}"),
    };

    /// <summary>
    /// Sends the Output's lines to an operation's details while it runs, reading the numbers build tools print:
    /// Ninja's <c>[37/120]</c>, CMake's <c>[ 37%]</c>, apk's <c>(12/40)</c>.
    /// </summary>
    /// <returns>Stops following when disposed.</returns>
    private OutputFollower FollowOutput(TrackedOperation operation) => new(Output, operation);

    /// <summary>
    /// The order the solution's projects are analyzed in: the active document's project first, then those with open
    /// documents, then the rest, so what is on screen has its diagnostics first.
    /// </summary>
    private List<ProjectInfo> InAnalysisOrder(IEnumerable<ProjectInfo> projects)
    {
        var active = ActiveDocument?.FilePath;
        var open = Documents.Select(d => d.FilePath).ToList();
        bool Contains(ProjectInfo project, string? file) =>
            file is not null && file.StartsWith(project.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        return [.. projects.OrderBy(p => Contains(p, active) ? 0 : open.Any(f => Contains(p, f)) ? 1 : 2)];
    }

    /// <summary>Completes an operation when disposed: failed, unless the code that ran it says otherwise.</summary>
    private sealed class CompletingOperation(TrackedOperation operation) : IDisposable
    {
        /// <summary>The operation.</summary>
        public TrackedOperation Operation { get; } = operation;

        /// <summary>How it ended; failed until set, as when an exception ends it.</summary>
        public OperationOutcome Outcome { get; set; } = OperationOutcome.Failed;

        public void Dispose() => Operation.Complete(Outcome);
    }

    /// <summary>Follows the Output for an operation until disposed.</summary>
    private sealed class OutputFollower : IDisposable
    {
        private readonly OutputViewModel _output;
        private readonly TrackedOperation _operation;
        private readonly StringBuilder _line = new();

        public OutputFollower(OutputViewModel output, TrackedOperation operation)
        {
            _output = output;
            _operation = operation;
            _output.Written += OnWritten;
        }

        private void OnWritten(object? sender, OutputWrittenEventArgs e)
        {
            foreach (var c in e.Text)
            {
                if (c == '\n')
                {
                    _operation.Log(_line.ToString().TrimEnd('\r'), parse: true);
                    _line.Clear();
                }
                else
                {
                    _line.Append(c);
                }
            }
        }

        public void Dispose()
        {
            _output.Written -= OnWritten;
            if (_line.Length > 0)
            {
                _operation.Log(_line.ToString(), parse: true);
                _line.Clear();
            }
        }
    }
}
