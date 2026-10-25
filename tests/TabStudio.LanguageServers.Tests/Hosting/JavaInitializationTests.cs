using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.LanguageServers.Tests.Hosting;

/// <summary>JDT bundle paths are resolved inside initialization arrays, on both host and Android.</summary>
public sealed class JavaInitializationTests
{
    [Fact]
    public void An_environment_variable_with_a_guest_default_can_supply_a_JDT_debug_bundle()
    {
        var options = new JsonObject { ["bundles"] = new JsonArray("${env:TABSTUDIO_TEST_MISSING_DEBUG_BUNDLE:/opt/java/debug.jar}") };
        var resolved = InitializationOptions.Resolve(options, "/workspace", "/usr/bin/jdtls")!;
        Assert.Equal("/opt/java/debug.jar", (string?)resolved["bundles"]![0]);
        Assert.Equal("${env:TABSTUDIO_TEST_MISSING_DEBUG_BUNDLE:/opt/java/debug.jar}", (string?)options["bundles"]![0]);
    }
}
