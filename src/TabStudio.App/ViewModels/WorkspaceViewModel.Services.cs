using TabStudio.App.ViewModels.BackingServices;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Services;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

/// <summary>Backing services run before F5; the launched app gets localhost connection variables.</summary>
public sealed partial class WorkspaceViewModel
{
    private bool _preparingServices;
    private IReadOnlyDictionary<string, string?> _serviceEnvironment = new Dictionary<string, string?>();
    public ServicesViewModel Services { get; private set; } = null!;
    public bool IsServicesSelected => SelectedSideBarPanel == SideBarPanel.Services;

    private void InitializeServices(IServiceDatabaseExplorer? databases)
    {
        ConfigureServiceDatabaseExplorer(ref databases);
        Services = new ServicesViewModel(_toolchains, _paths, _ui, address =>
        {
            WebPreview.Show("Service", address, null);
            SelectSideBarPanel(SideBarPanel.WebPreview);
        }, () =>
        {
            Output.WriteLine("This image needs a remote container host. Use TabStudio Link / Windows Bridge, or connect with Remote SSH when available, and run Compose there.", OutputKind.Info);
            SelectSideBarPanel(SideBarPanel.WindowsBridge);
        }, databases);
    }

    partial void ConfigureServiceDatabaseExplorer(ref IServiceDatabaseExplorer? databases);

    private async Task<bool> PrepareServicesAsync(ProjectInfo project)
    {
        if (!await PrepareServicesAsync(project.DirectoryPath))
        {
            return false;
        }

        // Saved Database Explorer connections that replace the project's SQL Server, and the message when none answers.
        // Still preparing: the probes of each connection take seconds, and F5 or Build pressed meanwhile started a
        // second build and run beside this one.
        _preparingServices = true;
        NotifyServicePreparationCommands();
        try
        {
            _serviceEnvironment = await AddDatabaseEnvironmentAsync(project, _serviceEnvironment);
        }
        finally
        {
            _preparingServices = false;
            NotifyServicePreparationCommands();
        }

        return true;
    }

    private async Task<bool> PrepareServicesAsync(string projectDirectory)
    {
        _run = new CancellationTokenSource();
        _preparingServices = true;
        if (Services.HasServices)
        {
            State = IdeState.Running;
            StatusText = "Checking backing services";
        }

        NotifyServicePreparationCommands();
        try
        {
            var profile = LaunchSettings.Read(projectDirectory);
            var environment = await Services.PrepareAsync(projectDirectory, profile.Environment ?? "Development", _run.Token);
            if (environment is null)
            {
                var reason = Services.OperationError ?? Services.Error ?? "Services did not start";
                State = IdeState.Failed; StatusText = reason;

                // In the Output too, where F5's result is looked for: without it F5 seemed to do nothing.
                Output.WriteLine($"The app was not started because its backing services did not start: {reason.TrimEnd('.')}. They are in the Services panel.", OutputKind.Failure);
                SelectSideBarPanel(SideBarPanel.Services);
                return false;
            }
            _serviceEnvironment = environment;
            State = IdeState.Ready;
            return true;
        }
        finally
        {
            _run.Dispose(); _run = null;
            _preparingServices = false;
            NotifyServicePreparationCommands();
        }
    }

    private void NotifyServicePreparationCommands()
    {
        RunCommand.NotifyCanExecuteChanged();
        BuildCommand.NotifyCanExecuteChanged();
        NotifyDebugCommands();
    }

    private ProcessStartSpec WithServiceEnvironment(ProcessStartSpec spec)
    {
        var environment = new Dictionary<string, string?>(spec.Environment, StringComparer.Ordinal);
        foreach (var (name, value) in _serviceEnvironment) environment[name] = value;
        return spec with { Environment = environment };
    }
}
