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

    /// <summary>The Makefile <c>sphinx-quickstart</c> writes: it only runs <c>$(SPHINXBUILD)</c>, which is sphinx-build.</summary>
    private const string SphinxMakefile = """
        # Minimal makefile for Sphinx documentation
        #

        # You can set these variables from the command line, and also
        # from the environment for the first two.
        SPHINXOPTS    ?=
        SPHINXBUILD   ?= sphinx-build
        SOURCEDIR     = source
        BUILDDIR      = build

        # Put it first so that "make" without argument is like "make help".
        help:
        	@$(SPHINXBUILD) -M help "$(SOURCEDIR)" "$(BUILDDIR)" $(SPHINXOPTS) $(O)

        .PHONY: help Makefile

        # Catch-all target: route all unknown targets to Sphinx using the new
        # "make mode" option.  $(O) is meant as a shortcut for $(SPHINXOPTS).
        %: Makefile
        	@$(SPHINXBUILD) -M $@ "$(SOURCEDIR)" "$(BUILDDIR)" $(SPHINXOPTS) $(O)
        """;

    private readonly TempDirectory _folder = new();

    [Theory]
    [InlineData(FdMakefile, "cargo")]
    [InlineData("build:\n\tnpm run build\ntest:\n\tnpm test\n", "npm")]
    [InlineData("build:\n\tgo build ./...\n", "go")]
    [InlineData("run:\n\t@dotnet run --project src/App\n", "dotnet")]
    [InlineData("all:\n\tcd web && yarn build && cd .. && ./gradlew assemble\n", "yarn")]
    [InlineData("release:\n\tCARGO_TARGET_DIR=out \\\n\t  cargo build --release\n", "cargo")]
    [InlineData(SphinxMakefile, "sphinx-build")]
    [InlineData("MKDOCS = mkdocs\nserve:\n\t${MKDOCS} serve\n", "mkdocs")]
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
    public void A_Sphinx_documentation_folder_opened_on_its_own_is_not_a_C_project()
    {
        _folder.Write("Makefile", SphinxMakefile);
        _folder.Write("make.bat", "@ECHO OFF\n");
        _folder.Write("source/index.rst", "Welcome\n=======\n");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        Assert.DoesNotContain(projects, p => p.Toolchain.Id == "cpp");
    }

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
    public void A_package_json_that_only_holds_tooling_comes_after_the_folder_s_real_project()
    {
        // mdBook: a Rust program, with a package.json for its front end's linters.
        _folder.Write("Cargo.toml", "[package]\nname = \"mdbook\"\n");
        _folder.Write("src/main.rs", "fn main() {}\n");
        _folder.Write("package.json", """{ "name": "mdbook", "scripts": { "lint": "eslint ." }, "devDependencies": { "eslint": "^9.0.0" } }""");
        _folder.Write("eslint.config.mjs", "export default [];\n");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        Assert.Equal(["rust", "node"], projects.Select(p => p.Toolchain.Id));
    }

    [Theory]
    [InlineData("""{ "scripts": { "lint": "eslint ." } }""", true)]
    [InlineData("""{ "devDependencies": { "prettier": "3" } }""", true)]
    [InlineData("""{ "scripts": { "dev": "vite" } }""", false)]
    [InlineData("""{ "scripts": { "start": "node index.js" } }""", false)]
    [InlineData("""{ "main": "index.js" }""", false)]
    [InlineData("""{ "bin": { "tool": "cli.js" } }""", false)]
    [InlineData("not json", false)]
    public void A_package_json_only_holds_tooling_when_nothing_in_it_runs(string json, bool expected)
    {
        _folder.Write("package.json", json);

        Assert.Equal(expected, ToolchainProjectDetector.IsToolingOnly(Path.Combine(_folder.Path, "package.json")));
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
