using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using TabStudio.App.ViewModels.Database;

namespace TabStudio.App.Tests.Database;

/// <summary>
/// A database failure names its reason: Database Explorer said "SqliteException: connection or schema operation
/// failed" for a missing file, a wrong password and an unreachable host alike.
/// </summary>
public sealed class DatabaseErrorTests
{
    [Fact]
    public void A_failure_says_what_went_wrong_without_the_connection_details()
    {
        Assert.StartsWith("SQLite cannot open the database file", DatabaseError.Message(new SqliteException("SQLite Error 14: 'unable to open database file'.", 14)), StringComparison.Ordinal);
        Assert.StartsWith("Cannot reach the server: Connection refused", DatabaseError.Message(new SocketException((int)SocketError.ConnectionRefused)), StringComparison.Ordinal);
        Assert.StartsWith("The server did not answer in time", DatabaseError.Message(new TimeoutException()), StringComparison.Ordinal);
    }
}
