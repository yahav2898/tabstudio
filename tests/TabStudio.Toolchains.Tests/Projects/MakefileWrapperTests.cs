using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>
/// A Makefile that only wraps another build tool is not a C/C++ project, a language's manifest comes before a
/// Makefile in its folder, and programs are named after their target or binary, never a build file.
/// </summary>
public sealed class MakefileWrapperTests : IDisposable
{
    /// <summary>sharkdp/fd's Makefile, a wrapper around <c>cargo build --release</c>.</summary>
    private const string FdMakefile = """
        PROFILE?=release
        EXE=target/$(PROFILE)/fd
        prefix=/usr/local
        bindir=$(prefix)/bin
        exe_name=fd
        ifdef VERSION
        	ARCHIVE_NAME = fd-v$(VERSION)
        else
        	ARCHIVE_NAME = fd
        endif
        export ARCHIVE_NAME

        $(EXE): Cargo.toml src/**/*.rs
        	cargo build --profile $(PROFILE) --locked

        .PHONY: completions
        completions: autocomplete/fd.bash

        comp_dir=@mkdir -p autocomplete

        autocomplete/fd.bash: $(EXE)
        	$(comp_dir)
        	$(EXE) --gen-completions bash > $@

        install: $(EXE) completions
        	install -Dm755 $(EXE) $(DESTDIR)$(bindir)/fd
        """;

    private readonly TempDirectory _folder = new();

    [Theory]
    [InlineData(FdMakefile, "cargo")]
    [InlineData("build:\n\tnpm run build\ntest:\n\tnpm test\n", "npm")]
    [InlineData("build:\n\tgo build ./...\n", "go")]
    [InlineData("run:\n\t@dotnet run --project src/App\n", "dotnet")]
    [InlineData("all:\n\tcd web && yarn build && cd .. && ./gradlew assemble\n", "yarn")]
    [InlineData("release:\n\tCARGO_TARGET_DIR=out \\\n\t  cargo build --release\n", "cargo")]
    [InlineData("build:\n\t$(WITH_LOCAL_JWT) docker compose build\nrun-local:\n\t$(WITH_LOCAL_JWT) ASPNETCORE_URLS=$(API_URL) dotnet run --project src/App\n", "docker")]
    public void A_Makefile_that_runs_another_build_tool_wraps_it(string makefile, string tool) =>
        Assert.Equal(tool, MakefileWrapper.WrappedToolOf(makefile));

    [Theory]
    [InlineData("CC=gcc\nhello: hello.o\n\t$(CC) -o hello hello.o\n")]
    [InlineData("%.o: %.c\n\tcc -c $< -o $@\n")]
    [InlineData("app: main.c util.c\n\tclang -o app main.c util.c\n")]
    [InlineData("hello: main.o\n")]
    [InlineData("build:\n\tcargo build\n\tgcc -shared -o libshim.so shim.c\n")]
    [InlineData("SRCS = main.cpp\ntest:\n\tpython3 tests.py\n")]
    [InlineData("all:\n\tcmake --build build\n")]
    [InlineData("")]
    public void A_Makefile_that_builds_C_or_C_plus_plus_itself_wraps_nothing(string makefile) =>
        Assert.Null(MakefileWrapper.WrappedToolOf(makefile));

    [Fact]
    public void A_Cargo_repository_with_a_wrapper_Makefile_has_only_its_Cargo_project()
    {
        _folder.Write("Cargo.toml", "[package]\nname = \"fd-find\"\nversion = \"10.3.0\"\n\n[[bin]]\nname = \"fd\"\npath = \"src/main.rs\"\n");
        _folder.Write("src/main.rs", "fn main() {}\n");
        _folder.Write("Makefile", FdMakefile);

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        var project = Assert.Single(projects);
        Assert.Equal("rust", project.Toolchain.Id);
        Assert.Equal("Cargo.toml", project.Marker);
    }

    [Fact]
    public void A_folder_with_a_language_manifest_and_a_real_C_Makefile_lists_the_manifest_first()
    {
        _folder.Write("Cargo.toml", "[package]\nname = \"hybrid\"\n");
        _folder.Write("src/main.rs", "fn main() {}\n");
        _folder.Write("Makefile", "CC=cc\nshim: shim.c\n\t$(CC) -o shim shim.c\n");
        _folder.Write("shim.c", "int main(void) { return 0; }\n");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        Assert.Equal(["rust", "cpp"], projects.Select(p => p.Toolchain.Id));
    }

    [Fact]
    public void A_C_project_with_only_a_Makefile_is_still_a_C_project()
    {
        _folder.Write("Makefile", "hello: hello.c\n\tcc -g -o hello hello.c\n");
        _folder.Write("hello.c", "int main(void) { return 0; }\n");

        var project = Assert.Single(ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default));

        Assert.Equal("cpp", project.Toolchain.Id);
        Assert.Equal("Makefile", project.Marker);
    }

    [Fact]
    public void Programs_are_named_after_their_binary_or_project_and_never_a_build_file()
    {
        var folder = Path.Combine(_folder.Path, "fd");
        var spec = new ProcessStartSpec { Program = Path.Combine(folder, ".tabstudio", "bin", "app"), WorkingDirectory = folder };

        // Before make reports the binary, the program is the project; after, the binary.
        var plan = new RunPlan(spec with { Program = "make" }, spec, "Makefile");
        Assert.Equal("fd", plan.ProgramName);
        Assert.Equal("fd", (plan with { Run = spec with { Program = Path.Combine(folder, "target", "release", "fd") } }).ProgramName);
        Assert.Equal("hello", new RunPlan(null, spec with { Program = Path.Combine(folder, "build", "hello") }, "CMakeLists.txt").ProgramName);
        Assert.Equal("fd", new RunPlan(null, spec with { Program = "java" }, "pom.xml").ProgramName);

        // A script or a command keeps its own name, and an explicit name wins.
        Assert.Equal("main.py", new RunPlan(null, spec with { Program = "python3" }, "main.py").ProgramName);
        Assert.Equal("npm start", new RunPlan(null, spec with { Program = "npm" }, "npm start").ProgramName);
        Assert.Equal("fd", (new RunPlan(null, spec, "fd-find") with { ProgramName = "fd" }).ProgramName);
        Assert.True(RunPlan.IsBuildFile("Makefile"));
        Assert.False(RunPlan.IsBuildFile("main.py"));
    }

    [Fact]
    public void A_C_Makefile_run_is_named_after_its_project_folder_until_its_binary_is_known()
    {
        var root = _folder.Resolve("calculator");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Makefile"), "calc: calc.c\n\tcc -o calc calc.c\n");
        var plan = RunPlanner.PlanProject(new ToolchainProject(ToolchainCatalog.Default.Find("cpp")!, root, "Makefile"))!;

        Assert.Equal("Makefile", plan.EntryPoint);
        Assert.Equal("calculator", plan.ProgramName);
    }

    public void Dispose() => _folder.Dispose();
}
