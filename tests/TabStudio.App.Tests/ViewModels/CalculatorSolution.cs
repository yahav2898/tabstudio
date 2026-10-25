using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// A small solution for Test Explorer tests: a library and its xUnit test project, with tests that pass, fail and
/// are skipped. The test project restores xUnit 2.9.3 from nuget.org.
/// </summary>
internal static class CalculatorSolution
{
    /// <summary>The folder the solution is written to, under the test's temporary folder.</summary>
    public const string Folder = "CalculatorSolution";

    /// <summary>The test file, relative to the temporary folder.</summary>
    public const string TestFile = Folder + "/Calculator.Tests/ArithmeticTests.cs";

    /// <summary>The test file's source.</summary>
    public const string Tests = """
        using Xunit;

        namespace Calculator.Tests;

        public class ArithmeticTests
        {
            [Fact]
            public void Adds() => Assert.Equal(4, Arithmetic.Add(2, 2));

            [Fact]
            public void Subtracts_with_a_mistake()
            {
                Console.WriteLine("Checking 5 - 3");
                Assert.Equal(3, Arithmetic.Add(5, -3));
            }

            [Fact(Skip = "Division by zero is not designed yet")]
            public void Divides_by_zero()
            {
            }

            [Theory]
            [InlineData(1, 2, 3)]
            [InlineData(-1, 1, 0)]
            public void Adds_pairs(int a, int b, int sum) => Assert.Equal(sum, Arithmetic.Add(a, b));
        }

        """;

    /// <summary>The line of the assertion that fails.</summary>
    public static int FailingLine => LineOf("Assert.Equal(3,");

    /// <summary>Writes the solution and returns the path of its .slnx file.</summary>
    public static string Write(TempDirectory temp)
    {
        temp.Write(Folder + "/Calculator/Calculator.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        temp.Write(Folder + "/Calculator/Arithmetic.cs", """
            namespace Calculator;

            public static class Arithmetic
            {
                public static int Add(int a, int b) => a + b;
            }
            """);
        temp.Write(Folder + "/Calculator.Tests/Calculator.Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="..\Calculator\Calculator.csproj" />
              </ItemGroup>
            </Project>
            """);
        temp.Write(TestFile, Tests);
        return temp.Write(Folder + "/Calculator.slnx", """
            <Solution>
              <Project Path="Calculator/Calculator.csproj" />
              <Project Path="Calculator.Tests/Calculator.Tests.csproj" />
            </Solution>
            """);
    }

    /// <summary>The one-based line of the first occurrence of a piece of text in the test file.</summary>
    public static int LineOf(string text)
    {
        var index = Tests.IndexOf(text, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{text}' is not in the test file.");
        return Tests[..index].Count(c => c == '\n') + 1;
    }
}
