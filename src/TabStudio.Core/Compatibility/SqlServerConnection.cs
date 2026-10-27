namespace TabStudio.Core.Compatibility;

/// <summary>One SQL Server connection string of a project's configuration.</summary>
/// <param name="Name">Its name under <c>ConnectionStrings</c>, such as <c>DefaultConnection</c>.</param>
/// <param name="Value">The connection string as written.</param>
/// <param name="SourceFile">The <c>appsettings*.json</c> file it comes from.</param>
/// <param name="Server">Where it points.</param>
public sealed record SqlServerConnection(string Name, string Value, string SourceFile, SqlServerConnectionString Server)
{
    /// <summary>
    /// The environment variable that overrides it for a run, as ASP.NET Core's configuration reads it:
    /// <c>ConnectionStrings__DefaultConnection</c>.
    /// </summary>
    public string EnvironmentVariable => "ConnectionStrings__" + Name.Replace(":", "__", StringComparison.Ordinal);
}
