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

    /// <summary>nsidnev/fastapi-realworld-example-app's poetry.lock (Poetry 1.1), abridged.</summary>
    private const string RealWorldPoetryLock = """
        [[package]]
        name = "asyncpg"
        version = "0.26.0"
        description = "An asyncio PostgreSQL driver"
        category = "main"
        optional = false
        python-versions = ">=3.6.0"

        [package.extras]
        dev = ["Cython (>=0.29.24,<0.30.0)", "pytest (>=6.0)"]

        [[package]]
        name = "black"
        version = "22.6.0"
        description = "The uncompromising code formatter."
        category = "dev"
        optional = false
        python-versions = ">=3.6.2"

        [package.dependencies]
        click = ">=8.0.0"

        [[package]]
        name = "Unidecode"
        version = "1.3.4"
        description = "ASCII transliterations of Unicode text"
        category = "main"
        optional = false
        python-versions = ">=3.5"

        [[package]]
        name = "uvicorn"
        version = "0.18.2"
        description = "The lightning-fast ASGI server."
        category = "main"
        optional = false
        python-versions = ">=3.7"

        [package.dependencies]
        click = ">=7.0"
        h11 = ">=0.8"

        [[package]]
        name = "localdep"
        version = "0.1.0"
        description = ""
        category = "main"
        optional = false
        python-versions = "*"
        develop = true

        [package.source]
        type = "directory"
        url = "../localdep"

        [metadata]
        lock-version = "1.1"
        python-versions = "^3.9"
        content-hash = "0123"

        [metadata.files]
        asyncpg = [
            {file = "asyncpg-0.26.0-cp310-cp310-musllinux_1_1_x86_64.whl", hash = "sha256:00"},
        ]
        """;

    /// <summary>A uv.lock, abridged: the project itself, an index package and a Git one.</summary>
    private const string UvLock = """
        version = 1
        requires-python = ">=3.10"

        [[package]]
        name = "my-api"
        version = "0.1.0"
        source = { editable = "." }
        dependencies = [
            { name = "fastapi" },
        ]

        [package.metadata]
        requires-dist = [{ name = "fastapi", specifier = ">=0.110" }]

        [[package]]
        name = "pydantic-core"
        version = "2.16.3"
        source = { registry = "https://pypi.org/simple" }
        sdist = { url = "https://files.pythonhosted.org/pydantic_core-2.16.3.tar.gz", hash = "sha256:00", size = 368930 }
        wheels = [
            { url = "https://files.pythonhosted.org/pydantic_core-2.16.3-cp312-none-musllinux_1_1_aarch64.whl", hash = "sha256:00", size = 2000000 },
        ]

        [[package]]
        name = "forked"
        version = "1.0.0"
        source = { git = "https://example.org/forked.git?rev=main#0123" }
        """;

    [Fact]
    public void Lock_files_pin_the_projects_index_packages_but_not_its_own_or_development_ones()
    {
        Assert.Equal(
            ["asyncpg 0.26.0", "unidecode 1.3.4", "uvicorn 0.18.2"],
            PythonDependencies.LockPinsOf(RealWorldPoetryLock).Select(p => $"{p.Name} {p.Version}"));
        Assert.Equal(["pydantic-core 2.16.3"], PythonDependencies.LockPinsOf(UvLock).Select(p => $"{p.Name} {p.Version}"));
        Assert.Empty(PythonDependencies.LockPinsOf("not toml at all\n"));
    }

    [Fact]
    public void A_project_without_requirements_txt_is_pinned_by_its_lock_files()
    {
        _folder.Write("pyproject.toml", "[tool.poetry.dependencies]\npython = \"^3.9\"\nasyncpg = \"^0.26.0\"\n");
        _folder.Write("poetry.lock", RealWorldPoetryLock);
        _folder.Write("uv.lock", UvLock);

        var pins = PythonDependencies.Pins(_folder.Path, _folder.Resolve("pyproject.toml"));

        Assert.Equal(["asyncpg 0.26.0", "unidecode 1.3.4", "uvicorn 0.18.2", "pydantic-core 2.16.3"], pins.Select(p => $"{p.Name} {p.Version}"));

        // A requirements manifest's own pins come first and keep their version.
        _folder.Write("requirements.txt", "asyncpg==0.29.0\n");
        Assert.Equal("0.29.0", PythonDependencies.Pins(_folder.Path, _folder.Resolve("requirements.txt")).Single(p => p.Name == "asyncpg").Version);
    }

    [Fact]
    public void The_packages_a_project_depends_on_are_read_from_each_manifest()
    {
        Assert.Equal(
            ["fastapi", "uvicorn", "pytest", "ruff", "httpx"],
            PythonDependencies.PyProjectNamesOf("""
                [project]
                name = "api"
                requires-python = ">=3.10"
                dependencies = [
                    "fastapi>=0.110",
                    "uvicorn[standard]>=0.29; python_version >= '3.10'",
                ]

                [project.optional-dependencies]
                test = ["pytest"]

                [dependency-groups]
                dev = ["ruff", "httpx>=0.27"]
                """));
        Assert.Equal(
            ["uvicorn", "fastapi", "pydantic", "black"],
            PythonDependencies.PyProjectNamesOf("""
                [tool.poetry.dependencies]
                python = "^3.9"
                uvicorn = "^0.18.2"
                fastapi = "^0.79.1"
                pydantic = { version = "^1.9", extras = ["email", "dotenv"] }

                [tool.poetry.group.dev.dependencies]
                black = "^22.6.0"
                """));
        Assert.Equal(["flask", "gunicorn"], PythonDependencies.PipfileNamesOf("[packages]\nFlask = \"*\"\n\n[dev-packages]\ngunicorn = \"*\"\n\n[requires]\npython_version = \"3.11\"\n"));

        _folder.Write("requirements/dev.txt", "Uvicorn[standard]>=0.29\n");
        Assert.True(PythonDependencies.DependsOn(_folder.Path, "uvicorn"));
        Assert.False(PythonDependencies.DependsOn(_folder.Path, "fastapi"));
    }

    [Fact]
    public void A_release_with_Linux_wheels_only_for_another_processor_limits_the_version_to_them()
    {
        // asyncpg 0.26.0: musl and glibc wheels for x86_64 up to CPython 3.10, none for aarch64; its C source does
        // not build on 3.12, so the tablet takes 3.10 and compiles it there.
        var asyncpg = Release("asyncpg", "0.26.0", ">=3.6.0",
            "asyncpg-0.26.0-cp310-cp310-macosx_10_9_x86_64.whl",
            "asyncpg-0.26.0-cp310-cp310-manylinux_2_17_x86_64.manylinux2014_x86_64.whl",
            "asyncpg-0.26.0-cp310-cp310-musllinux_1_1_x86_64.whl",
            "asyncpg-0.26.0-cp39-cp39-musllinux_1_1_x86_64.whl",
            "asyncpg-0.26.0-cp310-cp310-win_amd64.whl",
            "asyncpg-0.26.0.tar.gz");

        Assert.True(PythonWheels.InstallsWithoutBuilding(asyncpg, 10, Tablet));
        Assert.False(PythonWheels.InstallsWithoutBuilding(asyncpg, 12, Tablet));
        var newest = PythonWheels.NewestSupported([asyncpg, Release("uvicorn", "0.18.2", ">=3.7", "uvicorn-0.18.2-py3-none-any.whl")], Tablet, [9, 10, 11, 12, 13, 14])!.Value;
        Assert.Equal(10, newest.Minor);
        Assert.Equal(["asyncpg"], newest.Limiting.Select(r => r.Name));

        // Wheels for Windows and macOS alone say nothing about building on Linux.
        Assert.True(PythonWheels.InstallsWithoutBuilding(Release("winonly", "1.0", null, "winonly-1.0-cp310-cp310-win_amd64.whl"), 12, Tablet));
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
