using TabStudio.App.Services.Toolchains;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainLaunchTests
{
    [Fact]
    public void Java_debug_arguments_use_the_adapters_command_line_format()
    {
        var configuration = ToolchainLaunch.Create("java", Plan("java", ["-cp", "target/classes", "com.example.App", "Ada Lovelace", "", "say \"hello\""], "/work/app", "pom.xml"))!;

        Assert.Equal("com.example.App", (string?)configuration.Properties["mainClass"]);
        Assert.Equal(["target/classes"], Strings(configuration.Properties["classPaths"]));
        Assert.Equal("\"Ada Lovelace\" \"\" \"say \\\"hello\\\"\"", (string?)configuration.Properties["args"]);
    }

    [Fact]
    public void A_Java_template_with_no_arguments_sends_an_empty_string()
    {
        var configuration = ToolchainLaunch.Create("java", Plan("java", ["-cp", "target/classes", "com.example.App"], "/work/app", "pom.xml"))!;

        Assert.Equal(string.Empty, (string?)configuration.Properties["args"]);
        Assert.Null(ToolchainLaunch.Create("java", Plan("java", ["-jar", "app.jar"], "/work/app", "app.jar")));
    }
}
