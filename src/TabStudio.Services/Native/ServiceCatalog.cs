using System.Text.RegularExpressions;
using TabStudio.Services.Compose;
using TabStudio.Toolchains.Alpine;

namespace TabStudio.Services.Native;

public enum NativeServiceKind { Postgres, Redis, Valkey, MariaDb, MongoDb, RabbitMq, Nginx, Mailpit, Minio }

public sealed record ServiceMapping(NativeServiceKind Kind, string Program, IReadOnlyList<string> Packages, IReadOnlyList<int> Ports, string? Version = null, string? Note = null);
public sealed record MappingResult(ServiceMapping? Mapping, string? UnsupportedReason)
{
    public bool Supported => Mapping is not null && UnsupportedReason is null;
}

/// <summary>Only known upstream images are mapped; arbitrary registry images are never treated as equivalent.</summary>
public static partial class ServiceCatalog
{
    public static MappingResult Map(ComposeService service, ApkIndex? index = null)
    {
        if (service.Unsupported.Count > 0) return Unsupported(string.Join(" ", service.Unsupported));
        if (service.Entrypoint is { Count: > 0 }) return Unsupported("Custom entrypoints cannot be translated to a native package. Use a remote host.");
        if (service.Image is not { Length: > 0 } image) return Unsupported("This service builds an image; run the startup project with F5, or use a remote host for this image.");
        if (image.Contains('@')) return Unsupported("Image digests cannot be reproduced with Alpine packages. Use a remote host.");
        image = image.Replace("docker.io/", "", StringComparison.Ordinal).Replace("library/", "", StringComparison.Ordinal);
        var colon = image.LastIndexOf(':');
        var name = colon >= 0 ? image[..colon] : image;
        var tag = colon >= 0 ? image[(colon + 1)..] : "latest";
        var requested = NumericVersion().Match(tag).Value;
        var major = requested.Split('.')[0];
        ServiceMapping? mapping = name switch
        {
            "postgres" => Postgres(major, index),
            "redis" => new(NativeServiceKind.Redis, "redis-server", ["redis"], [6379]),
            "valkey/valkey" or "valkey" => new(NativeServiceKind.Valkey, "valkey-server", ["valkey"], [6379]),
            "mysql" or "mariadb" => new(NativeServiceKind.MariaDb, "mariadbd", ["mariadb", "mariadb-client"], [3306], Note: name == "mysql" ? "MySQL is provided by Alpine's MariaDB (wire-compatible; not all MySQL features are equivalent)." : null),
            "mongo" or "mongodb/mongodb-community-server" => new(NativeServiceKind.MongoDb, "mongod", ["mongodb", "mongodb-tools"], [27017]),
            "rabbitmq" => new(NativeServiceKind.RabbitMq, "rabbitmq-server", ["rabbitmq-server"], tag.Contains("management", StringComparison.Ordinal) ? [5672, 15672] : [5672]),
            "nginx" => new(NativeServiceKind.Nginx, "nginx", ["nginx"], [80]),
            "axllent/mailpit" => new(NativeServiceKind.Mailpit, "mailpit", ["mailpit"], [8025, 1025]),
            "minio/minio" or "quay.io/minio/minio" => new(NativeServiceKind.Minio, "minio", ["minio"], [9000, 9001]),
            _ => null,
        };
        if (mapping is null) return Unsupported($"No native Alpine mapping for '{name}'. Use a remote host to run this image.");
        if (index is not null)
        {
            if (mapping.Kind == NativeServiceKind.Redis && index.Find("redis") is null && index.Find("valkey") is not null)
                mapping = new(NativeServiceKind.Valkey, "valkey-server", ["valkey"], [6379], Note: "Redis is provided by Valkey, which uses the Redis protocol.");
            var missing = mapping.Packages.FirstOrDefault(p => index.Find(p) is null);
            if (missing is not null) return Unsupported($"Alpine does not package '{missing}' for this release and architecture. Use a remote host.");
            var actual = index.Find(mapping.Packages[0])!.Version;
            var note = mapping.Note;
            if (requested.Length > 0 && !(actual == requested || actual.StartsWith(requested + ".", StringComparison.Ordinal) || actual.StartsWith(requested + "-", StringComparison.Ordinal)))
            {
                if (mapping.Kind == NativeServiceKind.Postgres) return Unsupported($"PostgreSQL {requested} is not available; Alpine offers {actual}. Choose an available image version or use a remote host. Existing data is never upgraded automatically.");
                note = $"{note} Image version {tag} is unavailable as a package; using Alpine {actual}.".Trim();
            }
            mapping = mapping with { Version = actual, Note = note };
        }
        if (service.Ports.Any(p => p.Protocol != "tcp")) return Unsupported("Only TCP service ports are supported locally. Use a remote host for UDP.");
        if (service.Ports.Any(p => !mapping.Ports.Contains(p.Target))) return Unsupported("This mapping cannot translate one of the requested target ports. Use a remote host or the image's default ports.");
        return new(mapping, null);
    }

    private static readonly string[] PostgresMajors = ["18", "17", "16", "15", "14"];

    private static ServiceMapping Postgres(string major, ApkIndex? index)
    {
        if (major.Length == 0)
        {
            major = PostgresMajors.FirstOrDefault(v => index?.Find("postgresql" + v) is not null) ?? "18";
        }
        return new(NativeServiceKind.Postgres, "postgres", ["postgresql" + major, "postgresql" + major + "-client"], [5432], major);
    }
    private static MappingResult Unsupported(string reason) => new(null, reason);
    [GeneratedRegex("^[0-9]+(?:\\.[0-9]+)*")] private static partial Regex NumericVersion();
}
