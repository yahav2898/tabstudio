using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace TabStudio.App.Swarm.Recording;

/// <summary>
/// Writes recorded UI tests as C# a person would write: an xUnit test with <c>[AvaloniaFact]</c> whose steps tap, type
/// and check the app's elements by name, over a small driver (<c>RecordedApp</c>) that starts the app headlessly at the
/// recorded size, as TabStudio's own UI tests run. The tests run in TabStudio's Test Explorer, on every screen of the
/// swarm, and in CI with <c>dotnet test</c>.
/// </summary>
public static class UiTestWriter
{
    /// <summary>The category of recorded UI tests, by which TabStudio runs them on every screen.</summary>
    public const string Category = "Recorded UI test";

    /// <summary>The file of the driver in the test project.</summary>
    public const string DriverFileName = "RecordedApp.cs";

    /// <summary>The file that starts Avalonia for the tests, in the test project.</summary>
    public const string AppBuilderFileName = "TestAppBuilder.cs";

    /// <summary>The folder of the recorded tests in the test project.</summary>
    public const string Folder = "Recorded";

    private static readonly char[] WordSeparators = [' ', '-', '_', '.', ',', ':', ';', '/', '\\', '\'', '"', '(', ')', '!', '?'];

    /// <summary>The test's class name, such as <c>CountingThreeTapsTest</c> for <c>Counting three taps</c>.</summary>
    /// <param name="testName">The test's name.</param>
    public static string ClassName(string testName)
    {
        ArgumentNullException.ThrowIfNull(testName);
        var builder = new StringBuilder();
        foreach (var word in Words(testName))
        {
            builder.Append(char.ToUpperInvariant(word[0])).Append(word.AsSpan(1));
        }

        var name = builder.Length == 0 ? "Recorded" : builder.ToString();
        return (char.IsDigit(name[0]) ? "Test" + name : name) + "Test";
    }

    /// <summary>The test method's name, as a sentence: <c>Counting_three_taps</c>.</summary>
    /// <param name="testName">The test's name.</param>
    public static string MethodName(string testName)
    {
        ArgumentNullException.ThrowIfNull(testName);
        var words = Words(testName).ToList();
        if (words.Count == 0)
        {
            return "Replays_the_recording";
        }

        words[0] = char.ToUpperInvariant(words[0][0]) + words[0][1..];
        var name = string.Join('_', words);
        return char.IsDigit(name[0]) ? "Test_" + name : name;
    }

    /// <summary>The C# of one recorded test.</summary>
    /// <param name="namespaceName">The test project's namespace.</param>
    /// <param name="recording">The recording.</param>
    public static string Test(string namespaceName, UiTestRecording recording)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentNullException.ThrowIfNull(recording);
        var profile = recording.Profile;
        var size = string.Create(CultureInfo.InvariantCulture, $"{profile.Width:0} × {profile.Height:0}");
        var code = new StringBuilder();
        code.Append(CultureInfo.InvariantCulture, $$"""
            using Avalonia.Headless.XUnit;
            using Avalonia.Input;

            namespace {{namespaceName}};

            /// <summary>Recorded in TabStudio on {{Escape(profile.Name)}} ({{size}}). It replays on every screen of the swarm and in CI.</summary>
            public sealed class {{ClassName(recording.Name)}}
            {
                [AvaloniaFact]
                [Trait("Category", "{{Category}}")]
                public void {{MethodName(recording.Name)}}()
                {
                    using var app = RecordedApp.Start(width: {{Number(profile.Width)}}, height: {{Number(profile.Height)}});

            """);
        code.Append('\n');
        foreach (var step in recording.Steps)
        {
            code.Append("        ").Append(Statement(step)).Append('\n');
        }

        code.Append("""
                }
            }

            """);
        return code.ToString();
    }

    /// <summary>The C# statement of a step, such as <c>app.Tap("CountButton");</c>.</summary>
    /// <param name="step">The step.</param>
    public static string Statement(UiTestStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var element = Literal(ElementPaths.ShortName(step.ElementPath));
        return step.Kind switch
        {
            UiTestStepKind.Tap => $"app.Tap({element});",
            UiTestStepKind.LongPress => $"app.LongPress({element});",
            UiTestStepKind.Scroll => $"app.Scroll({element}, deltaX: {Number(step.ScrollX)}, deltaY: {Number(step.ScrollY)});",
            UiTestStepKind.Type => $"app.Type({Literal(step.Text ?? string.Empty)});",
            UiTestStepKind.Press => step.Modifiers is { Length: > 0 } modifiers
                ? $"app.Press(Key.{step.Key}, {string.Join(" | ", modifiers.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(m => "KeyModifiers." + m))});"
                : $"app.Press(Key.{step.Key});",
            _ => $"app.ShouldShow({element}, {Literal(step.Text ?? string.Empty)});",
        };
    }

    /// <summary>The C# of the driver the recorded tests use.</summary>
    /// <param name="namespaceName">The test project's namespace.</param>
    /// <param name="appClass">The full name of the app's <c>Application</c> class, such as <c>HelloAvalonia.App</c>.</param>
    public static string Driver(string namespaceName, string appClass)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentNullException.ThrowIfNull(appClass);
        return UiTestDriverSource.Text.Replace("$NAMESPACE$", namespaceName, StringComparison.Ordinal).Replace("$APP$", appClass, StringComparison.Ordinal);
    }

    /// <summary>The C# that starts Avalonia's headless platform with the app, as <c>dotnet test</c> runs the tests.</summary>
    /// <param name="namespaceName">The test project's namespace.</param>
    /// <param name="appClass">The full name of the app's <c>Application</c> class, such as <c>HelloAvalonia.App</c>.</param>
    public static string AppBuilder(string namespaceName, string appClass)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentNullException.ThrowIfNull(appClass);
        return $$"""
            using Avalonia;
            using Avalonia.Headless;

            [assembly: AvaloniaTestApplication(typeof({{namespaceName}}.TestAppBuilder))]

            namespace {{namespaceName}};

            /// <summary>Starts Avalonia with the app, without a screen, for the UI tests; each test shows the app's view itself (see RecordedApp).</summary>
            public static class TestAppBuilder
            {
                public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<global::{{appClass}}>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
            }

            """;
    }

    /// <summary>
    /// The project file of a new test project for an app, without its packages, which <see cref="Packages"/> lists:
    /// they are added the way TabStudio adds every package, so central package management keeps working.
    /// </summary>
    /// <param name="appProject">The app's project file, relative to the test project's folder.</param>
    public static string Project(string appProject)
    {
        ArgumentNullException.ThrowIfNull(appProject);
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>

              <ItemGroup>
                <ProjectReference Include="{appProject.Replace('/', '\\')}" />
              </ItemGroup>

              <ItemGroup>
                <Using Include="Xunit" />
              </ItemGroup>

            </Project>

            """;
    }

    /// <summary>The packages recorded UI tests need: xUnit, its runner, and Avalonia's headless platform for xUnit.</summary>
    /// <param name="avaloniaVersion">The Avalonia version the app uses.</param>
    public static IReadOnlyList<(string Id, string Version)> Packages(string avaloniaVersion)
    {
        ArgumentNullException.ThrowIfNull(avaloniaVersion);
        return
        [
            ("Avalonia.Headless.XUnit", avaloniaVersion),
            ("Microsoft.NET.Test.Sdk", "17.14.1"),
            ("xunit", "2.9.3"),
            ("xunit.runner.visualstudio", "3.1.5"),
        ];
    }

    private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    private static IEnumerable<string> Words(string text) =>
        text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => string.Concat(word.Where(char.IsLetterOrDigit)))
            .Where(word => word.Length > 0);
}
