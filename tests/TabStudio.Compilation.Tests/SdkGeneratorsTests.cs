using TabStudio.Core.Workspaces;

namespace TabStudio.Compilation.Tests;

/// <summary>The SDK's rules for source generators: which ones are off by default, and where interceptors may go.</summary>
public sealed class SdkGeneratorsTests
{
    private const string RequestDelegateGenerator = "/packs/Microsoft.AspNetCore.App.Ref/analyzers/dotnet/cs/Microsoft.AspNetCore.Http.RequestDelegateGenerator.dll";

    [Fact]
    public void The_request_delegate_generator_runs_only_when_the_project_turns_it_on_or_is_trimmed()
    {
        Assert.False(SdkGenerators.IsEnabled(Project(), RequestDelegateGenerator));
        Assert.True(SdkGenerators.IsEnabled(Project(("EnableRequestDelegateGenerator", "true")), RequestDelegateGenerator));
        Assert.True(SdkGenerators.IsEnabled(Project(("PublishAot", "true")), RequestDelegateGenerator));
        Assert.False(SdkGenerators.IsEnabled(Project(("PublishAot", "true"), ("EnableRequestDelegateGenerator", "false")), RequestDelegateGenerator));
    }

    [Fact]
    public void Other_generators_always_run()
    {
        Assert.True(SdkGenerators.IsEnabled(Project(), "/packs/Microsoft.NETCore.App.Ref/analyzers/dotnet/cs/System.Text.RegularExpressions.Generator.dll"));
        Assert.True(SdkGenerators.IsEnabled(Project(), "/packages/communitytoolkit.mvvm/analyzers/dotnet/roslyn4.12/cs/CommunityToolkit.Mvvm.SourceGenerators.dll"));
    }

    [Fact]
    public void Interceptors_are_allowed_in_the_namespaces_of_the_generators_that_run_and_those_the_project_lists()
    {
        var namespaces = SdkGenerators.InterceptorsNamespaces(Project(
            ("EnableRequestDelegateGenerator", "true"),
            ("InterceptorsNamespaces", "$(InterceptorsNamespaces);MyApp.Generated")));

        Assert.Contains("Microsoft.AspNetCore.Http.Generated", namespaces);
        Assert.Contains("MyApp.Generated", namespaces);
        Assert.Contains("TabStudio.Generated", namespaces);
        Assert.DoesNotContain(namespaces, name => name.StartsWith("$(", StringComparison.Ordinal));
        Assert.DoesNotContain("Microsoft.AspNetCore.Http.Generated", SdkGenerators.InterceptorsNamespaces(Project()));
    }

    private static ProjectInfo Project(params (string Name, string Value)[] properties) => new()
    {
        Name = "App",
        FilePath = "/work/App/App.csproj",
        Sdk = "Microsoft.NET.Sdk.Web",
        Properties = properties.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase),
    };
}
