using TabStudio.Core.Compatibility;

namespace TabStudio.App.ViewModels;

/// <summary>One project in the Compatibility Doctor table.</summary>
/// <param name="Report">The doctor's verdict.</param>
public sealed record CompatibilityRowViewModel(CompatibilityReport Report)
{
    /// <summary>The project name.</summary>
    public string Project => Report.ProjectName;

    /// <summary>The project type, such as "Console App".</summary>
    public string Type => ProjectKindNames.For(Report.Kind);

    /// <summary>How it runs, such as "In-process".</summary>
    public string RunsVia => Report.RunsVia;

    /// <summary>The short status label.</summary>
    public string Status => CompatibilityStatusNames.For(Report.Status);

    /// <summary>Why, in a sentence or two.</summary>
    public string Explanation => Report.Explanation;

    /// <summary>Whether F5 runs it on this device.</summary>
    public bool IsReady => Report.Status == CompatibilityStatus.Ready;

    /// <summary>Whether it is a library, built together with the projects that use it.</summary>
    public bool IsLibrary => Report.Status == CompatibilityStatus.NotRunnable;

    /// <summary>Whether it cannot run on this device as it is.</summary>
    public bool IsBlocked => !IsReady && !IsLibrary;

    /// <summary>Whether code changes would make it run here, which "Fix with Agent" asks Claude to make.</summary>
    public bool CanFixWithAgent => Report.Status == CompatibilityStatus.NeedsChanges;

    /// <summary>Whether it is a legacy .NET Framework project, which Upgrade project converts to an SDK-style one.</summary>
    public bool CanUpgradeProject => Report.CanUpgradeProject;

    /// <summary>Whether this framework has an Avalonia conversion.</summary>
    public bool CanPortToAvalonia => Report.Kind is TabStudio.Core.Workspaces.ProjectKind.Wpf or TabStudio.Core.Workspaces.ProjectKind.WinForms;

    /// <summary>The toolchain that runs the project, such as <c>python</c>, for Python and Node.js projects.</summary>
    public string? ToolchainId { get; init; }

    /// <summary>The label of the fix that installs the project's toolchain, such as "Install Python", when it can be installed.</summary>
    public string? InstallToolchainText { get; init; }

    /// <summary>Whether installing a toolchain from the Toolchain Store makes it run here.</summary>
    public bool CanInstallToolchain => InstallToolchainText is not null;

    /// <summary>
    /// The project's SQL Server, when it uses one that cannot run here and nothing answers for it, which the row offers
    /// to replace: a SQL Server on the network, or PostgreSQL or SQLite when the project supports them.
    /// </summary>
    public SqlServerAdvice? SqlServer { get; init; }

    /// <summary>Whether the row offers to connect to a SQL Server on the network or in the cloud.</summary>
    public bool CanConnectSqlServer => SqlServer is not null;

    /// <summary>The label of the switch to a database that runs here, such as "Use PostgreSQL", when the project supports one.</summary>
    public string? SwitchDatabaseText => SqlServer?.Usage switch
    {
        { SupportsPostgreSql: true } => "Use PostgreSQL",
        { SupportsSqlite: true } => "Use SQLite",
        _ => null,
    };

    /// <summary>Whether the project can switch to a database that runs here.</summary>
    public bool CanSwitchDatabase => SwitchDatabaseText is not null;
}

/// <summary>Why a project's SQL Server cannot be used here, and the connection string it is about.</summary>
/// <param name="Usage">The project's use of SQL Server.</param>
/// <param name="Connection">The connection string nothing answered for, or <see langword="null"/> when the configuration has none.</param>
/// <param name="Message">The one message the Compatibility Doctor and the Output show.</param>
public sealed record SqlServerAdvice(SqlServerUsage Usage, SqlServerConnection? Connection, string Message);

/// <summary>One technology detected in a workspace, shown as a chip.</summary>
/// <param name="Name">The technology, such as ".NET 10" or "React".</param>
/// <param name="Evidence">The file that revealed it, such as "global.json".</param>
public sealed record StackChipViewModel(string Name, string Evidence);
