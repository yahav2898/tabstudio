using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Testing;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class TestingHostPlannerTests
{
    [Fact]
    public void Rust_host_target_is_read_from_rustc_instead_of_guessed_from_android()
    {
        var target = EmbeddedHostTestPlanner.RustHostTarget("rustc 1.91.0\nbinary: rustc\nhost: aarch64-unknown-linux-musl\nrelease: 1.91.0\n");
        Assert.Equal("aarch64-unknown-linux-musl", target);
        using var temp = new TempDirectory();
        temp.Write(".cargo/config.toml", "[build]\ntarget='thumbv7em-none-eabihf'");
        var command = EmbeddedHostTestPlanner.Plan(temp.Path, new EmbeddedHostTestOptions { Format = EmbeddedTestFormat.Rust }, target);
        Assert.Equal("cargo", command.Program);
        Assert.Equal(["test", "--target", target, "--", "--color", "never"], command.Arguments);
    }

    [Theory]
    [InlineData("thumbv7em-none-eabihf")]
    [InlineData("--target=host")]
    [InlineData("host with spaces")]
    public void Bare_metal_or_invalid_host_targets_are_rejected(string target)
    {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentException>(() => EmbeddedHostTestPlanner.Plan(temp.Path,
            new EmbeddedHostTestOptions { Format = EmbeddedTestFormat.Rust, HostTarget = target }));
    }

    [Fact]
    public void Native_host_binary_paths_and_arguments_remain_separate_literals()
    {
        using var temp = new TempDirectory();
        var command = EmbeddedHostTestPlanner.Plan(temp.Path, new EmbeddedHostTestOptions
        {
            Executable = "build/tests/sensor tests", Format = EmbeddedTestFormat.Unity, Arguments = ["-n", "name; $(touch should-not-exist)"],
        });
        Assert.Equal(Path.Combine(temp.Path, "build/tests/sensor tests"), command.Program);
        Assert.Equal(["-n", "name; $(touch should-not-exist)"], command.Arguments);
    }

    [Fact]
    public void GoogleTest_gets_an_explicit_fresh_XML_path_and_cannot_override_it()
    {
        using var temp = new TempDirectory();
        var options = new EmbeddedHostTestOptions { Format = EmbeddedTestFormat.GoogleTest, Executable = "build/tests" };
        var command = EmbeddedHostTestPlanner.Plan(temp.Path, options, googleTestReport: ".tabstudio/results/new.xml");
        Assert.Contains("--gtest_output=xml:" + Path.Combine(temp.Path, ".tabstudio/results/new.xml"), command.Arguments);
        Assert.Throws<ArgumentException>(() => EmbeddedHostTestPlanner.Plan(temp.Path,
            options with { Arguments = ["--gtest_output=xml:old.xml"] }, googleTestReport: "new.xml"));
    }

    [Fact]
    public void On_target_defmt_cannot_be_mislabeled_as_a_host_test()
    {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentException>(() => EmbeddedHostTestPlanner.Plan(temp.Path,
            new EmbeddedHostTestOptions { Format = EmbeddedTestFormat.DefmtTest, Executable = "firmware.elf" }));
    }

    [Fact]
    public void Rust_arguments_cannot_undo_the_explicit_host_target()
    {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentException>(() => EmbeddedHostTestPlanner.Plan(temp.Path,
            new EmbeddedHostTestOptions
            {
                Format = EmbeddedTestFormat.Rust, HostTarget = "x86_64-unknown-linux-gnu", Arguments = ["--target=thumbv6m-none-eabi"],
            }));
    }
}
