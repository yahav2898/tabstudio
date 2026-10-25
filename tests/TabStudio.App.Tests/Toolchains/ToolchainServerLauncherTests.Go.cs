using TabStudio.App.Services.Toolchains;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainServerLauncherTests
{
    [Theory]
    [InlineData("/usr/bin/dlv", true)]
    [InlineData("/usr/bin/gopls", false)]
    [InlineData("/usr/bin/python3", false)]
    public void Delve_preserves_the_real_UID_for_its_same_user_connection_check(string command, bool realIdentity)
    {
        var runtime = new FakeRuntime();
        using var process = new ToolchainServerLauncher(runtime).Start(new ServerStartInfo(command, [], "/work", new Dictionary<string, string?>()));

        Assert.Equal(realIdentity, Assert.Single(runtime.Started).Spec.UseHostIdentity);
    }
}
