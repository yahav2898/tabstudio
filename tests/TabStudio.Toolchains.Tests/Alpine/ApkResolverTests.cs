using TabStudio.Toolchains.Alpine;

namespace TabStudio.Toolchains.Tests.Alpine;

/// <summary>Reading Alpine's database format and planning installs and updates from it.</summary>
public sealed class ApkResolverTests
{
    // A slice of a real APKINDEX: python3 needs shared libraries other packages provide.
    private const string Index = """
        C:Q1aaaaaaaaaaaaaaaaaaaaaaaaaaa=
        P:musl
        V:1.2.6-r3
        A:x86_64
        S:415466
        I:1015808
        T:the musl c library (libc) implementation
        L:MIT
        p:so:libc.musl-x86_64.so.1=1

        C:Q1bbbbbbbbbbbbbbbbbbbbbbbbbbb=
        P:libffi
        V:3.5.2-r1
        A:x86_64
        S:22000
        I:53248
        D:so:libc.musl-x86_64.so.1
        p:so:libffi.so.8=8.1.4

        C:Q1ccccccccccccccccccccccccccc=
        P:python3
        V:3.14.8-r0
        A:x86_64
        S:8388608
        I:23592960
        T:A high-level scripting language
        L:PSF-2.0
        D:so:libc.musl-x86_64.so.1 so:libffi.so.8 python3-pyc-placeholder>=1
        p:cmd:python3=3.14.8-r0 cmd:python3.14=3.14.8-r0

        C:Q1ddddddddddddddddddddddddddd=
        P:python3-pyc-placeholder
        V:1-r0
        A:x86_64
        S:100
        I:200

        C:Q1eeeeeeeeeeeeeeeeeeeeeeeeeee=
        P:py3-pip
        V:26.1.2-r0
        A:x86_64
        S:3000000
        I:13000000
        D:python3 !py2-pip

        C:Q1fffffffffffffffffffffffffff=
        P:py3-pip-pyc
        V:26.1.2-r0
        A:x86_64
        S:1000000
        I:2000000
        i:py3-pip=26.1.2-r0 pyc

        C:Q1ggggggggggggggggggggggggggg=
        P:pyc
        V:3.14.8-r0
        A:x86_64
        S:1000
        I:2000
        """;

    private static readonly ApkIndex AlpineIndex = new(ApkDatabaseText.Read(Index, "main"));

    [Fact]
    public void Reads_packages_from_the_database_format()
    {
        var packages = ApkDatabaseText.Read(Index, "main");

        Assert.Equal(7, packages.Count);
        var python = packages.Single(p => p.Name == "python3");
        Assert.Equal("3.14.8-r0", python.Version);
        Assert.Equal("x86_64", python.Architecture);
        Assert.Equal(8388608, python.Size);
        Assert.Equal(23592960, python.InstalledSize);
        Assert.Equal("PSF-2.0", python.License);
        Assert.Equal("Q1ccccccccccccccccccccccccccc=", python.Checksum);
        Assert.Equal("main", python.Repository);
        Assert.Equal("python3-3.14.8-r0.apk", python.FileName);
        Assert.Equal(["so:libc.musl-x86_64.so.1", "so:libffi.so.8", "python3-pyc-placeholder>=1"], python.Depends.Select(d => d.ToString()));
        Assert.Contains(python.Provides, p => p.Name == "cmd:python3" && p.Version == "3.14.8-r0");
    }

    [Theory]
    [InlineData("python3", "python3", null, null, false)]
    [InlineData("python3>=3.14", "python3", ">=", "3.14", false)]
    [InlineData("so:libc.musl-x86_64.so.1", "so:libc.musl-x86_64.so.1", null, null, false)]
    [InlineData("python3~3.14", "python3", "~", "3.14", false)]
    [InlineData("!py2-pip", "py2-pip", null, null, true)]
    [InlineData("cmd:python3=3.14.8-r0", "cmd:python3", "=", "3.14.8-r0", false)]
    public void Parses_dependency_atoms(string atom, string name, string? operation, string? version, bool conflict)
    {
        var dependency = ApkDependency.Parse(atom);

        Assert.Equal(new ApkDependency(name, operation, version, conflict), dependency);
        Assert.Equal(atom, dependency.ToString());
    }

    [Fact]
    public void Plans_a_package_with_everything_it_needs_that_is_not_installed()
    {
        var installed = new[] { AlpineIndex.Find("musl")! };

        var plan = ApkResolver.PlanInstall(AlpineIndex, installed, ["py3-pip"]);

        Assert.Equal(["libffi", "py3-pip", "python3", "python3-pyc-placeholder"], plan.Packages.Select(p => p.Name).Order());
        Assert.Empty(plan.Missing);
        Assert.Equal(22000 + 8388608 + 100 + 3000000, plan.DownloadSize);
        Assert.Equal(53248 + 23592960 + 200 + 13000000, plan.InstalledSize);
    }

    [Fact]
    public void Adds_packages_that_install_themselves_with_others()
    {
        var installed = new[] { AlpineIndex.Find("musl")!, AlpineIndex.Find("pyc")! };

        var plan = ApkResolver.PlanInstall(AlpineIndex, installed, ["py3-pip"]);

        Assert.Contains(plan.Packages, p => p.Name == "py3-pip-pyc");
    }

    [Fact]
    public void Plans_nothing_for_what_is_installed()
    {
        var installed = ApkDatabaseText.Read(Index);

        var plan = ApkResolver.PlanInstall(AlpineIndex, installed, ["python3", "py3-pip"]);

        Assert.Empty(plan.Packages);
        Assert.Equal(0, plan.DownloadSize);
    }

    [Fact]
    public void Reports_what_no_repository_has()
    {
        var plan = ApkResolver.PlanInstall(AlpineIndex, [], ["no-such-package"]);

        Assert.Empty(plan.Packages);
        Assert.Equal(["no-such-package"], plan.Missing);
    }

    [Fact]
    public void Plans_updates_for_installed_packages_with_newer_versions()
    {
        var installed = ApkDatabaseText.Read(Index)
            .Select(p => p.Name == "python3" ? p with { Version = "3.14.7-r0" } : p)
            .Select(p => p.Name == "libffi" ? p with { Version = "3.5.1-r0" } : p)
            .ToList();

        var plan = ApkResolver.PlanUpgrade(AlpineIndex, installed, ["py3-pip"]);

        Assert.Equal(["libffi", "python3"], plan.Packages.Select(p => p.Name).Order());
        Assert.All(plan.Packages, p => Assert.Equal(AlpineIndex.Find(p.Name)!.Version, p.Version));
    }

    [Fact]
    public void Finds_the_installed_packages_a_toolchain_consists_of()
    {
        var installed = ApkDatabaseText.Read(Index);

        var closure = ApkResolver.Closure(installed, ["py3-pip"]);

        Assert.Equal(["libffi", "musl", "py3-pip", "python3", "python3-pyc-placeholder"], closure.Select(p => p.Name).Order());
    }

    [Fact]
    public void Chooses_providers_by_priority_then_version()
    {
        var index = new ApkIndex(ApkDatabaseText.Read("""
            P:busybox-binsh
            V:1.37.0-r20
            k:100
            p:/bin/sh cmd:sh=1.37.0-r20

            P:dash-binsh
            V:0.5.13-r0
            k:60
            p:/bin/sh cmd:sh=0.5.13-r0
            """));

        var candidates = index.Candidates(ApkDependency.Parse("/bin/sh"));

        Assert.Equal(["busybox-binsh", "dash-binsh"], candidates.Select(p => p.Name));
    }
}
