using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels.Database;

public sealed record EfContext(string Name, string FullName) { public override string ToString() => Name; }
public sealed record EfProject(ProjectInfo Project) { public override string ToString() => Project.Name; }

public sealed partial class EfCoreViewModel : ObservableObject, IDisposable
{
    private readonly Func<ProjectInfo, ProjectInfo, string, IDictionary, CancellationToken, Task<object?>> _execute;
    private readonly CancellationTokenSource _lifetime = new();
    public EfCoreViewModel(Func<ProjectInfo, ProjectInfo, string, IDictionary, CancellationToken, Task<object?>> execute) => _execute = execute;
    public ObservableCollection<EfProject> Projects { get; } = [];
    public ObservableCollection<EfContext> Contexts { get; } = [];
    public ObservableCollection<string> Migrations { get; } = [];
    [ObservableProperty] public partial EfProject? SelectedProject { get; set; }
    [ObservableProperty] public partial EfProject? StartupProject { get; set; }
    [ObservableProperty] public partial EfContext? SelectedContext { get; set; }
    [ObservableProperty] public partial string MigrationName { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial string Status { get; private set; } = "Select a project and list its DbContexts. Operations use the project's configured database.";
    public void LoadProjects(IReadOnlyList<ProjectInfo> projects)
    {
        var selected = SelectedProject?.Project.FilePath;
        Projects.Clear();
        foreach (var p in projects.Where(p => p.Language == ProjectLanguage.CSharp)) Projects.Add(new EfProject(p));
        SelectedProject = Projects.FirstOrDefault(p => p.Project.FilePath == selected) ?? Projects.FirstOrDefault(p => p.Project.PackageReferences.Any(r => r.Name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))) ?? Projects.FirstOrDefault();
        StartupProject = SelectedProject;
        Contexts.Clear(); Migrations.Clear();
    }
    partial void OnSelectedProjectChanged(EfProject? value) { Contexts.Clear(); Migrations.Clear(); SelectedContext = null; StartupProject = value; }
    partial void OnSelectedContextChanged(EfContext? value) { Migrations.Clear(); }
    [RelayCommand] private Task ListContextsAsync() => GuardAsync(async token =>
    {
        var result = await ExecuteAsync("GetContextTypes", new Hashtable(), token);
        Contexts.Clear(); Migrations.Clear();
        foreach (var row in Dictionaries(result)) Contexts.Add(new EfContext((string)row["Name"]!, (string)row["FullName"]!));
        SelectedContext = Contexts.FirstOrDefault(); Status = $"{Contexts.Count} DbContext(s) found.";
    });
    [RelayCommand] private Task ListMigrationsAsync() => GuardAsync(async token =>
    {
        RequireContext();
        var result = await ExecuteAsync("GetMigrations", new Hashtable { ["contextType"] = SelectedContext!.FullName, ["noConnect"] = true }, token);
        Migrations.Clear();
        foreach (var row in Dictionaries(result)) Migrations.Add((string)row["Id"]!);
        Status = $"{Migrations.Count} migration(s). Listing does not connect to the database.";
    });
    [RelayCommand] private Task UpdateDatabaseAsync() => GuardAsync(async token =>
    {
        RequireContext();
        await ExecuteAsync("UpdateDatabase", new Hashtable { ["contextType"] = SelectedContext!.FullName }, token);
        Status = "Database updated to the latest migration.";
    });
    [RelayCommand] private Task AddMigrationAsync() => GuardAsync(async token =>
    {
        RequireContext();
        if (string.IsNullOrWhiteSpace(MigrationName) || MigrationName.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new InvalidOperationException("Enter a migration name using letters, digits and underscores.");
        var result = await ExecuteAsync("AddMigration", new Hashtable { ["contextType"] = SelectedContext!.FullName, ["name"] = MigrationName, ["outputDir"] = "Migrations", ["dryRun"] = false }, token);
        var files = result is IDictionary paths ? paths.Values.Cast<object?>().OfType<string>().Select(Path.GetFileName) : [];
        Status = "Migration added: " + string.Join(", ", files); MigrationName = string.Empty;
    });
    private void RequireContext() { if (SelectedContext is null) throw new InvalidOperationException("List and select a DbContext first."); }
    private Task<object?> ExecuteAsync(string operation, Hashtable args, CancellationToken token)
    {
        if (SelectedProject is not { } project) throw new InvalidOperationException("Open a solution containing an EF Core project.");
        Status = "Building projects and running EF Core…";
        var startup = StartupProject?.Project ?? project.Project;
        args["startupProjectPath"] = startup.FilePath;
        return _execute(project.Project, startup, operation, args, token);
    }
    private static IEnumerable<IDictionary> Dictionaries(object? value) => value is IEnumerable items ? items.Cast<object>().OfType<IDictionary>() : [];
    private async Task GuardAsync(Func<CancellationToken, Task> operation)
    {
        if (IsBusy || _lifetime.IsCancellationRequested) return;
        IsBusy = true;
        try { await operation(_lifetime.Token); }
        catch (OperationCanceledException) { Status = "Cancelled before the EF operation started."; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Status = DatabaseError.Message(ex); }
        finally { IsBusy = false; }
    }
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); }
}
