using System.Text.Json;
using System.Text.RegularExpressions;

namespace TabStudio.Remote;

/// <summary>Commands are user-authored shell scripts; they run only when Build or Run is pressed.</summary>
public sealed record RemoteRunProfile(string BuildCommand = "dotnet build", string RunCommand = "dotnet run --no-launch-profile", int? ForwardPort = null)
{
    public static RemoteRunProfile Load(string projectDirectory) => Profiles.Load<RemoteRunProfile>(projectDirectory, "remote-run.json") ?? new();
    public void Save(string projectDirectory)
    {
        if (ForwardPort is < 1 or > 65535) { throw new ArgumentException("The forwarded port must be between 1 and 65535."); }
        Profiles.Save(projectDirectory, "remote-run.json", this);
    }
}

/// <summary>A publish destination, stored per project without credentials.</summary>
public sealed record ServerPublishProfile(string HostName, string RemoteDirectory, string ServiceName, string? RuntimeIdentifier = null)
{
    public static ServerPublishProfile? Load(string projectDirectory) => Profiles.Load<ServerPublishProfile>(projectDirectory, "server-publish.json");
    public void Save(string projectDirectory) { Validate(); Profiles.Save(projectDirectory, "server-publish.json", this); }
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(HostName);
        var path = RemoteWorkspace.Normalize(RemoteDirectory);
        if (path == "/") { throw new ArgumentException("Choose an application directory, not the server root."); }
        if (!Regex.IsMatch(ServiceName, @"\A[a-zA-Z0-9][a-zA-Z0-9_.@-]*\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) { throw new ArgumentException("Use a systemd service name, for example my-app.service."); }
        if (RuntimeIdentifier is { } rid && !Regex.IsMatch(rid, @"\A[a-zA-Z0-9][a-zA-Z0-9.-]*\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) { throw new ArgumentException("Invalid runtime identifier."); }
    }
}

internal static class Profiles
{
    public static T? Load<T>(string directory, string name)
    {
        var path = Path.Combine(directory, ".tabstudio", name);
        return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default;
    }
    public static void Save<T>(string directory, string name, T value)
    {
        var folder = Path.Combine(directory, ".tabstudio");
        Directory.CreateDirectory(folder);
        SshHostStore.AtomicWrite(Path.Combine(folder, name), JsonSerializer.Serialize(value, SshHostStore.JsonOptions));
    }
}
