using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Tests;

public sealed class MsBuildConditionsTests
{
    private static readonly Dictionary<string, string> Properties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IncludePollyUsings"] = "true",
        ["TargetFramework"] = "net8.0",
        ["ProjectType"] = "Benchmark",
    };

    [Theory]
    [InlineData("'$(IncludePollyUsings)' == 'true'", true)]
    [InlineData("'$(IncludePollyUsings)' == 'TRUE'", true)]
    [InlineData("'$(Unknown)' == 'true'", false)]
    [InlineData("'$(Unknown)' == ''", true)]
    [InlineData("$(ProjectType) != ''", true)]
    [InlineData("'$(TargetFramework)' != 'net462' and '$(ProjectType)' == 'Benchmark'", true)]
    [InlineData("'$(TargetFramework)' == 'net462' or '$(ProjectType)' == 'Library'", false)]
    [InlineData("!Exists('missing.props')", true)]
    [InlineData("$([MSBuild]::IsTargetFrameworkCompatible($(TargetFramework), 'net9.0'))", null)]
    public void Simple_conditions_are_evaluated_and_others_are_not(string condition, bool? expected) =>
        Assert.Equal(expected, MsBuildConditions.Evaluate(condition, Properties, Path.GetTempPath()));
}
