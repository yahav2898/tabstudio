using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// VBDev2Dev's Dots: its .NET Framework library's AssemblyInfo has AssemblyVersion("1.0.*"), which a deterministic
/// build rejects (BC37330). MSBuild builds an SDK-style project deterministically and a .NET Framework one not, unless
/// the project says.
/// </summary>
public sealed class DeterminismTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void An_SDK_project_is_deterministic_and_a_NET_Framework_project_is_not_unless_it_says()
    {
        var sdk = ProjectParser.Parse(_temp.Resolve("A/A.csproj"), """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        var sdkOff = ProjectParser.Parse(_temp.Resolve("B/B.csproj"), """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Deterministic>false</Deterministic></PropertyGroup></Project>""");
        const string Legacy = """
            <Project ToolsVersion="12.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup><OutputType>Library</OutputType><TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>DETERMINISTIC</PropertyGroup>
            </Project>
            """;
        var legacy = ProjectParser.Parse(_temp.Resolve("C/C.vbproj"), Legacy.Replace("DETERMINISTIC", string.Empty, StringComparison.Ordinal));
        var legacyOn = ProjectParser.Parse(_temp.Resolve("D/D.vbproj"), Legacy.Replace("DETERMINISTIC", "<Deterministic>true</Deterministic>", StringComparison.Ordinal));

        Assert.True(CSharpCompiler.IsDeterministic(sdk));
        Assert.False(CSharpCompiler.IsDeterministic(sdkOff));
        Assert.False(CSharpCompiler.IsDeterministic(legacy));
        Assert.True(CSharpCompiler.IsDeterministic(legacyOn));
        Assert.True(CSharpCompiler.IsDeterministic(null));
        Assert.False(TabStudio.Compilation.VisualBasic.VisualBasicCompiler.CreateCompilationOptions(legacy, AssemblyKind.Library).Deterministic);
    }

    public void Dispose() => _temp.Dispose();
}
