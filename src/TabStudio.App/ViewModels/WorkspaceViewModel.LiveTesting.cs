using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using AvaloniaEdit.Document;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;
using TabStudio.LiveTesting.Building;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Live Unit Testing in the workspace: it follows the documents' edits and saves, gives way to builds, programs,
/// debug sessions, Test Explorer runs and notebook cells, and remembers its settings per solution.
/// </summary>
public sealed partial class WorkspaceViewModel : ILiveTestingHost, ILiveTestingExplorerActions
{
    private readonly Dictionary<DocumentViewModel, EventHandler<DocumentChangeEventArgs>> _liveTestingEditHandlers = [];
    private LiveTestingSettingsStore _liveTestingSettings;
    private string? _liveTestingKey;

    /// <summary>Live Unit Testing.</summary>
    public LiveTestingViewModel LiveTesting { get; private set; }

    /// <inheritdoc />
    IReadOnlyList<ProjectInfo> ILiveTestingHost.TestProjects => TestProjectsOf(Workspace);

    /// <inheritdoc />
    bool ILiveTestingHost.CanRunLive =>
        !_disposed && _language is not null && _testRun is null && !Tests.IsRunning && !IsNotebookRunning && State is IdeState.Ready or IdeState.Failed;

    /// <inheritdoc />
    ThermalState ILiveTestingHost.Thermal => _device.Thermal;

    /// <inheritdoc />
    DeviceProfile ILiveTestingHost.Profile => _profile;

    /// <inheritdoc />
    IEnumerable<DocumentViewModel> ILiveTestingHost.OpenDocuments => Documents;

    /// <inheritdoc />
    async Task ILiveTestingHost.PrepareLiveRunAsync()
    {
        // The editors' text reaches the language service a moment after typing; a live build needs it now.
        foreach (var document in Documents)
        {
            document.SyncText();
        }

        await _restore;
    }

    /// <inheritdoc />
    Task<CoverageBuild> ILiveTestingHost.BuildForCoverageAsync(ProjectInfo testProject, IReadOnlySet<string> uninstrumented, CancellationToken cancellationToken) =>
        _language is { } language
            ? language.BuildForCoverageAsync(testProject, uninstrumented, cancellationToken)
            : Task.FromCanceled<CoverageBuild>(new CancellationToken(canceled: true));

    /// <inheritdoc />
    IReadOnlyList<string> ILiveTestingHost.GetPackageRuntimeAssemblies(string projectFilePath) =>
        _language?.GetPackageRuntimeAssemblies(projectFilePath) ?? [];

    /// <inheritdoc />
    void ILiveTestingHost.SaveLiveTestingSettings(LiveTestingSettings settings)
    {
        if (_liveTestingKey is { } key)
        {
            _liveTestingSettings.Save(key, settings);
        }
    }

    /// <inheritdoc />
    void ILiveTestingExplorerActions.SetLiveTesting(WorkspaceNode node, bool include)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Project is { } project)
        {
            LiveTesting.SetInclusion(project.FilePath, string.Empty, include);
            StatusText = $"{(include ? "Included" : "Excluded")} {project.Name} {(include ? "in" : "from")} Live Unit Testing";
        }
    }

    /// <summary>Creates Live Unit Testing, off, and starts following the workspace. Called once, by the constructor.</summary>
    [MemberNotNull(nameof(LiveTesting), nameof(_liveTestingSettings))]
    private void InitializeLiveTesting()
    {
        _liveTestingSettings = new LiveTestingSettingsStore(_paths);
        LiveTesting = new LiveTestingViewModel(this, _ui, null);
        Tests.LiveTesting = LiveTesting;
        PropertyChanged += OnPropertyChangedForLiveTesting;
        Tests.PropertyChanged += OnTestExplorerChangedForLiveTesting;
        Documents.CollectionChanged += OnDocumentsChangedForLiveTesting;
    }

    /// <summary>Stops Live Unit Testing when the workspace closes.</summary>
    private void DisposeLiveTesting()
    {
        PropertyChanged -= OnPropertyChangedForLiveTesting;
        Tests.PropertyChanged -= OnTestExplorerChangedForLiveTesting;
        Documents.CollectionChanged -= OnDocumentsChangedForLiveTesting;
        foreach (var (document, handler) in _liveTestingEditHandlers)
        {
            document.Document.Changed -= handler;
        }

        _liveTestingEditHandlers.Clear();
        LiveTesting.Dispose();
    }

    /// <summary>After a C# document was saved: in "run on save" mode, the tests it affects run.</summary>
    private void OnDocumentSavedForLiveTesting(DocumentViewModel document) => LiveTesting.OnDocumentSaved(document);

    private void OnPropertyChangedForLiveTesting(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Workspace):
                var key = Workspace is { } workspace ? workspace.Solution?.FilePath ?? workspace.RootDirectory : null;
                if (key != _liveTestingKey)
                {
                    _liveTestingKey = key;
                    LiveTesting.ApplySettings(key is null ? null : _liveTestingSettings.Load(key));
                }

                LiveTesting.OnWorkspaceReloaded();
                break;
            case nameof(State) or nameof(IsNotebookRunning) or nameof(ThermalText):
                LiveTesting.OnIdeChanged();
                break;
        }
    }

    private void OnTestExplorerChangedForLiveTesting(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestExplorerViewModel.IsRunning))
        {
            LiveTesting.OnIdeChanged();
        }
    }

    private void OnDocumentsChangedForLiveTesting(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var document in e.OldItems?.OfType<DocumentViewModel>() ?? [])
        {
            if (_liveTestingEditHandlers.Remove(document, out var handler))
            {
                document.Document.Changed -= handler;
            }

            document.LiveTesting = null;
        }

        foreach (var document in e.NewItems?.OfType<DocumentViewModel>() ?? [])
        {
            if (_liveTestingEditHandlers.ContainsKey(document))
            {
                continue;
            }

            void OnEdited(object? s, DocumentChangeEventArgs args) => LiveTesting.OnDocumentEdited(document);
            _liveTestingEditHandlers[document] = OnEdited;
            document.Document.Changed += OnEdited;
            LiveTesting.OnDocumentOpened(document);
        }
    }
}
