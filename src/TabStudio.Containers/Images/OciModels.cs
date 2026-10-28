using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.Containers.Images;

/// <summary>The media types of the OCI image and Docker distribution formats this store reads and writes.</summary>
public static class MediaTypes
{
    /// <summary>An OCI image index (a multi-platform image).</summary>
    public const string OciIndex = "application/vnd.oci.image.index.v1+json";
    /// <summary>An OCI image manifest.</summary>
    public const string OciManifest = "application/vnd.oci.image.manifest.v1+json";
    /// <summary>An OCI image configuration.</summary>
    public const string OciConfig = "application/vnd.oci.image.config.v1+json";
    /// <summary>A gzip-compressed OCI layer.</summary>
    public const string OciLayerGzip = "application/vnd.oci.image.layer.v1.tar+gzip";
    /// <summary>An uncompressed OCI layer.</summary>
    public const string OciLayer = "application/vnd.oci.image.layer.v1.tar";
    /// <summary>A zstd-compressed OCI layer.</summary>
    public const string OciLayerZstd = "application/vnd.oci.image.layer.v1.tar+zstd";
    /// <summary>A Docker manifest list (a multi-platform image).</summary>
    public const string DockerManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";
    /// <summary>A Docker image manifest, schema 2.</summary>
    public const string DockerManifest = "application/vnd.docker.distribution.manifest.v2+json";
    /// <summary>A Docker image configuration.</summary>
    public const string DockerConfig = "application/vnd.docker.container.image.v1+json";
    /// <summary>A gzip-compressed Docker layer.</summary>
    public const string DockerLayer = "application/vnd.docker.image.rootfs.diff.tar.gzip";
    /// <summary>A dev container Feature's layer: a tar with <c>install.sh</c>.</summary>
    public const string DevContainerFeatureLayer = "application/vnd.devcontainers.layer.v1+tar";

    /// <summary>What a manifest request accepts, indexes first.</summary>
    public const string ManifestAccept = OciIndex + ", " + DockerManifestList + ", " + OciManifest + ", " + DockerManifest;

    /// <summary>Whether a media type is a multi-platform index.</summary>
    public static bool IsIndex(string? mediaType) => mediaType is OciIndex or DockerManifestList;
}

/// <summary>A reference to a blob: its media type, digest and size.</summary>
public sealed record Descriptor
{
    /// <summary>The blob's media type.</summary>
    [JsonPropertyName("mediaType")] public string MediaType { get; init; } = "";
    /// <summary>The blob's SHA-256 digest, <c>sha256:…</c>.</summary>
    [JsonPropertyName("digest")] public string Digest { get; init; } = "";
    /// <summary>The blob's size in bytes.</summary>
    [JsonPropertyName("size")] public long Size { get; init; }
    /// <summary>For an index entry, the platform of the manifest it names.</summary>
    [JsonPropertyName("platform")] public OciPlatform? Platform { get; init; }
    /// <summary>Annotations, such as an attestation's reference type.</summary>
    [JsonPropertyName("annotations")] public Dictionary<string, string>? Annotations { get; init; }
}

/// <summary>An image index or manifest list: one manifest per platform.</summary>
public sealed record ImageIndex
{
    /// <summary>The schema version, 2.</summary>
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 2;
    /// <summary>The index's media type.</summary>
    [JsonPropertyName("mediaType")] public string? MediaType { get; init; }
    /// <summary>The platform manifests.</summary>
    [JsonPropertyName("manifests")] public List<Descriptor> Manifests { get; init; } = [];
}

/// <summary>An image manifest: its configuration and its layers, bottom first.</summary>
public sealed record ImageManifest
{
    /// <summary>The schema version, 2.</summary>
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 2;
    /// <summary>The manifest's media type.</summary>
    [JsonPropertyName("mediaType")] public string? MediaType { get; init; }
    /// <summary>For an artifact such as a dev container Feature, its type.</summary>
    [JsonPropertyName("artifactType")] public string? ArtifactType { get; init; }
    /// <summary>The configuration blob.</summary>
    [JsonPropertyName("config")] public Descriptor Config { get; init; } = new();
    /// <summary>The layers, bottom first.</summary>
    [JsonPropertyName("layers")] public List<Descriptor> Layers { get; init; } = [];
    /// <summary>Annotations.</summary>
    [JsonPropertyName("annotations")] public Dictionary<string, string>? Annotations { get; init; }
}

/// <summary>An image's configuration file: its platform, its run settings and its layers' uncompressed digests.</summary>
public sealed record ImageConfigFile
{
    /// <summary>The CPU architecture, such as <c>arm64</c>.</summary>
    [JsonPropertyName("architecture")] public string Architecture { get; init; } = "";
    /// <summary>The operating system, <c>linux</c>.</summary>
    [JsonPropertyName("os")] public string Os { get; init; } = "linux";
    /// <summary>The architecture variant, such as <c>v8</c> or <c>v7</c>.</summary>
    [JsonPropertyName("variant")] public string? Variant { get; init; }
    /// <summary>When the image was built.</summary>
    [JsonPropertyName("created")] public DateTimeOffset? Created { get; init; }
    /// <summary>How a container runs.</summary>
    [JsonPropertyName("config")] public ImageRunConfig Config { get; init; } = new();
    /// <summary>The layers' uncompressed digests.</summary>
    [JsonPropertyName("rootfs")] public ImageRootfs Rootfs { get; init; } = new();
    /// <summary>How each layer was made.</summary>
    [JsonPropertyName("history")] public List<ImageHistory> History { get; init; } = [];
}

/// <summary>The uncompressed digests of an image's layers.</summary>
public sealed record ImageRootfs
{
    /// <summary>Always <c>layers</c>.</summary>
    [JsonPropertyName("type")] public string Type { get; init; } = "layers";
    /// <summary>The SHA-256 of each layer's uncompressed tar, bottom first.</summary>
    [JsonPropertyName("diff_ids")] public List<string> DiffIds { get; init; } = [];
}

/// <summary>One step of an image's history.</summary>
public sealed record ImageHistory
{
    /// <summary>When the step ran.</summary>
    [JsonPropertyName("created")] public DateTimeOffset? Created { get; init; }
    /// <summary>The instruction.</summary>
    [JsonPropertyName("created_by")] public string? CreatedBy { get; init; }
    /// <summary>A comment, such as the build tool.</summary>
    [JsonPropertyName("comment")] public string? Comment { get; init; }
    /// <summary>Whether the step made no layer, such as <c>ENV</c>.</summary>
    [JsonPropertyName("empty_layer")] public bool? EmptyLayer { get; init; }
}

/// <summary>How a container from an image runs, as the image's configuration says.</summary>
public sealed record ImageRunConfig
{
    /// <summary>The user, such as <c>postgres</c>, <c>1000</c> or <c>1000:1000</c>; empty for root.</summary>
    [JsonPropertyName("User")] public string? User { get; init; }
    /// <summary>The ports the image says it listens on, such as <c>5432/tcp</c>.</summary>
    [JsonPropertyName("ExposedPorts")] public Dictionary<string, JsonElement>? ExposedPorts { get; init; }
    /// <summary>The environment, as <c>NAME=value</c>.</summary>
    [JsonPropertyName("Env")] public List<string>? Env { get; init; }
    /// <summary>The program that runs, before <see cref="Cmd"/>.</summary>
    [JsonPropertyName("Entrypoint")] public List<string>? Entrypoint { get; init; }
    /// <summary>The default arguments, or the program when there is no entrypoint.</summary>
    [JsonPropertyName("Cmd")] public List<string>? Cmd { get; init; }
    /// <summary>The working directory.</summary>
    [JsonPropertyName("WorkingDir")] public string? WorkingDir { get; init; }
    /// <summary>Labels.</summary>
    [JsonPropertyName("Labels")] public Dictionary<string, string>? Labels { get; init; }
    /// <summary>The signal that stops a container, such as <c>SIGQUIT</c>.</summary>
    [JsonPropertyName("StopSignal")] public string? StopSignal { get; init; }
    /// <summary>Folders that get an anonymous volume.</summary>
    [JsonPropertyName("Volumes")] public Dictionary<string, JsonElement>? Volumes { get; init; }
    /// <summary>The health check.</summary>
    [JsonPropertyName("Healthcheck")] public ImageHealthcheck? Healthcheck { get; init; }
    /// <summary>The shell that shell-form instructions use.</summary>
    [JsonPropertyName("Shell")] public List<string>? Shell { get; init; }
    /// <summary>Instructions to run when the image is a base of another build.</summary>
    [JsonPropertyName("OnBuild")] public List<string>? OnBuild { get; init; }
}

/// <summary>An image's health check, durations in nanoseconds as Docker writes them.</summary>
public sealed record ImageHealthcheck
{
    /// <summary><c>["CMD", …]</c>, <c>["CMD-SHELL", "…"]</c> or <c>["NONE"]</c>.</summary>
    [JsonPropertyName("Test")] public List<string>? Test { get; init; }
    /// <summary>The time between checks.</summary>
    [JsonPropertyName("Interval")] public long? Interval { get; init; }
    /// <summary>How long one check may take.</summary>
    [JsonPropertyName("Timeout")] public long? Timeout { get; init; }
    /// <summary>The time a container gets before failed checks count.</summary>
    [JsonPropertyName("StartPeriod")] public long? StartPeriod { get; init; }
    /// <summary>The time between checks during the start period.</summary>
    [JsonPropertyName("StartInterval")] public long? StartInterval { get; init; }
    /// <summary>How many failed checks make a container unhealthy.</summary>
    [JsonPropertyName("Retries")] public int? Retries { get; init; }
}

/// <summary>The JSON settings for OCI documents.</summary>
public static class OciJson
{
    /// <summary>Indented-off, null-skipping, as registries expect.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = false,
    };

    /// <summary>Reads a document.</summary>
    public static T Read<T>(ReadOnlySpan<byte> json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"The {typeof(T).Name} is empty.");

    /// <summary>Writes a document as UTF-8.</summary>
    public static byte[] Write<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
}
