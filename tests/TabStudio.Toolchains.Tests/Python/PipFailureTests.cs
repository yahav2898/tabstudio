using TabStudio.Toolchains.Python;

namespace TabStudio.Toolchains.Tests.Python;

/// <summary>A failed pip install, reduced to the package, the cause and what its build was missing.</summary>
public sealed class PipFailureTests
{
    /// <summary>What pip printed on the tablet with 0.17 for microblog on Python 3.14, abridged.</summary>
    private const string Psycopg2Log = """
        Collecting psycopg2-binary==2.9.9 (from -r requirements.txt (line 40))
          Downloading psycopg2-binary-2.9.9.tar.gz (384 kB)
          Installing build dependencies: started
          Installing build dependencies: finished with status 'done'
          Getting requirements to build wheel: started
          Getting requirements to build wheel: finished with status 'error'
          error: subprocess-exited-with-error

          × Getting requirements to build wheel did not run successfully.
          │ exit code: 1
          ╰─> [34 lines of output]
              running egg_info
              writing psycopg2_binary.egg-info/PKG-INFO

              Error: pg_config executable not found.

              pg_config is required to build psycopg2 from source.  Please add the directory
              containing pg_config to the $PATH or specify the full executable path with the
              option:

                  python setup.py build_ext --pg-config /path/to/pg_config build ...

              If you prefer to avoid building psycopg2 from source, please install the PyPI
              'psycopg2-binary' package instead.
              [end of output]

          note: This error originates from a subprocess, and is likely not a problem with pip.
        ERROR: Failed to build 'psycopg2-binary' when getting requirements to build wheel
        """;

    [Fact]
    public void A_build_without_pg_config_names_the_package_its_version_and_the_missing_tool()
    {
        var failure = PipFailure.Analyze(Lines(Psycopg2Log));

        Assert.Equal(PipFailureKind.Build, failure.Kind);
        Assert.Equal("psycopg2-binary", failure.Package);
        Assert.Equal("2.9.9", failure.Version);
        Assert.Equal("PostgreSQL's pg_config was missing", failure.Reason);
        Assert.True(failure.NeedsBuildTools);
        Assert.Equal(
            "psycopg2-binary 2.9.9 could not be installed: it has no prebuilt wheel for Python 3.14 on this tablet, so pip compiled it, and PostgreSQL's pg_config was missing.",
            failure.Summary("3.14", "on this tablet"));
    }

    [Fact]
    public void A_missing_compiler_or_header_is_the_reason_and_headers_are_collected()
    {
        var compiler = PipFailure.Analyze(Lines("""
            Collecting greenlet==3.0.1
              Using cached greenlet-3.0.1.tar.gz (174 kB)
            Building wheels for collected packages: greenlet
              Building wheel for greenlet (pyproject.toml): finished with status 'error'
              error: command 'gcc' failed: No such file or directory
            Failed to build greenlet
            """));
        Assert.Equal(PipFailureKind.Build, compiler.Kind);
        Assert.Equal("greenlet", compiler.Package);
        Assert.Equal("3.0.1", compiler.Version);
        Assert.Equal("the C compiler was missing", compiler.Reason);

        var header = PipFailure.Analyze(Lines("""
            Collecting cffi==1.15.1
              Building wheel for cffi (setup.py): finished with status 'error'
                  c/_cffi_backend.c:15:10: fatal error: ffi.h: No such file or directory
                  compilation terminated.
            ERROR: Failed building wheel for cffi
            """));
        Assert.Equal("cffi", header.Package);
        Assert.Equal(["ffi.h"], header.MissingHeaders);
        Assert.Equal("the C header ffi.h was missing", header.Reason);

        var python = PipFailure.Analyze(Lines("""
            Collecting mysqlclient==2.2.4
                  MySQLdb/_mysql.c:46:10: fatal error: Python.h: No such file or directory
            ERROR: Could not build wheels for mysqlclient, which is required to install pyproject.toml-based projects
            """));
        Assert.Equal("mysqlclient", python.Package);
        Assert.Equal("Python's headers were missing", python.Reason);
    }

    [Fact]
    public void A_version_that_does_not_exist_for_this_Python_is_not_a_build_failure()
    {
        var failure = PipFailure.Analyze(Lines("""
            Collecting numpy==1.21.0
            ERROR: Ignored the following versions that require a different python version: 1.21.0 Requires-Python >=3.7,<3.11
            ERROR: Could not find a version that satisfies the requirement numpy==1.21.0 (from versions: 1.26.0, 2.0.0)
            ERROR: No matching distribution found for numpy==1.21.0
            """));

        Assert.Equal(PipFailureKind.NoMatchingVersion, failure.Kind);
        Assert.Equal("numpy", failure.Package);
        Assert.Equal("1.21.0", failure.Version);
        Assert.False(failure.NeedsBuildTools);
        Assert.StartsWith("numpy 1.21.0 could not be installed: PyPI has no release of it that installs on Python 3.14", failure.Summary("3.14", "here"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("WARNING: Retrying after connection broken by 'NewConnectionError: Failed to establish a new connection: [Errno -3] Temporary failure in name resolution'", PipFailureKind.Network)]
    [InlineData("ERROR: Could not install packages due to an OSError: [Errno 28] No space left on device", PipFailureKind.DiskFull)]
    [InlineData("ERROR: Cannot install flask==3.0.0 and werkzeug==2.0.0 because these package versions have conflicting dependencies.", PipFailureKind.Conflict)]
    [InlineData("ERROR: Something TabStudio does not know", PipFailureKind.Unknown)]
    public void Other_causes_are_recognized(string line, PipFailureKind kind)
    {
        var failure = PipFailure.Analyze(["Collecting flask==3.0.0", line]);

        Assert.Equal(kind, failure.Kind);
        Assert.False(failure.NeedsBuildTools);
        Assert.False(string.IsNullOrWhiteSpace(failure.Summary("3.12", "on this computer")));
    }

    [Fact]
    public void An_unknown_build_error_quotes_the_last_specific_error_line()
    {
        var failure = PipFailure.Analyze(Lines("""
            Collecting oldpkg==0.1
              error: subprocess-exited-with-error
                  ValueError: setup.py needs distutils, which Python 3.12 removed
              note: This error originates from a subprocess, and is likely not a problem with pip.
            ERROR: Failed to build installable wheels for some pyproject.toml based projects (oldpkg)
            """));

        Assert.Equal(PipFailureKind.Build, failure.Kind);
        Assert.Equal("oldpkg", failure.Package);
        Assert.Null(failure.Reason);
        Assert.Contains("distutils", failure.Summary("3.12", "here"), StringComparison.Ordinal);
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
