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
    public void Simple_conditions_are_evaluated(string condition, bool? expected) =>
        Assert.Equal(expected, MsBuildConditions.Evaluate(condition, Properties, Path.GetTempPath()));

    [Theory]
    [InlineData("$([MSBuild]::IsTargetFrameworkCompatible($(TargetFramework), 'net9.0'))", false)]
    [InlineData("$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'netstandard2.0'))", true)]
    [InlineData("$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net6.0'))", true)]
    [InlineData("$(TargetFramework.StartsWith('net4')) == 'false'", true)]
    [InlineData(" $(TargetFramework.StartsWith('net4')) ", false)]
    [InlineData("'$([MSBuild]::GetTargetFrameworkIdentifier($(TargetFramework)))' == '.NETCoreApp'", true)]
    [InlineData("$([MSBuild]::VersionGreaterThanOrEquals($([MSBuild]::GetTargetFrameworkVersion('$(TargetFramework)')), '7.0'))", true)]
    [InlineData("('$(ProjectType)' == 'Library' or '$(ProjectType)' == 'Benchmark') and !('$(TargetFramework)' == 'net462')", true)]
    [InlineData("'$(TargetFramework)' == 'net462' and $([Unknown]::Call())", false)]
    [InlineData("'$(TargetFramework)' == 'net8.0' and $([Unknown]::Call())", null)]
    [InlineData("$([System.String]::Copy('$(ProjectType)').Contains('Bench'))", true)]
    [InlineData("'$(ProjectType.ToLowerInvariant())' == 'benchmark'", true)]
    [InlineData("$([System.Version]::Parse('10.2.3').Major) >= 10", true)]
    [InlineData("'8.0' < '10.0'", true)]
    [InlineData("'$(IncludePollyUsings)'", true)]
    public void Property_functions_parentheses_and_comparisons_are_evaluated_as_MSBuild_does(string condition, bool? expected) =>
        Assert.Equal(expected, MsBuildConditions.Evaluate(condition, Properties, Path.GetTempPath()));
}
