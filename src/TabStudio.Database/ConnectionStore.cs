using System.Text.Json;

namespace TabStudio.Database;

public interface IDatabaseSecretStore
{
    Task<string?> GetAsync(string key, CancellationToken token = default);
    Task SetAsync(string key, string value, CancellationToken token = default);
    Task RemoveAsync(string key, CancellationToken token = default);
}

// Desktop fallback is intentionally session-only. Android replaces this with its Keystore-backed store.
public sealed class MemoryDatabaseSecretStore : IDatabaseSecretStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);
    public Task<string?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(_values.GetValueOrDefault(key));
    public Task SetAsync(string key, string value, CancellationToken token = default) { _values[key] = value; return Task.CompletedTask; }
    public Task RemoveAsync(string key, CancellationToken token = default) { _values.TryRemove(key, out _); return Task.CompletedTask; }
}

public sealed class ConnectionStore(string metadataFile, IDatabaseSecretStore secrets)
{
    private readonly SemaphoreSlim _gate = new(1);
    private sealed record Profile(Guid Id, string Name, DatabaseProvider Provider);

    public async Task<IReadOnlyList<DatabaseConnection>> LoadAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var found = new List<DatabaseConnection>();
            foreach (var p in await ProfilesAsync(token).ConfigureAwait(false))
                if (await secrets.GetAsync(p.Id.ToString("N"), token).ConfigureAwait(false) is { } value)
                    found.Add(new DatabaseConnection(p.Id, p.Name, p.Provider, value));
            return found;
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(DatabaseConnection connection, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var profiles = await ProfilesAsync(token).ConfigureAwait(false);
            profiles.RemoveAll(p => p.Id == connection.Id);
            profiles.Add(new Profile(connection.Id, connection.Name, connection.Provider));
            await secrets.SetAsync(connection.Id.ToString("N"), connection.ConnectionString, token).ConfigureAwait(false);
            await WriteAsync(profiles, token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(Guid id, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var profiles = await ProfilesAsync(token).ConfigureAwait(false); profiles.RemoveAll(p => p.Id == id);
            await WriteAsync(profiles, token).ConfigureAwait(false);
            await secrets.RemoveAsync(id.ToString("N"), token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<Profile>> ProfilesAsync(CancellationToken token)
    {
        if (!File.Exists(metadataFile)) return [];
        using var stream = File.OpenRead(metadataFile);
        return await JsonSerializer.DeserializeAsync<List<Profile>>(stream, cancellationToken: token).ConfigureAwait(false) ?? [];
    }

    private async Task WriteAsync(List<Profile> profiles, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(metadataFile)!);
        var temp = metadataFile + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(profiles), token).ConfigureAwait(false);
        File.Move(temp, metadataFile, overwrite: true);
    }
}
