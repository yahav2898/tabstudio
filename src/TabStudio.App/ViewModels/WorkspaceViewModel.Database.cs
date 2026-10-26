using System.Collections;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Database;
using TabStudio.App.ViewModels.Database;
using TabStudio.Core.Workspaces;
using TabStudio.Database;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    public DatabaseExplorerViewModel DatabaseExplorer { get; private set; } = null!;
    public bool IsDatabaseSelected => SelectedSideBarPanel == SideBarPanel.Database;
    private void InitializeDatabase(IDatabaseSecretStore? secrets)
    {
        DatabaseExplorer = new DatabaseExplorerViewModel(new ConnectionStore(Path.Combine(_paths.State, "database-connections.json"), secrets ?? new MemoryDatabaseSecretStore()),
            () => Workspace?.RootDirectory, new EfCoreViewModel(ExecuteEfAsync));
        DatabaseExplorer.OpenRequested += OpenDatabaseDocument;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedSideBarPanel) && IsDatabaseSelected) _ = DatabaseExplorer.ActivateAsync();
        };
    }
    [RelayCommand] private async Task ShowDatabaseAsync()
    {
        SelectSideBarPanel(SideBarPanel.Database); await DatabaseExplorer.ActivateAsync();
    }
    private void OpenDatabaseDocument(DatabaseConnection connection, DatabaseSchema? schema, DatabaseTable? table)
    {
        var query = new DatabaseQueryViewModel(connection, schema, table);
        var folder = Path.Combine(_paths.State, "SqlQueries"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, (table is null ? "Query" : table.Name.Split(Path.GetInvalidFileNameChars())[0]) + "-" + Guid.NewGuid().ToString("N")[..8] + ".sql");
        var document = DocumentViewModel.ForDatabase(query, path, _profile);
        Attach(document); Documents.Add(document); ActiveDocument = document;
        if (table is not null) _ = query.LoadPageAsync();
    }

    private async Task<object?> ExecuteEfAsync(ProjectInfo project, ProjectInfo startup, string operation, IDictionary args, CancellationToken token)
    {
        if (_language is not { } language) throw new InvalidOperationException("The C# language service is not ready.");
        if (!await SaveAllCoreAsync()) throw new InvalidOperationException("Save the solution before running EF Core.");
        await WaitForPackagesAsync(); token.ThrowIfCancellationRequested();
        var targetBuild = await language.BuildAsync(project, token);
        var startupBuild = startup.FilePath == project.FilePath ? targetBuild : await language.BuildAsync(startup, token);
        ErrorList.SetBuildResult(targetBuild);
        if (startupBuild != targetBuild) ErrorList.SetBuildResult(startupBuild);
        if (!targetBuild.Success || !startupBuild.Success) throw new InvalidOperationException("EF Core build failed. Build the solution and correct its errors first.");
        var runtime = language.GetPackageRuntimeAssemblies(startup.FilePath).Concat(language.GetPackageRuntimeAssemblies(project.FilePath)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!runtime.Any(p => Path.GetFileName(p).Equals("Microsoft.EntityFrameworkCore.Design.dll", StringComparison.OrdinalIgnoreCase)))
        {
            var ef = runtime.FirstOrDefault(p => Path.GetFileName(p).Equals("Microsoft.EntityFrameworkCore.dll", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("This project has no EF Core runtime. Add an EF Core provider to the project first.");
            var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(ef).ProductVersion?.Split('+')[0]
                ?? throw new InvalidOperationException("The EF Core runtime version could not be determined.");
            var directory = Path.Combine(_paths.Cache, "EfCoreTools", version); Directory.CreateDirectory(directory);
            var tool = new ProjectInfo { Name = "EfCoreTools", FilePath = Path.Combine(directory, "EfCoreTools.csproj"), Language = ProjectLanguage.CSharp,
                PackageReferences = [new PackageReferenceInfo("Microsoft.EntityFrameworkCore.Design", version)] };
            var restored = await _compiler.Packages.RestoreAsync(tool, cancellationToken: token);
            if (!restored.Success) throw new InvalidOperationException("Could not restore the matching EF Core design tools. Check NuGet connectivity and the project's EF version.");
            runtime.AddRange(restored.RuntimeAssemblies);
        }
        token.ThrowIfCancellationRequested();
        // EF's design API is synchronous and has no cancellation token. Once started, let it finish.
        var result = await Task.Run(() => EfCoreOperations.Execute(targetBuild, startupBuild, runtime, project, operation, args), CancellationToken.None);
        if (operation == "AddMigration" && !_disposed) await ReloadWorkspaceAsync();
        return result;
    }
}
