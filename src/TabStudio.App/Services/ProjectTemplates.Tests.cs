namespace TabStudio.App.Services;

/// <summary>
/// Unit test projects, as Visual Studio's xUnit, NUnit and MSTest templates write them: Test Explorer lists their
/// tests from source, runs them in the app, and debugs them with Debug Tests.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>An xUnit test project.</summary>
    public const string XunitId = "xunit";

    /// <summary>An NUnit test project.</summary>
    public const string NunitId = "nunit";

    /// <summary>An MSTest test project.</summary>
    public const string MstestId = "mstest";

    /// <summary>The test SDK every template references, as Visual Studio's do, so <c>dotnet test</c> runs them too.</summary>
    private const string TestSdkVersion = "17.14.1";

    private static ProjectTemplate[] TestTemplates =>
    [
        new(XunitId, "xUnit Test Project", "Unit tests with xUnit, run and debugged from Test Explorer on the tablet.", ["C#", "Test", "xUnit"]),
        new(NunitId, "NUnit Test Project", "Unit tests with NUnit, run and debugged from Test Explorer on the tablet.", ["C#", "Test", "NUnit"]),
        new(MstestId, "MSTest Test Project", "Unit tests with MSTest, run and debugged from Test Explorer on the tablet.", ["C#", "Test", "MSTest"]),
    ];

    private static string WriteTestProject(string root, string name, string templateId)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        var (packages, @namespace, file, source) = templateId switch
        {
            NunitId => (
                "    <PackageReference Include=\"NUnit\" Version=\"4.3.2\" />\n    <PackageReference Include=\"NUnit3TestAdapter\" Version=\"4.6.0\" />\n",
                "NUnit.Framework",
                "UnitTest1.cs",
                $$"""
                namespace {{name}};

                public class Tests
                {
                    private int _total;

                    [SetUp]
                    public void Setup() => _total = 0;

                    // Run it from Test Explorer, or set a breakpoint and choose Debug.
                    [Test]
                    public void Adds()
                    {
                        _total += 2;
                        Assert.That(_total, Is.EqualTo(2));
                    }
                }

                """),
            MstestId => (
                "    <PackageReference Include=\"MSTest.TestAdapter\" Version=\"3.8.3\" />\n    <PackageReference Include=\"MSTest.TestFramework\" Version=\"3.8.3\" />\n",
                "Microsoft.VisualStudio.TestTools.UnitTesting",
                "Test1.cs",
                $$"""
                namespace {{name}};

                [TestClass]
                public sealed class Test1
                {
                    // Run it from Test Explorer, or set a breakpoint and choose Debug.
                    [TestMethod]
                    public void Adds()
                    {
                        var total = 1 + 1;
                        Assert.AreEqual(2, total);
                    }
                }

                """),
            _ => (
                "    <PackageReference Include=\"xunit\" Version=\"2.9.3\" />\n    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"3.1.5\" />\n",
                "Xunit",
                "UnitTest1.cs",
                $$"""
                namespace {{name}};

                public class UnitTest1
                {
                    // Run it from Test Explorer, or set a breakpoint and choose Debug.
                    [Fact]
                    public void Adds()
                    {
                        var total = 1 + 1;
                        Assert.Equal(2, total);
                    }
                }

                """),
        };

        var project = Path.Combine(directory, name + ".csproj");
        File.WriteAllText(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="{TestSdkVersion}" />
            {packages}  </ItemGroup>

              <ItemGroup>
                <Using Include="{@namespace}" />
              </ItemGroup>

            </Project>

            """);
        File.WriteAllText(Path.Combine(directory, file), source);
        if (templateId == MstestId)
        {
            File.WriteAllText(Path.Combine(directory, "MSTestSettings.cs"), """
                [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

                """);
        }

        return project;
    }
}
