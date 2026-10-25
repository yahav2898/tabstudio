using System.Text.RegularExpressions;

namespace TabStudio.App.Services;

/// <summary>A project template on the start screen.</summary>
/// <param name="Id">A stable identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">One sentence about what it creates.</param>
/// <param name="Tags">Language and platform tags shown under the name.</param>
public sealed record ProjectTemplate(string Id, string Name, string Description, IReadOnlyList<string> Tags);

/// <summary>
/// Creates new solutions from templates, laid out the way Visual Studio lays them out: a solution folder with
/// the <c>.slnx</c> file and one folder per project.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A console app.</summary>
    public const string ConsoleId = "console";

    /// <summary>A class library.</summary>
    public const string LibraryId = "classlib";

    /// <summary>A console app and a class library it references.</summary>
    public const string ConsoleWithLibraryId = "console-with-library";

    private readonly AppPaths _paths;

    /// <summary>Creates the template service over the app's folders.</summary>
    /// <param name="paths">Where projects are created.</param>
    public ProjectTemplates(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <summary>The available templates, most common first.</summary>
    public static IReadOnlyList<ProjectTemplate> All { get; } =
    [
        new(ConsoleId, "Console App", "A command-line program that runs on the tablet, with input and output in the Output panel.", ["C#", "Console"]),
        new(ConsoleWithLibraryId, "Console App and Class Library", "A program and a library it references, in one solution.", ["C#", "Console", "Library"]),
        new(LibraryId, "Class Library", "Reusable code for other projects to reference.", ["C#", "Library"]),
    ];

    /// <summary>Checks a project name. Names become namespaces, so they follow C# identifier rules.</summary>
    /// <param name="name">The proposed name.</param>
    /// <returns>A message describing the problem, or <see langword="null"/> when the name is valid.</returns>
    public static string? Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Enter a name for the project.";
        }

        if (name.Length > 64)
        {
            return "Use a name of 64 characters or fewer.";
        }

        return ProjectNamePattern().IsMatch(name)
            ? null
            : "Use letters, digits, dots and underscores, starting with a letter.";
    }

    /// <summary>Creates a new solution from a template.</summary>
    /// <param name="templateId">One of the template identifiers.</param>
    /// <param name="name">The solution and project name. Must pass <see cref="Validate"/>.</param>
    /// <returns>The path of the new solution file.</returns>
    /// <exception cref="ArgumentException">The name is invalid or the template is unknown.</exception>
    public string Create(string templateId, string name)
    {
        if (Validate(name) is { } problem)
        {
            throw new ArgumentException(problem, nameof(name));
        }

        var root = _paths.GetAvailableProjectDirectory(name);
        Directory.CreateDirectory(root);

        string[] projects = templateId switch
        {
            ConsoleId => [WriteConsole(root, name, reference: null)],
            LibraryId => [WriteLibrary(root, name)],
            ConsoleWithLibraryId => [WriteConsole(root, name, reference: name + ".Core"), WriteLibrary(root, name + ".Core")],
            _ => throw new ArgumentException($"Unknown template '{templateId}'.", nameof(templateId)),
        };

        var solution = Path.Combine(root, name + ".slnx");
        var entries = string.Join("\n", projects.Select(p => $"  <Project Path=\"{Path.GetRelativePath(root, p).Replace('\\', '/')}\" />"));
        File.WriteAllText(solution, $"<Solution>\n{entries}\n</Solution>\n");
        return solution;
    }

    private static string WriteConsole(string root, string name, string? reference)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);

        var referenceItem = reference is null
            ? string.Empty
            : $"""

              <ItemGroup>
                <ProjectReference Include="..\{reference}\{reference}.csproj" />
              </ItemGroup>

            """;
        var project = Path.Combine(directory, name + ".csproj");
        File.WriteAllText(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            {referenceItem}
            </Project>

            """);

        var program = reference is null
            ? """
              // Press F5 to build and run this program on your tablet.
              Console.WriteLine("Hello from TabStudio!");

              """
            : $"""
              using {reference};

              // Press F5 to build both projects and run this program on your tablet.
              Console.WriteLine(Messages.Welcome("TabStudio"));

              """;
        File.WriteAllText(Path.Combine(directory, "Program.cs"), program);
        return project;
    }

    private static string WriteLibrary(string root, string name)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);

        var project = Path.Combine(directory, name + ".csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

            </Project>

            """);

        File.WriteAllText(Path.Combine(directory, "Messages.cs"), $$"""
            namespace {{name}};

            /// <summary>Text the app shows. Add your own reusable code to this library.</summary>
            public static class Messages
            {
                /// <summary>A welcome message.</summary>
                public static string Welcome(string name) => $"Hello from {name}!";
            }

            """);
        return project;
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)*$")]
    private static partial Regex ProjectNamePattern();
}
