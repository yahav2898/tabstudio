namespace TabStudio.Core.Compatibility;

/// <summary>How a SQL Server connection string reaches its server.</summary>
public enum SqlServerTransport
{
    /// <summary>TCP to a host and port, the only transport a tablet has.</summary>
    Tcp,

    /// <summary>SQL Server Express LocalDB, such as <c>(localdb)\MSSQLLocalDB</c>, which exists only on Windows.</summary>
    LocalDb,

    /// <summary>Named pipes (<c>np:</c>) or shared memory (<c>lpc:</c>), which work only on the server's own Windows machine.</summary>
    WindowsOnly,
}
