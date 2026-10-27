namespace TabStudio.Core.Compatibility;

/// <summary>
/// A project that uses SQL Server: the evidence, its SQL Server connection strings, and the other databases it also
/// supports, which decide what TabStudio can offer in its place.
/// </summary>
/// <param name="ProjectName">The project.</param>
/// <param name="Evidence">What shows it uses SQL Server, such as the package <c>Microsoft.EntityFrameworkCore.SqlServer</c>.</param>
/// <param name="Connections">Its SQL Server connection strings, from <c>appsettings*.json</c>; may be empty.</param>
/// <param name="SupportsPostgreSql">Whether it also references a PostgreSQL provider (Npgsql), so it can switch to PostgreSQL.</param>
/// <param name="SupportsSqlite">Whether it also references a SQLite provider, so it can switch to SQLite.</param>
/// <param name="ProviderSetting">
/// A configuration key that chooses the database provider, such as <c>DatabaseProvider</c>, when the configuration has
/// one, with its current value.
/// </param>
public sealed record SqlServerUsage(
    string ProjectName,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<SqlServerConnection> Connections,
    bool SupportsPostgreSql,
    bool SupportsSqlite,
    KeyValuePair<string, string>? ProviderSetting = null)
{
    /// <summary>Whether the project can use a database other than SQL Server that runs on the tablet.</summary>
    public bool CanSwitch => SupportsPostgreSql || SupportsSqlite;
}
