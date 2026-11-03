using TabStudio.Core.Diagnostics;
using TabStudio.Packages;

namespace TabStudio.App.Tests.RealRepositories;

/// <summary>
/// The real-repository gate retries a repository once when it failed on the network, and never when it regressed. These
/// are the failures CI has seen, word for word.
/// </summary>
public sealed class NetworkFailuresTests
{
    [Fact]
    public void Restore_errors_that_say_nuget_org_could_not_be_read_are_network_failures()
    {
        // moq (NU1301 on a 304 from nuget.org's CDN) and RestSharp (TS3001 on a dropped connection).
        Assert.True(NetworkFailures.IsNetworkDiagnostic(Error(
            PackageDiagnosticIds.SourceFailed,
            "nuget.org answered 304 (The condition specified using HTTP conditional header(s) is not met.) for https://api.nuget.org/v3-flatcontainer/microsoft.visualstudio.textmanager.interop.12.0/index.json.")));
        Assert.True(NetworkFailures.IsNetworkDiagnostic(Error(
            PackageDiagnosticIds.NetworkUnreachable,
            "Could not reach nuget.org (https://api.nuget.org/v3/index.json): An error occurred while sending the request. Check the device's internet connection; packages restored before keep working offline.")));
    }

    [Fact]
    public void Compiler_errors_are_regressions_however_many_there_are()
    {
        // MudBlazor's 1416 errors had no network error among them: the gate must fail, not retry.
        Assert.False(NetworkFailures.IsNetworkDiagnostic(Error("RZ9991", "The attribute names could not be inferred from bind attribute 'bind-Open'.")));
        Assert.False(NetworkFailures.IsNetworkDiagnostic(Error("CS0103", "The name 'context' does not exist in the current context")));

        // TS3001 is also the Debugging and Windows Bridge builders' id, for problems that are not the network's.
        Assert.False(NetworkFailures.IsNetworkDiagnostic(Error(PackageDiagnosticIds.NetworkUnreachable, "The program could not be instrumented for debugging.")));
    }

    [Theory]
    [InlineData("fatal: unable to access 'https://github.com/apple/swift-numerics.git/': Could not resolve host: github.com")]
    [InlineData("npm error code ECONNRESET")]
    [InlineData("error: failed to download from `https://static.crates.io/crates/serde/1.0.0/download`")]
    [InlineData("  × Failed to download `numpy==2.1.0`\n  ╰─▶ Request failed after 3 retries: error sending request: Connection reset by peer")]
    [InlineData("[ERROR] Failed to execute goal: Could not transfer artifact org.apache:commons:pom:1.0 from/to central (https://repo.maven.apache.org/maven2): Read timed out")]
    [InlineData("curl error 28 while downloading https://repo.packagist.org/p2/laravel/framework.json: Connection timed out after 10001 milliseconds")]
    [InlineData("App: NU1301: nuget.org answered 503 (Service Unavailable) for https://api.nuget.org/v3-flatcontainer/a/index.json.")]
    public void Toolchain_output_that_says_the_network_failed_is_found(string output) =>
        Assert.NotNull(NetworkFailures.FindInOutput("Building...\n" + output + "\nDone."));

    [Theory]
    [InlineData("*** Program crashed: Bad pointer dereference at 0xffff801652b0e720 ***")]
    [InlineData("error[E0425]: cannot find value `x` in this scope")]
    [InlineData("Tests: 0 passed, 0 failed, 0 skipped of 219: The build failed. Fix the errors in the Error List, then run the tests again.")]
    [InlineData("AssertionError: expected 3, got 4")]
    public void Output_without_a_network_failure_is_not_retried(string output) =>
        Assert.Null(NetworkFailures.FindInOutput(output));

    private static BuildDiagnostic Error(string id, string message) => new(id, DiagnosticSeverityLevel.Error, message, null, "App");
}
