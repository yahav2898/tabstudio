using TabStudio.App.Services.Toolchains;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainServerLauncherTests
{
    [Fact]
    public void A_private_Delve_socket_directory_is_shared_with_the_runtime()
    {
        using var folder = new TempDirectory();
        var runtime = new FakeRuntime();
        var address = "unix:" + folder.Resolve("d");
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo("/usr/bin/dlv", ["dap", "--listen", address], "/work", new Dictionary<string, string?>()));

        Assert.Equal(["share " + folder.Path, "start /usr/bin/dlv"], runtime.Calls);
        Assert.Equal(["dap", "--listen", address], Assert.Single(runtime.Started).Spec.Arguments);
    }
}
