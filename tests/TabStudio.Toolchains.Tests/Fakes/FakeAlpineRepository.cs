using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Tests.Fakes;

/// <summary>
/// An Alpine Linux mirror in memory, in Alpine's real formats: a minimal root file system, and repositories with
/// signed indexes and packages made of signature, control and data streams. It is signed with a key of its own,
/// which its root file system trusts, as Alpine's own minirootfs trusts Alpine's keys.
/// </summary>
internal sealed class FakeAlpineRepository : IDisposable
{
    /// <summary>The name of the repository's signing key, as signatures and <c>etc/apk/keys</c> name it.</summary>
    public const string KeyName = "tabstudio-tests-61f0a1b2.rsa.pub";

    public const string Architecture = "x86_64";

    private readonly RSA _key = RSA.Create(2048);
    private readonly Dictionary<string, List<(FakePackage Package, string Entry)>> _repositories = new(StringComparer.Ordinal);

    public FakeAlpineRepository()
    {
        Distribution = new LinuxDistribution
        {
            Name = "Alpine Linux",
            Version = "3.24.3",
            Branch = "v3.24",
            Mirror = new Uri("https://mirror.example.org/alpine"),
            Repositories = ["main", "community"],
        };
        foreach (var repository in Distribution.Repositories)
        {
            _repositories[repository] = [];
        }

        Rootfs = BuildRootfs();
        Distribution = Distribution with
        {
            Rootfs = new Dictionary<string, RootfsImage>
            {
                [Architecture] = new() { File = $"alpine-minirootfs-3.24.3-{Architecture}.tar.gz", Sha256 = Convert.ToHexStringLower(SHA256.HashData(Rootfs)), Size = Rootfs.Length },
            },
        };
        Publish();
    }

    /// <summary>The distribution, as a catalog pins it.</summary>
    public LinuxDistribution Distribution { get; private set; }

    /// <summary>The root file system archive.</summary>
    public byte[] Rootfs { get; }

    /// <summary>The web server that serves the mirror.</summary>
    public FakeHttpHandler Server { get; } = new();

    /// <summary>The public key, as a PEM file in <c>etc/apk/keys</c>.</summary>
    public string PublicKeyPem => _key.ExportSubjectPublicKeyInfoPem();

    /// <summary>A catalog with the fake mirror and the given toolchains.</summary>
    public ToolchainCatalog Catalog(params ToolchainDefinition[] toolchains) =>
        new() { SchemaVersion = 1, Linux = Distribution, Toolchains = toolchains };

    /// <summary>Adds a package and publishes the repository's new index.</summary>
    public FakePackage Add(string repository, string name, string version, IReadOnlyDictionary<string, string>? files = null, string[]? depends = null, string[]? provides = null)
    {
        var package = new FakePackage(name, version, files ?? new Dictionary<string, string> { [$"usr/share/{name}/README"] = $"{name} {version}\n" }, depends ?? [], provides ?? []);
        var (bytes, checksum, installedSize) = BuildPackage(package);
        package.Bytes = bytes;
        package.Repository = repository;
        package.Address = Distribution.RepositoryAddress(repository, Architecture).AbsoluteUri.TrimEnd('/') + $"/{name}-{version}.apk";
        var entry = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"C:{checksum}\nP:{name}\nV:{version}\nA:{Architecture}\nS:{bytes.Length}\nI:{installedSize}\nT:The {name} package\nL:MIT\n")
            .Append(depends is { Length: > 0 } ? $"D:{string.Join(' ', depends)}\n" : string.Empty)
            .Append(provides is { Length: > 0 } ? $"p:{string.Join(' ', provides)}\n" : string.Empty)
            .ToString();
        _repositories[repository].RemoveAll(p => p.Package.Name == name);
        _repositories[repository].Add((package, entry));
        Server.Add(new Uri(package.Address), bytes);
        Publish(repository);
        return package;
    }

    /// <summary>The package of that name the mirror has now.</summary>
    public FakePackage Package(string name) => _repositories.Values.SelectMany(r => r).Select(p => p.Package).Last(p => p.Name == name);

    /// <summary>Serves the root file system and every repository's index.</summary>
    public void Publish()
    {
        Server.Add(Distribution.RootfsAddress(Architecture, Distribution.Rootfs[Architecture]), Rootfs);
        foreach (var repository in _repositories.Keys)
        {
            Publish(repository);
        }
    }

    /// <summary>The address of a repository's index.</summary>
    public Uri IndexAddress(string repository) => new(Distribution.RepositoryAddress(repository, Architecture), "APKINDEX.tar.gz");

    public void Dispose()
    {
        _key.Dispose();
        Server.Dispose();
    }

    private void Publish(string repository)
    {
        var text = string.Join("\n", _repositories[repository].Select(p => p.Entry));
        Server.Add(IndexAddress(repository), BuildIndex(text));
    }

    [SuppressMessage("Security", "CA5350", Justification = "Alpine's format signs with SHA-1; the fake mirror writes it.")]
    private byte[] BuildIndex(string text)
    {
        var index = Gzip(Tar(complete: true, ("DESCRIPTION", Encoding.UTF8.GetBytes("v3.24.3-test")), ("APKINDEX", Encoding.UTF8.GetBytes(text))));
        var signature = _key.SignData(index, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        return [.. Gzip(Tar(complete: false, ($".SIGN.RSA.{KeyName}", signature))), .. index];
    }

    [SuppressMessage("Security", "CA5350", Justification = "Alpine's format identifies control sections by SHA-1; the fake mirror writes it.")]
    private (byte[] Bytes, string Checksum, long InstalledSize) BuildPackage(FakePackage package)
    {
        var data = Gzip(Tar(complete: true, package.Files.Select(f => (f.Key, Encoding.UTF8.GetBytes(f.Value))).ToArray()));
        var installedSize = package.Files.Sum(f => (long)Encoding.UTF8.GetByteCount(f.Value));
        var info = string.Create(CultureInfo.InvariantCulture, $"pkgname = {package.Name}\npkgver = {package.Version}\narch = {Architecture}\nsize = {installedSize}\n")
            + string.Concat(package.Depends.Select(d => $"depend = {d}\n"))
            + $"datahash = {Convert.ToHexStringLower(SHA256.HashData(data))}\n";
        var control = Gzip(Tar(complete: false, (".PKGINFO", Encoding.UTF8.GetBytes(info))));
        var signature = Gzip(Tar(complete: false, ($".SIGN.RSA.{KeyName}", _key.SignData(control, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1))));
        return ([.. signature, .. control, .. data], "Q1" + Convert.ToBase64String(SHA1.HashData(control)), installedSize);
    }

    private byte[] BuildRootfs() => Gzip(Tar(
        complete: true,
        ("etc/alpine-release", "3.24.3\n"u8.ToArray()),
        ("etc/os-release", "NAME=\"Alpine Linux\"\nID=alpine\nVERSION_ID=3.24.3\n"u8.ToArray()),
        ($"etc/apk/keys/{KeyName}", Encoding.UTF8.GetBytes(PublicKeyPem)),
        ("etc/apk/repositories", "https://mirror.example.org/alpine/v3.24/main\n"u8.ToArray()),
        ("etc/apk/world", "alpine-baselayout\nbusybox\n"u8.ToArray()),
        ("lib/apk/db/installed", "C:Q1base=\nP:busybox\nV:1.37.0-r30\nA:x86_64\nS:500000\nI:900000\n\n"u8.ToArray()),
        ("bin/busybox", "#!/bin/sh\n"u8.ToArray()),
        ("tmp/.keep", []),
        ("root/.keep", [])));

    /// <summary>A tar stream; apk's signature and control streams are cut before the end-of-archive blocks.</summary>
    private static byte[] Tar(bool complete, params (string Name, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var writer = new TarWriter(buffer, TarEntryFormat.Ustar, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                writer.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(content),
                    Mode = name.StartsWith("bin/", StringComparison.Ordinal) ? (UnixFileMode)0b111_101_101 : (UnixFileMode)0b110_100_100,
                });
            }
        }

        var bytes = buffer.ToArray();
        return complete ? bytes : bytes[..^1024];
    }

    private static byte[] Gzip(byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(content);
        }

        return buffer.ToArray();
    }
}

/// <summary>A package in a <see cref="FakeAlpineRepository"/>.</summary>
internal sealed class FakePackage(string name, string version, IReadOnlyDictionary<string, string> files, string[] depends, string[] provides)
{
    public string Name { get; } = name;

    public string Version { get; } = version;

    public IReadOnlyDictionary<string, string> Files { get; } = files;

    public string[] Depends { get; } = depends;

    public string[] Provides { get; } = provides;

    public byte[] Bytes { get; set; } = [];

    public string Repository { get; set; } = "main";

    public string Address { get; set; } = string.Empty;
}
