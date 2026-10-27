using TabStudio.Testing;
using TabStudio.Toolchains.Python;

namespace TabStudio.Toolchains.Tests.Python;

/// <summary>The Python version a project asks for, from each file that can name one, and the version chosen.</summary>
public sealed class PythonVersionTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    [Theory]
    [InlineData(">=3.9,<3.12", "9,10,11", null)]
    [InlineData(">= 3.10", "10,11,12,13,14", null)]
    [InlineData("^3.10", "10,11,12,13,14", null)]
    [InlineData("~3.10", "10", 10)]
    [InlineData("~=3.10", "10,11,12,13,14", null)]
    [InlineData("~=3.10.2", "10", 10)]
    [InlineData("3.11", "11", 11)]
    [InlineData("==3.11.4", "11", 11)]
    [InlineData("3.11.*", "11", 11)]
    [InlineData("3.12t", "12", 12)]
    [InlineData(">=3.10,!=3.12.*", "10,11,13,14", null)]
    [InlineData(">3.8", "9,10,11,12,13,14", null)]
    [InlineData("<=3.11", "8,9,10,11", null)]
    [InlineData(">=3.8.1,<4.0", "8,9,10,11,12,13,14", null)]
    [InlineData(">=2.7,!=3.0.*,<3.13", "8,9,10,11,12", null)]
    public void A_specifier_allows_the_minor_versions_it_names(string specifier, string allowed, int? exact)
    {
        var range = PythonVersionRange.Parse(specifier)!;

        Assert.Equal(allowed, string.Join(",", Enumerable.Range(8, 7).Where(range.Allows)));
        Assert.Equal(exact, range.ExactMinor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2.7")]
    [InlineData("==2.7.18")]
    [InlineData("^3.8 || ^3.9")]
    [InlineData("pypy3.10")]
    public void A_specifier_without_a_Python_3_version_names_none(string specifier) =>
        Assert.Null(PythonVersionRange.Parse(specifier));

    [Fact]
    public void The_python_version_file_comes_first()
    {
        _folder.Write(".python-version", "# pyenv\n3.11.9\n3.12\n");
        _folder.Write("runtime.txt", "python-3.10.4\n");
        _folder.Write("pyproject.toml", "[project]\nrequires-python = \">=3.9\"\n");

        var request = PythonVersionRequest.Find(_folder.Path)!;

        Assert.Equal(".python-version", request.Source);
        Assert.Equal(11, request.Range.ExactMinor);
    }

    [Fact]
    public void Each_file_that_can_name_a_version_is_read_in_order()
    {
        _folder.Write("Dockerfile", "FROM node:20 AS web\nFROM --platform=linux/amd64 python:3.10-slim AS app\n");
        Assert.Equal(("Dockerfile", 10), Read());

        _folder.Write("Pipfile", "[packages]\nflask = \"*\"\n\n[requires]\npython_version = \"3.9\"\n");
        Assert.Equal(("Pipfile", 9), Read());

        _folder.Write("pyproject.toml", "[tool.poetry.dependencies]\npython = \"~3.12\"\nflask = \"^3\"\n");
        Assert.Equal(("pyproject.toml", 12), Read());

        _folder.Write("pyproject.toml", "[project]\nname = \"x\"\nrequires-python = \">=3.10,<3.12\"\n[tool.other]\nrequires-python = \"3.8\"\n");
        Assert.Equal("pyproject.toml", PythonVersionRequest.Find(_folder.Path)!.Source);
        Assert.Equal(">=3.10,<3.12", PythonVersionRequest.Find(_folder.Path)!.Range.Text);

        _folder.Write("runtime.txt", "python-3.13.1\n");
        Assert.Equal(("runtime.txt", 13), Read());
    }

    [Theory]
    [InlineData("FROM python:slim\n")]
    [InlineData("FROM python:3\n")]
    [InlineData("FROM python:latest AS base\n")]
    [InlineData("FROM ubuntu:24.04\nRUN apt-get install -y python3\n")]
    public void A_Dockerfile_without_a_minor_version_names_none(string dockerfile)
    {
        _folder.Write("Dockerfile", dockerfile);

        Assert.Null(PythonVersionRequest.Find(_folder.Path));
    }

    [Fact]
    public void An_exact_request_is_used_as_asked()
    {
        var choice = PythonVersionChoice.Choose(Request("3.10"), pins: (12, []), systemMinor: 14)!;

        Assert.Equal("3.10", choice.Version);
        Assert.False(choice.UsesSystemPython);
        Assert.Contains(".python-version", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_request_the_pins_choose_the_newest_Python_they_install_on_from_wheels()
    {
        var limiting = new PythonRelease("psycopg2-binary", "2.9.9", ">=3.7", []);

        var choice = PythonVersionChoice.Choose(null, pins: (11, [limiting]), systemMinor: 14)!;

        Assert.Equal(11, choice.Minor);
        Assert.False(choice.UsesSystemPython);
        Assert.Contains("psycopg2-binary 2.9.9", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_range_and_the_pins_both_limit_the_choice()
    {
        var limiting = new PythonRelease("numpy", "1.26.4", ">=3.9", []);

        Assert.Equal(11, PythonVersionChoice.Choose(Request(">=3.9,<3.13"), (11, [limiting]), 14)!.Minor);
        Assert.Equal(12, PythonVersionChoice.Choose(Request(">=3.9,<3.13"), (13, [limiting]), 14)!.Minor);

        // When the pins allow nothing the range does, the range decides, with the toolchain's own Python if it fits.
        Assert.Equal(14, PythonVersionChoice.Choose(Request(">=3.13"), (11, [limiting]), 14)!.Minor);
    }

    [Fact]
    public void Nothing_to_decide_keeps_the_toolchains_Python()
    {
        Assert.Null(PythonVersionChoice.Choose(null, null, 14));
        Assert.Null(PythonVersionChoice.Choose(null, (14, [new PythonRelease("x", "1", null, [])]), 14));

        var choice = PythonVersionChoice.Choose(Request(">=3.9"), null, 14)!;
        Assert.True(choice.UsesSystemPython);
        Assert.Equal(14, choice.Minor);
    }

    public void Dispose() => _folder.Dispose();

    private (string Source, int? Minor) Read() =>
        PythonVersionRequest.Find(_folder.Path) is { } request ? (request.Source, request.Range.ExactMinor) : ("none", null);

    private static PythonVersionRequest Request(string specifier) => new(PythonVersionRange.Parse(specifier)!, ".python-version");
}
