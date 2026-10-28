namespace TabStudio.Containers.Registry;

/// <summary>A registry sign-in: a user name and a password or access token.</summary>
/// <param name="Username">The user name; for a token-only registry any non-empty name.</param>
/// <param name="Secret">The password or personal access token.</param>
public sealed record RegistryCredential(string Username, string Secret);

/// <summary>
/// Where registry sign-ins are kept. On Android the app stores them with the Keystore-backed secret store that
/// also keeps database passwords, so they are encrypted with a key that never leaves the device; on a computer the
/// default keeps them only for the session.
/// </summary>
public interface IRegistryCredentialStore
{
    /// <summary>The sign-in for a registry host, such as <c>ghcr.io</c>, if one is saved.</summary>
    Task<RegistryCredential?> GetAsync(string registry, CancellationToken cancellationToken = default);

    /// <summary>Saves the sign-in for a registry host.</summary>
    Task SetAsync(string registry, RegistryCredential credential, CancellationToken cancellationToken = default);

    /// <summary>Forgets the sign-in for a registry host.</summary>
    Task RemoveAsync(string registry, CancellationToken cancellationToken = default);
}

/// <summary>Sign-ins kept in memory for this session only.</summary>
public sealed class SessionRegistryCredentialStore : IRegistryCredentialStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RegistryCredential> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<RegistryCredential?> GetAsync(string registry, CancellationToken cancellationToken = default) => Task.FromResult(_values.GetValueOrDefault(Normalize(registry)));

    /// <inheritdoc />
    public Task SetAsync(string registry, RegistryCredential credential, CancellationToken cancellationToken = default)
    {
        _values[Normalize(registry)] = credential;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string registry, CancellationToken cancellationToken = default)
    {
        _values.TryRemove(Normalize(registry), out _);
        return Task.CompletedTask;
    }

    /// <summary>Docker Hub's sign-in is kept under <c>docker.io</c> whichever of its names is used.</summary>
    public static string Normalize(string registry) =>
        registry.ToLowerInvariant() is "index.docker.io" or "registry-1.docker.io" or "https://index.docker.io/v1/" ? "docker.io" : registry.ToLowerInvariant();
}
