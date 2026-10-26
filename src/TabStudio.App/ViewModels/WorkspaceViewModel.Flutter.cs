using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Toolchains;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.Toolchains.Projects;
using TabStudio.Compilation;
using TabStudio.Swarm;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.ViewModels;

/// <summary>Flutter's web preview and hot restart on save; Dart uses the regular DAP debugger views.</summary>
public sealed partial class WorkspaceViewModel
{
    private bool _flutterReloading;
    private bool _flutterReloadPending;
    private bool _flutterStarted;
    private string? _flutterFolder;
    private bool _flutterWeb;

    private async Task StartDartOrFlutterAsync(ToolchainStartupItem item, bool noDebug)
    {
        if (!await EnsureToolchainReadyAsync(item.Toolchain, "Flutter and Dart are required to run this project.")) return;
        if (!await SaveAllCoreAsync()) return;
        var plan = RunPlanner.PlanProject(item.Project);
        if (plan is null) { Output.WriteLine("Add lib/main.dart or bin/main.dart to run this project.", OutputKind.Failure); return; }
        var flutter = plan.Run.Program == "flutter";
        var type = flutter ? "flutter" : "dart";
        var configuration = ToolchainLaunch.Create(type, plan)!;
        _flutterFolder = item.Project.Directory;
        _flutterWeb = flutter;
        _flutterStarted = false;
        if (flutter) WebPreview.ShowStopped();
        // web-server supplies a page to any embedded browser. It has no Dart Debug Extension, so VM debugging
        // is available for Dart console/native Flutter targets, while this target uses hot restart.
        if (flutter && !noDebug)
            Output.WriteLine("Flutter web-server previews use Hot Restart. Use a native device launch configuration for VM breakpoints and Hot Reload.", OutputKind.Info);
        await LaunchAdapterAsync(new LaunchTarget(item.Info.Name, configuration, false), DebugStart.Run, noDebug || flutter);
    }

    private void WireFlutterEvents(DebugAdapterSession session)
    {
        if (session.Adapter.Id != "flutter") return;
        session.FlutterAppStarted += (_, _) => _ui.Post(() => _flutterStarted = ReferenceEquals(session, _adapterRoot));
        session.FlutterWebLaunch += (_, address) => _ui.Post(() =>
        {
            if (!ReferenceEquals(session, _adapterRoot)) return;
            WebPreview.Show(_adapterTarget?.Name ?? "Flutter", address, null);
            SelectSideBarPanel(SideBarPanel.WebPreview);
            if (StartupProject is { } project) OnWebAppStarted(project);
        });
        session.Ended += (_, _) => _ui.Post(() =>
        {
            if (!ReferenceEquals(session, _adapterRoot) && _adapterRoot is not null) return;
            _flutterStarted = false;
            _flutterFolder = null;
            WebPreview.ShowStopped();
            OnWebAppStopped();
            if (_flutterSwarmStop is { } stop)
            {
                stop.Cancel();
            }
        });
    }

    private void ConfigureFlutterTarget(TabStudio.LanguageServers.Configuration.LaunchConfiguration configuration)
    {
        _flutterStarted = false;
        _flutterFolder = configuration.WorkingDirectory ?? Workspace?.RootDirectory;
        _flutterWeb = configuration.Properties["toolArgs"]?.ToJsonString().Contains("web-server", StringComparison.Ordinal) == true;
    }

    private void OnDocumentSavedForFlutter(DocumentViewModel document)
    {
        if (!_flutterStarted || _adapter?.Adapter.Id != "flutter") return;
        var folder = _flutterFolder ?? _adapterTarget?.Configuration.Properties["cwd"]?.GetValue<string>();
        if (folder is null || !document.FilePath.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.GetExtension(document.FilePath) != ".dart")
        {
            return;
        }
        _flutterReloadPending = true;
        _ = ReloadFlutterAsync(fullRestart: _flutterWeb, reason: "save");
    }

    /// <summary>Debug &gt; Flutter Hot Reload; web-server targets use Hot Restart.</summary>
    [RelayCommand]
    private Task FlutterHotReloadAsync() => ReloadFlutterAsync(fullRestart: _flutterWeb);

    /// <summary>Debug &gt; Flutter Hot Restart resets the application's state.</summary>
    [RelayCommand]
    private Task FlutterHotRestartAsync() => ReloadFlutterAsync(fullRestart: true);

    private async Task ReloadFlutterAsync(bool fullRestart, string reason = "manual")
    {
        if (_flutterReloading || !_flutterStarted || _adapter is not { Adapter.Id: "flutter" } session) return;
        _flutterReloading = true;
        try
        {
            do
            {
                _flutterReloadPending = false;
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                await session.ReloadFlutterAsync(fullRestart, reason, timeout.Token);
                Output.WriteLine(fullRestart ? "Flutter Hot Restart completed." : "Flutter Hot Reload completed.", OutputKind.Info);
                if (_flutterWeb) WebPreview.ReloadCommand.Execute(null);
            }
            while (_flutterReloadPending && !session.HasEnded);
        }
        catch (Exception ex) when (ex is DebugAdapterException or OperationCanceledException)
        {
            Output.WriteLine($"Flutter reload failed: {ex.Message}", OutputKind.Failure);
        }
        finally { _flutterReloading = false; }
    }

    private CancellationTokenSource? _flutterSwarmStop;

    private async Task RunFlutterOnSwarmAsync(ToolchainStartupItem item)
    {
        if (_swarmServices?.Coordinator is not { } swarm) return;
        if (!swarm.IsOn && !await swarm.StartAsync()) return;
        await StartDartOrFlutterAsync(item, noDebug: true);
        if (_adapterRoot is null) return;
        using var stop = new CancellationTokenSource();
        _flutterSwarmStop = stop;
        try
        {
            using var startTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            while (!WebPreview.IsRunning && _adapterRoot is not null)
                await Task.Delay(100, startTimeout.Token);
            if (!WebPreview.IsRunning || WebPreview.AppAddress is not { } address) return;
            // Web devices open the tunnel URL and never load the assembly field. The empty entry is a protocol
            // placeholder: no .NET compilation or assembly execution is involved in a Flutter web deployment.
            await swarm.Coordinator!.RunAsync(new SwarmBuild
            {
                AppName = item.Info.Name, Kind = SwarmAppKind.Web,
                Entry = new CompiledAssembly("FlutterWeb", [], null), Web = new SwarmWebTarget(address),
            }, stop.Token);
            SelectSideBarPanel(SideBarPanel.Swarm);
            await Task.Delay(Timeout.Infinite, stop.Token);
        }
        catch (OperationCanceledException) { }
        catch (IOException ex) { Output.WriteLine($"Flutter Swarm: {ex.Message}", OutputKind.Failure); }
        finally
        {
            _flutterSwarmStop = null;
            await swarm.Coordinator!.StopRunAsync();
        }
    }
}
