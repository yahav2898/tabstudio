using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// A small solution for Live Unit Testing: a library with code that passing tests run, code a failing test runs and
/// code no test runs, and its xUnit project. The test project restores xUnit 2.9.3 from nuget.org.
/// </summary>
internal static class PricingSolution
{
    /// <summary>The folder the solution is written to, under the test's temporary folder.</summary>
    public const string Folder = "PricingSolution";

    /// <summary>The library's file, relative to the temporary folder.</summary>
    public const string LibraryFile = Folder + "/Shop/Pricing.cs";

    /// <summary>The test file, relative to the temporary folder.</summary>
    public const string TestFile = Folder + "/Shop.Tests/PricingTests.cs";

    /// <summary>The code under test: Tax has a bug its test finds, and two lines run in no test.</summary>
    public const string Library = """
        namespace Shop;

        /// <summary>Prices of an order.</summary>
        public static class Pricing
        {
            public static int Total(int price, int quantity)
            {
                if (quantity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(quantity));
                }

                return price * quantity;
            }

            public static int Discount(int total)
            {
                if (total >= 100)
                {
                    return total * 9 / 10;
                }

                return total;
            }

            public static int Tax(int total)
            {
                var rate = 20;
                return total * rate / 100;
            }
        }

        """;

    /// <summary>The tests. The tax test expects a different rate than the code has.</summary>
    public const string Tests = """
        namespace Shop.Tests;

        public class PricingTests
        {
            [Fact]
            public void Total_multiplies_price_and_quantity() => Assert.Equal(30, Pricing.Total(10, 3));

            [Theory]
            [InlineData(200, 180)]
            [InlineData(100, 90)]
            public void Discount_takes_ten_percent_off_large_orders(int total, int expected) => Assert.Equal(expected, Pricing.Discount(total));

            [Fact]
            public void Tax_is_seventeen_percent() => Assert.Equal(17, Pricing.Tax(100));
        }

        """;

    /// <summary>Writes the solution and returns the path of its .slnx file.</summary>
    public static string Write(TempDirectory temp)
    {
        temp.Write(Folder + "/Shop/Shop.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        temp.Write(LibraryFile, Library);
        temp.Write(Folder + "/Shop.Tests/Shop.Tests.csproj", """
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
                <Using Include="Xunit" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="..\Shop\Shop.csproj" />
              </ItemGroup>
            </Project>
            """);
        temp.Write(TestFile, Tests);
        return temp.Write(Folder + "/Shop.slnx", """
            <Solution>
              <Project Path="Shop/Shop.csproj" />
              <Project Path="Shop.Tests/Shop.Tests.csproj" />
            </Solution>
            """);
    }

    /// <summary>The one-based line of the first occurrence of a piece of text in the library.</summary>
    public static int LineOf(string text)
    {
        var index = Library.IndexOf(text, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{text}' is not in the library.");
        return Library[..index].Count(c => c == '\n') + 1;
    }
}
