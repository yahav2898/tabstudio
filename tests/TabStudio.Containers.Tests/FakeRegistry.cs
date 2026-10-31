using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TabStudio.Containers.Images;
using TabStudio.Containers.Registry;

namespace TabStudio.Containers.Tests;

/// <summary>
/// A local OCI registry for tests: token authentication like Docker Hub's, a multi-platform index, manifests and
/// blobs, and a count of blob downloads.
/// </summary>
internal sealed class FakeRegistry : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, (byte[] Bytes, string MediaType)> _content = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();

    public FakeRegistry()
    {
        Port = FreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = Task.Run(ServeAsync);
    }

    public int Port { get; }
    public string Host => $"127.0.0.1:{Port}";
    public int BlobRequests { get; private set; }
    public int TokenRequests { get; private set; }
    public string? RequiredBasic { get; set; }
    public string? TokenRealm { get; set; }
    public bool CorruptBlobs { get; set; }

    public void Add(string path, byte[] bytes, string mediaType) => _content[path] = (bytes, mediaType);

    /// <summary>Publishes an image for the platforms given, each with its own layer, behind one index tag.</summary>
    public Dictionary<string, byte[]> PublishImage(string repository, string tag, params string[] architectures)
    {
        var layers = new Dictionary<string, byte[]>();
        var index = new ImageIndex { MediaType = MediaTypes.OciIndex };
        foreach (var architecture in architectures)
        {
            var tar = Layer(("etc/os-release", $"ID=test\nARCH={architecture}\n"), ("bin/hello", "#!/bin/sh\necho hello\n"));
            var gzip = Gzip(tar);
            var layerDigest = RegistryClient.Digest(gzip);
            Add($"/v2/{repository}/blobs/{layerDigest}", gzip, MediaTypes.OciLayerGzip);
            layers[architecture] = gzip;
            var config = OciJson.Write(new ImageConfigFile
            {
                Architecture = architecture,
                Config = new ImageRunConfig { Cmd = ["/bin/hello"], Env = ["PATH=/bin"], ExposedPorts = new() { ["80/tcp"] = System.Text.Json.JsonSerializer.SerializeToElement(new { }) } },
                Rootfs = new ImageRootfs { DiffIds = [RegistryClient.Digest(tar)] },
            });
            var configDigest = RegistryClient.Digest(config);
            Add($"/v2/{repository}/blobs/{configDigest}", config, MediaTypes.OciConfig);
            var manifest = OciJson.Write(new ImageManifest
            {
                MediaType = MediaTypes.OciManifest,
                Config = new Descriptor { MediaType = MediaTypes.OciConfig, Digest = configDigest, Size = config.Length },
                Layers = [new Descriptor { MediaType = MediaTypes.OciLayerGzip, Digest = layerDigest, Size = gzip.Length }],
            });
            var manifestDigest = RegistryClient.Digest(manifest);
            Add($"/v2/{repository}/manifests/{manifestDigest}", manifest, MediaTypes.OciManifest);
            index.Manifests.Add(new Descriptor { MediaType = MediaTypes.OciManifest, Digest = manifestDigest, Size = manifest.Length, Platform = OciPlatform.Linux(architecture) });
        }

        Add($"/v2/{repository}/manifests/{tag}", OciJson.Write(index), MediaTypes.OciIndex);
        return layers;
    }

    public static byte[] Layer(params (string Name, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var writer = new TarWriter(stream, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)), Mode = (UnixFileMode)0b111_101_101 };
                writer.WriteEntry(entry);
            }
        }

        return stream.ToArray();
    }

    public static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true)) gzip.Write(data);
        return output.ToArray();
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            try
            {
                Handle(context);
            }
            finally
            {
                context.Response.Close();
            }
        }
    }

    private void Handle(HttpListenerContext context)
    {
        var path = context.Request.Url!.AbsolutePath;
        var response = context.Response;
        if (path == "/token")
        {
            TokenRequests++;
            if (RequiredBasic is not null && context.Request.Headers["Authorization"] != "Basic " + RequiredBasic)
            {
                response.StatusCode = 401;
                return;
            }

            Write(response, Encoding.UTF8.GetBytes("{\"token\":\"secret-token\"}"), "application/json");
            return;
        }

        if (context.Request.Headers["Authorization"] != "Bearer secret-token")
        {
            response.StatusCode = 401;
            response.Headers["WWW-Authenticate"] = $"Bearer realm=\"{TokenRealm ?? $"http://{Host}/token"}\",service=\"fake\"";
            return;
        }

        if (path == "/v2/")
        {
            response.StatusCode = 200;
            return;
        }

        if (!_content.TryGetValue(path, out var item))
        {
            response.StatusCode = 404;
            Write(response, Encoding.UTF8.GetBytes("{\"errors\":[{\"code\":\"MANIFEST_UNKNOWN\",\"message\":\"manifest unknown\"}]}"), "application/json");
            return;
        }

        var bytes = item.Bytes;
        if (path.Contains("/blobs/", StringComparison.Ordinal))
        {
            BlobRequests++;
            if (CorruptBlobs && item.MediaType == MediaTypes.OciLayerGzip)
            {
                bytes = [.. bytes];
                bytes[^1] ^= 0xFF;
            }
        }

        Write(response, bytes, item.MediaType);
    }

    private static void Write(HttpListenerResponse response, byte[] bytes, string mediaType)
    {
        response.ContentType = mediaType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
    }
}

