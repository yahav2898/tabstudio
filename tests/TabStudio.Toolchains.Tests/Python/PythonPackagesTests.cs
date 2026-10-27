using System.Net;
using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Python;

namespace TabStudio.Toolchains.Tests.Python;

/// <summary>Pinned requirements, the wheels their releases publish, and what compiling a package needs.</summary>
public sealed class PythonPackagesTests : IDisposable
{
    private static readonly PythonPlatform Tablet = new("aarch64", Musl: true);
    private static readonly PythonPlatform Computer = new("x86_64", Musl: false);
    private readonly TempDirectory _folder = new();

    [Fact]
    public void Requirements_pins_are_read_with_their_includes_and_without_markers_or_options()
    {
        _folder.Write("requirements.txt", """
            # The app
            -r requirements/base.txt
            Flask==3.0.0
            Flask_SQLAlchemy == 3.1.1  # the ORM
            uvicorn[standard]==0.30.1 \
                --hash=sha256:0123
            psycopg2-binary==2.9.9
            requests>=2.31
            numpy==1.21.0; python_version < "3.10"
            -e ./local
            --index-url https://example.org/simple
            """);
        _folder.Write("requirements/base.txt", "Werkzeug==3.0.1\nflask==9.9.9\n");

        var pins = PythonRequirements.Pins(_folder.Resolve("requirements.txt"));

        Assert.Equal(
            ["werkzeug 3.0.1", "flask 9.9.9", "flask-sqlalchemy 3.1.1", "uvicorn 0.30.1", "psycopg2-binary 2.9.9"],
            pins.Select(p => $"{p.Name} {p.Version}"));
        Assert.Contains("requests", PythonRequirements.NamesOf(File.ReadAllText(_folder.Resolve("requirements.txt"))));
        Assert.Equal("ruamel-yaml-clib", PythonRequirements.Normalize("ruamel.yaml_clib"));
    }

    [Fact]
    public void A_release_with_wheels_for_this_platform_up_to_a_version_installs_from_a_wheel_only_there()
    {
        var psycopg = Release("psycopg2-binary", "2.9.9", ">=3.7",
            "psycopg2-binary-2.9.9.tar.gz",
            "psycopg2_binary-2.9.9-cp311-cp311-musllinux_1_1_aarch64.whl",
            "psycopg2_binary-2.9.9-cp312-cp312-musllinux_1_1_aarch64.whl",
            "psycopg2_binary-2.9.9-cp312-cp312-manylinux_2_17_x86_64.manylinux2014_x86_64.whl");

        Assert.True(PythonWheels.InstallsWithoutBuilding(psycopg, 12, Tablet));
        Assert.True(PythonWheels.InstallsWithoutBuilding(psycopg, 11, Tablet));
        Assert.False(PythonWheels.InstallsWithoutBuilding(psycopg, 14, Tablet));
        Assert.True(PythonWheels.InstallsWithoutBuilding(psycopg, 12, Computer));
        Assert.False(PythonWheels.InstallsWithoutBuilding(psycopg, 13, Computer));
    }

    [Fact]
    public void Pure_stable_ABI_and_source_only_releases_do_not_limit_the_version()
    {
        var pure = Release("flask", "3.0.0", ">=3.8", "flask-3.0.0-py3-none-any.whl", "flask-3.0.0.tar.gz");
        var abi3 = Release("cryptography", "43.0.0", ">=3.7", "cryptography-43.0.0-cp39-abi3-musllinux_1_2_aarch64.whl");
        var source = Release("flask-mail", "0.9.1", null, "Flask-Mail-0.9.1.tar.gz");
        var otherPlatform = Release("pywin32", "306", null, "pywin32-306-cp311-cp311-win_amd64.whl");

        foreach (var minor in new[] { 9, 11, 14 })
        {
            Assert.True(PythonWheels.InstallsWithoutBuilding(pure, minor, Tablet));
            Assert.True(PythonWheels.InstallsWithoutBuilding(abi3, minor, Tablet));
            Assert.True(PythonWheels.InstallsWithoutBuilding(source, minor, Tablet));
            Assert.True(PythonWheels.InstallsWithoutBuilding(otherPlatform, minor, Tablet));
        }

        Assert.False(PythonWheels.InstallsWithoutBuilding(abi3, 8, Tablet));
        Assert.Null(PythonWheels.NewestSupported([pure, abi3, source], Tablet, [9, 10, 11, 12, 13, 14]));
    }

    [Fact]
    public void Requires_python_limits_the_version_too()
    {
        var old = Release("numpy", "1.21.0", ">=3.7,<3.11", "numpy-1.21.0.zip");

        Assert.True(PythonWheels.InstallsWithoutBuilding(old, 10, Tablet));
        Assert.False(PythonWheels.InstallsWithoutBuilding(old, 11, Tablet));
    }

    [Fact]
    public void The_newest_version_every_pinned_release_installs_on_is_chosen_with_the_releases_that_decided_it()
    {
        // microblog's pins on the tablet: multidict 6.0.4 and greenlet 3.0.1 publish musl wheels up to 3.11 and 3.12.
        var releases = new[]
        {
            Release("multidict", "6.0.4", ">=3.7", "multidict-6.0.4-cp311-cp311-musllinux_1_1_aarch64.whl", "multidict-6.0.4.tar.gz"),
            Release("greenlet", "3.0.1", ">=3.7", "greenlet-3.0.1-cp312-cp312-musllinux_1_1_aarch64.whl", "greenlet-3.0.1-cp311-cp311-musllinux_1_1_aarch64.whl"),
            Release("flask", "3.0.0", ">=3.8", "flask-3.0.0-py3-none-any.whl"),
        };

        var newest = PythonWheels.NewestSupported(releases, Tablet, [9, 10, 11, 12, 13, 14])!.Value;

        Assert.Equal(11, newest.Minor);
        Assert.Equal(["multidict", "greenlet"], newest.Limiting.Select(r => r.Name));
    }

    [Fact]
    public async Task Releases_are_read_from_the_JSON_API_and_unreachable_ones_are_left_out()
    {
        using var http = new HttpClient(new PyPiHandler());

        var releases = await PythonWheels.ReadAsync(http, [new("psycopg2-binary", "2.9.9"), new("missing", "1.0")], new Uri("https://pypi.test/pypi/"), CancellationToken.None);

        var release = Assert.Single(releases);
        Assert.Equal(">=3.7", release.RequiresPython);
        Assert.Equal(["psycopg2_binary-2.9.9-cp312-cp312-musllinux_1_1_aarch64.whl", "psycopg2-binary-2.9.9.tar.gz"], release.Files);
    }

    [Fact]
    public void The_build_table_lists_what_known_packages_compile_against()
    {
        var table = PythonBuildPackages.Default;

        Assert.Equal(["build-base", "python3-dev"], table.BuildTools);
        Assert.Contains("libpq-dev", table.For("psycopg2")!.Packages);
        Assert.Contains("pg_config", table.For("psycopg2")!.Needs, StringComparison.Ordinal);
        Assert.Contains("mariadb-dev", table.For("mysqlclient")!.Packages);
        Assert.Equal(["libxml2-dev", "libxslt-dev"], table.For("lxml")!.Packages);
        Assert.Equal(["libjpeg-turbo-dev", "zlib-dev"], table.For("Pillow")!.Packages);
        Assert.Equal(["libffi-dev"], table.For("cffi")!.Packages);
        Assert.Contains("openssl-dev", table.For("cryptography")!.Packages);
        Assert.NotNull(table.For("cryptography")!.Note);
        Assert.Null(table.For("flask"));
        Assert.Equal("libffi-dev", table.ForHeader("ffi.h"));
        Assert.Equal("libxml2-dev", table.ForHeader("<libxml/xmlversion.h>"));

        Assert.Equal(["build-base", "python3-dev", "libpq-dev", "libffi-dev"], table.PackagesFor(["psycopg2"], ["ffi.h", "unknown.h"]));
    }

    [Fact]
    public void Every_build_package_is_an_Alpine_name_without_spaces()
    {
        var table = PythonBuildPackages.Default;
        foreach (var name in table.KnownPackages)
        {
            Assert.All(table.For(name)!.Packages, package => Assert.Matches("^[a-z0-9][a-z0-9+._-]*$", package));
        }
    }

    public void Dispose() => _folder.Dispose();

    private static PythonRelease Release(string name, string version, string? requires, params string[] files) => new(name, version, requires, files);

    private sealed class PyPiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/pypi/psycopg2-binary/2.9.9/json")
            {
                const string json = """
                    {"info": {"name": "psycopg2-binary", "requires_python": ">=3.7"},
                     "urls": [
                       {"filename": "psycopg2_binary-2.9.9-cp312-cp312-musllinux_1_1_aarch64.whl", "packagetype": "bdist_wheel", "yanked": false},
                       {"filename": "psycopg2_binary-2.9.9-cp313-cp313-musllinux_1_1_aarch64.whl", "packagetype": "bdist_wheel", "yanked": true},
                       {"filename": "psycopg2-binary-2.9.9.tar.gz", "packagetype": "sdist"}
                     ]}
                    """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
