using System.Security.Cryptography;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Compilation.Running;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.Database;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Projects that use SQL Server, which has no engine for ARM64 Linux, so neither the tablet nor Services can run it:
/// the Compatibility Doctor and the run's Output say so in one message, and offer a SQL Server on the network (a saved
/// Database Explorer connection the run uses) or, when the project supports one, PostgreSQL in Services or SQLite.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>What the next run's Output says about its databases, written once the build has cleared the Output.</summary>
    private readonly List<(string Text, OutputKind Kind)> _runNotes = [];

    /// <summary>The check of the workspace's SQL Server connections that runs after it opens, for tests to wait for.</summary>
    internal Task DatabaseCheck { get; private set; } = Task.CompletedTask;

    /// <summary>Writes what <see cref="AddDatabaseEnvironmentAsync"/> found to the Output, at the start of the run's build.</summary>
    private void WriteRunNotes()
    {
        foreach (var (text, kind) in _runNotes)
        {
            Output.WriteLine(text, kind);
        }

        _runNotes.Clear();
    }

    /// <summary>
    /// The name of the saved Database Explorer connection that replaces a project's connection string when it runs,
    /// such as <c>SimplCommerce.WebHost: DefaultConnection</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="connectionName">The connection string's name in its configuration.</param>
    public static string RunConnectionName(string project, string connectionName) => $"{project}: {connectionName}";

    /// <summary>Starts checking, off the UI thread, whether the workspace's SQL Server connection strings reach a server.</summary>
    private void StartDatabaseCheck(Workspace workspace) => DatabaseCheck = CheckDatabasesAsync(workspace);

    private async Task CheckDatabasesAsync(Workspace workspace)
    {
        var saved = await SavedConnectionNamesAsync();
        var projects = workspace.Projects.Where(p => p.IsExecutable && p.IsRoslynLanguage && !p.IsTestProject).ToList();
        var advice = await Task.Run(async () =>
        {
            var composed = ComposeConnectionVariables(workspace.RootDirectory);
            var found = new List<(string Project, SqlServerAdvice Advice)>();
            foreach (var project in projects)
            {
                if (SqlServerDetector.Detect(project, workspace.Projects) is { } usage
                    && await FindUnreachableAsync(usage, saved, composed) is var (unreachable, answered) && !answered)
                {
                    found.Add((project.Name, new SqlServerAdvice(usage, unreachable, SqlServerDetector.Explain(usage, unreachable))));
                }
            }

            return found;
        });

        if (_disposed || !ReferenceEquals(Workspace, workspace))
        {
            return;
        }

        foreach (var (project, found) in advice)
        {
            var index = Compatibility.ToList().FindIndex(r => r.Project == project && r.Report.Status == CompatibilityStatus.Ready);
            if (index >= 0)
            {
                var row = Compatibility[index];
                Compatibility[index] = row with { Report = row.Report with { Status = CompatibilityStatus.NeedsDatabase, Explanation = found.Message }, SqlServer = found };
            }
        }
    }

    /// <summary>
    /// The first SQL Server connection string of a project that nothing replaces or answers for; <c>Answered</c> is
    /// <see langword="true"/> when every one is replaced (by a saved connection or the run's environment) or reachable.
    /// </summary>
    private static async Task<(SqlServerConnection? Unreachable, bool Answered)> FindUnreachableAsync(
        SqlServerUsage usage, IReadOnlySet<string> savedConnections, Dictionary<string, string?>? environment)
    {
        if (usage.Connections.Count == 0)
        {
            // Only a package says it uses SQL Server; the doctor cannot know where, so it does not guess.
            return (null, true);
        }

        foreach (var connection in usage.Connections)
        {
            if (savedConnections.Contains(RunConnectionName(usage.ProjectName, connection.Name))
                || environment?.ContainsKey(connection.EnvironmentVariable) == true
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(connection.EnvironmentVariable)))
            {
                continue;
            }

            if (!await SqlServerDetector.IsReachableAsync(connection.Server, SqlServerDetector.ProbeTimeout).ConfigureAwait(false))
            {
                return (connection, false);
            }
        }

        return (null, true);
    }

    private async Task<IReadOnlySet<string>> SavedConnectionNamesAsync()
    {
        try
        {
            return (await DatabaseExplorer.LoadSavedConnectionsAsync()).Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return new HashSet<string>();
        }
    }

    /// <summary>
    /// Before a run: points the project's SQL Server connection strings at the saved Database Explorer connections
    /// named after them, and notes for the run's Output (written when its build starts) when one of them reaches nothing.
    /// </summary>
    /// <param name="project">The project about to run.</param>
    /// <param name="environment">The run's environment so far, such as Services' connection variables.</param>
    /// <returns>The run's environment with the saved connections added.</returns>
    internal async Task<IReadOnlyDictionary<string, string?>> AddDatabaseEnvironmentAsync(ProjectInfo project, IReadOnlyDictionary<string, string?> environment)
    {
        _runNotes.Clear();
        var projects = Workspace?.Projects ?? [];
        if (await Task.Run(() => SqlServerDetector.Detect(project, projects)) is not { Connections.Count: > 0 } usage)
        {
            return environment;
        }

        var result = new Dictionary<string, string?>(environment, StringComparer.Ordinal);
        IReadOnlyList<DatabaseConnection> saved = [];
        try
        {
            saved = await DatabaseExplorer.LoadSavedConnectionsAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _runNotes.Add(($"The Database Explorer's saved connections could not be read: {ex.Message}", OutputKind.Info));
        }

        foreach (var connection in usage.Connections.Where(c => !result.ContainsKey(c.EnvironmentVariable)))
        {
            var name = RunConnectionName(project.Name, connection.Name);
            if (saved.LastOrDefault(c => c.Name == name) is { } replacement)
            {
                result[connection.EnvironmentVariable] = replacement.ConnectionString;
                _runNotes.Add(($"'{connection.Name}' comes from the Database Explorer connection '{name}' for this run ({connection.EnvironmentVariable}); {Path.GetFileName(connection.SourceFile)} is unchanged.", OutputKind.Info));
            }
        }

        var (unreachable, answered) = await Task.Run(() => FindUnreachableAsync(usage, new HashSet<string>(), result));
        if (!answered)
        {
            _runNotes.Add((SqlServerDetector.Explain(usage, unreachable) + " " + ChoicesHint(usage), OutputKind.Failure));
        }

        return result;
    }

    /// <summary>After a crash: explains a SQL Server failure, so a stack trace is never the only explanation.</summary>
    private void ExplainSqlServerCrash(ProjectInfo project, RunResult result)
    {
        if (result.Outcome != RunOutcome.Crashed || !SqlServerDetector.IsSqlServerFailure(result.Exception))
        {
            return;
        }

        var usage = SqlServerDetector.Detect(project, Workspace?.Projects ?? [])
            ?? new SqlServerUsage(project.Name, ["SqlClient in the crash"], [], SupportsPostgreSql: false, SupportsSqlite: false);
        var connection = usage.Connections.FirstOrDefault(c => !_serviceEnvironment.ContainsKey(c.EnvironmentVariable)) ?? (usage.Connections.Count > 0 ? usage.Connections[0] : null);
        Output.WriteLine(SqlServerDetector.Explain(usage, connection) + " " + ChoicesHint(usage), OutputKind.Failure);
    }

    private static string ChoicesHint(SqlServerUsage usage) => usage.CanSwitch
        ? "Both choices are in the Compatibility Doctor (View > Compatibility Doctor)."
        : "Choose Connect to SQL Server in the Compatibility Doctor (View > Compatibility Doctor).";

    /// <summary>
    /// "Connect to SQL Server…": the Database Explorer's connection form, filled in from the project's appsettings. Once
    /// saved (in its encrypted store), the project's runs use it in place of the appsettings value.
    /// </summary>
    [RelayCommand]
    private async Task ConnectSqlServerAsync(CompatibilityRowViewModel? row)
    {
        if (row?.SqlServer is not { } advice)
        {
            return;
        }

        var connection = advice.Connection ?? (advice.Usage.Connections.Count > 0 ? advice.Usage.Connections[0] : null);
        var name = RunConnectionName(advice.Usage.ProjectName, connection?.Name ?? "DefaultConnection");
        SelectSideBarPanel(SideBarPanel.Database);
        await DatabaseExplorer.ActivateAsync();
        DatabaseExplorer.BeginConnection(
            name,
            DatabaseProvider.SqlServer,
            connection is null ? "Server=tcp:db.example.com,1433;Initial Catalog=;User ID=;Password=;Encrypt=True" : WithSqlLogin(connection.Value),
            $"Enter the address of a SQL Server on your network or in the cloud, and a SQL login. Once saved, {advice.Usage.ProjectName} runs with this connection.");
    }

    /// <summary>
    /// "Use PostgreSQL" or "Use SQLite": runs the project with a database that works here. PostgreSQL is added to the
    /// solution's <c>compose.yaml</c>, so Services runs it on F5 and the app gets its connection string; SQLite is a
    /// saved connection to a file beside the project. The appsettings files are not changed.
    /// </summary>
    [RelayCommand]
    private async Task SwitchDatabaseAsync(CompatibilityRowViewModel? row)
    {
        if (row?.SqlServer is not { } advice || Workspace is not { } workspace
            || workspace.Projects.FirstOrDefault(p => p.Name == advice.Usage.ProjectName) is not { } project)
        {
            return;
        }

        var connection = advice.Connection ?? (advice.Usage.Connections.Count > 0 ? advice.Usage.Connections[0] : null);
        var name = connection?.Name ?? "DefaultConnection";
        var variable = connection?.EnvironmentVariable ?? "ConnectionStrings__DefaultConnection";
        var database = SafeName(connection?.Server.Database ?? project.Name);
        if (advice.Usage.SupportsPostgreSql)
        {
            var compose = Path.Combine(workspace.RootDirectory, "compose.yaml");
            if (HasComposeFiles(workspace.RootDirectory))
            {
                Output.WriteLine(
                    $"This repository already has Compose files, so TabStudio does not write one. Add a PostgreSQL service to them, and give {project.Name} "
                    + $"{variable}: Host=<service>;Database={database};Username=app;Password=<password> in its environment.",
                    OutputKind.Info);
                SelectSideBarPanel(SideBarPanel.Services);
                return;
            }

            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
            var context = Path.GetRelativePath(workspace.RootDirectory, project.DirectoryPath).Replace('\\', '/');
            var provider = advice.Usage.ProviderSetting is { } setting ? $"\n      {setting.Key}: PostgreSQL" : string.Empty;
            await File.WriteAllTextAsync(compose, $"""
                # Written by TabStudio: {project.Name} uses PostgreSQL here, because SQL Server cannot run on an ARM64 tablet.
                # Services runs the database on F5, and the app gets {variable} for it. appsettings.json is unchanged.
                services:
                  app:
                    build: {context}
                    depends_on: [postgres]
                    environment:
                      {variable}: Host=postgres;Port=5432;Database={database};Username=app;Password={password}{provider}
                  postgres:
                    image: postgres:17
                    environment:
                      POSTGRES_USER: app
                      POSTGRES_PASSWORD: {password}
                      POSTGRES_DB: {database}
                    ports: ['0:5432']

                """);
            await Services.LoadAsync(workspace.RootDirectory);
            Output.WriteLine($"Added PostgreSQL to compose.yaml: F5 starts it in Services and runs {project.Name} with {variable} pointing at it. The app must use its PostgreSQL provider for this connection.", OutputKind.Success);
            SelectSideBarPanel(SideBarPanel.Services);
        }
        else if (advice.Usage.SupportsSqlite)
        {
            var file = Path.Combine(project.DirectoryPath, database + ".db");
            await DatabaseExplorer.SaveRunConnectionAsync(new DatabaseConnection(Guid.NewGuid(), RunConnectionName(project.Name, name), DatabaseProvider.SQLite, $"Data Source={file}"));
            Output.WriteLine($"{project.Name} now runs with SQLite in {Path.GetFileName(file)} ({variable}), saved as a Database Explorer connection. The app must use its SQLite provider for this connection.", OutputKind.Success);
        }

        var index = Compatibility.IndexOf(row);
        if (index >= 0)
        {
            Compatibility[index] = row with { Report = CompatibilityDoctor.Diagnose(project), SqlServer = null };
        }
    }

    /// <summary>
    /// The <c>ConnectionStrings__*</c> variables the repository's Compose files give an app, such as the one Use
    /// PostgreSQL writes: Services supplies them on F5, so they replace appsettings.
    /// </summary>
    private static Dictionary<string, string?> ComposeConnectionVariables(string root)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var name in (string[])["compose.yaml", "compose.yml", "docker-compose.yml", "docker-compose.yaml"])
        {
            var path = Path.Combine(root, name);
            try
            {
                if (File.Exists(path))
                {
                    foreach (System.Text.RegularExpressions.Match match in ConnectionVariable().Matches(File.ReadAllText(path)))
                    {
                        variables[match.Value] = null;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Services reports Compose files it cannot read.
            }
        }

        return variables;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"ConnectionStrings__[A-Za-z0-9_]+")]
    private static partial System.Text.RegularExpressions.Regex ConnectionVariable();

    /// <summary>Whether the folder already has Compose files, which TabStudio never overwrites.</summary>
    private static bool HasComposeFiles(string root)
    {
        try
        {
            return global::TabStudio.Services.Compose.ComposeReader.Discover(root) is not null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Files that do not parse are still the repository's own.
            return true;
        }
    }

    /// <summary>A connection string from appsettings with Windows sign-in replaced by an empty SQL login to fill in.</summary>
    private static string WithSqlLogin(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var removed = parts.RemoveAll(p => p.StartsWith("Trusted_Connection", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Integrated Security", StringComparison.OrdinalIgnoreCase));
        if (removed > 0 && !parts.Any(p => p.StartsWith("User ID", StringComparison.OrdinalIgnoreCase) || p.StartsWith("UID", StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add("User ID=");
            parts.Add("Password=");
        }

        return string.Join(';', parts);
    }

    private static string SafeName(string name)
    {
        var safe = new string([.. name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')]).Trim('_');
        return safe.Length == 0 ? "app" : safe;
    }
}
