namespace TabStudio.Android.Probe;

/// <summary>
/// The small C# solution the probe compiles and runs on the device: a program and a library it references,
/// so that project references and loading a dependency are covered too.
/// </summary>
internal static class ProbeSources
{
    /// <summary>What the program must print. Anything else fails the probe.</summary>
    public const string ExpectedOutput = "Hello from TabStudio on Android! Squares: 1 4 9 16 25";

    /// <summary>The program's only source file.</summary>
    public const string Program = """
        using Greetings;

        // Compiled by Roslyn and run on this device, inside TabStudio's own process.
        var squares = Enumerable.Range(1, 5).Select(n => n * n);
        var result = new ProbeResult(Greeter.Greet("TabStudio"), [.. squares]);
        Console.WriteLine($"{result.Greeting} Squares: {string.Join(' ', result.Squares)}");

        internal sealed record ProbeResult(string Greeting, int[] Squares);

        """;

    /// <summary>The library's only source file.</summary>
    public const string Greeter = """
        namespace Greetings;

        public static class Greeter
        {
            public static string Greet(string app) =>
                $"Hello from {app} on {(OperatingSystem.IsAndroid() ? "Android" : "another OS")}!";
        }

        """;

    /// <summary>The solution file of the workspace the probe writes to disk for <c>RoslynWorkspaceHost</c>.</summary>
    public const string Solution = """
        <Solution>
          <Project Path="Probe/Probe.csproj" />
          <Project Path="Greetings/Greetings.csproj" />
        </Solution>
        """;

    /// <summary>The program's project file.</summary>
    public const string ProgramProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="..\Greetings\Greetings.csproj" />
          </ItemGroup>
        </Project>
        """;

    /// <summary>The library's project file.</summary>
    public const string LibraryProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;
}
