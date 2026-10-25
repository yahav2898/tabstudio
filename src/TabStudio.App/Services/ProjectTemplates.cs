using System.Text.RegularExpressions;
using TabStudio.App.Notebooks;

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

    /// <summary>An ASP.NET Core web app: a page and a minimal API.</summary>
    public const string WebId = "web";

    /// <summary>A C# notebook, in a folder of its own.</summary>
    public const string NotebookId = "notebook";

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
        new(WebId, "ASP.NET Core Web App", "A web page and an API, served on the tablet and previewed beside the code.", ["C#", "Web", "ASP.NET Core"]),
        new(LibraryId, "Class Library", "Reusable code for other projects to reference.", ["C#", "Library"]),
        new(NotebookId, "C# Notebook", "Cells of C# you run one at a time, with results, tables and errors right under each cell, like Polyglot Notebooks.", ["C#", "Notebook"]),
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

    /// <summary>Creates a new solution, or a notebook, from a template.</summary>
    /// <param name="templateId">One of the template identifiers.</param>
    /// <param name="name">The solution and project name. Must pass <see cref="Validate"/>.</param>
    /// <returns>The path of the new solution file, or of the notebook file for <see cref="NotebookId"/>.</returns>
    /// <exception cref="ArgumentException">The name is invalid or the template is unknown.</exception>
    public string Create(string templateId, string name)
    {
        if (Validate(name) is { } problem)
        {
            throw new ArgumentException(problem, nameof(name));
        }

        var root = _paths.GetAvailableProjectDirectory(name);
        Directory.CreateDirectory(root);
        if (templateId == NotebookId)
        {
            var notebook = Path.Combine(root, name + NotebookFiles.DefaultExtension);
            File.WriteAllText(notebook, NotebookFiles.ContentFor(notebook, title: name));
            return notebook;
        }

        string[] projects = templateId switch
        {
            ConsoleId => [WriteConsole(root, name, reference: null)],
            LibraryId => [WriteLibrary(root, name)],
            ConsoleWithLibraryId => [WriteConsole(root, name, reference: name + ".Core"), WriteLibrary(root, name + ".Core")],
            WebId => [WriteWeb(root, name)],
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

    private static string WriteWeb(string root, string name)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(directory, "wwwroot"));
        Directory.CreateDirectory(Path.Combine(directory, "Properties"));

        var project = Path.Combine(directory, name + ".csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

            </Project>

            """);

        File.WriteAllText(Path.Combine(directory, "Program.cs"), """
            // Press F5 to serve this app on your tablet; it opens in the preview beside the code.
            // Edit and save a file while it runs, and the app restarts with your change.
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapGet("/api/greeting", (string? name) => new Greeting($"Hello, {name ?? "web"}!", DateTimeOffset.Now));

            app.Run();

            internal sealed record Greeting(string Message, DateTimeOffset Time);

            """);

        File.WriteAllText(Path.Combine(directory, "wwwroot", "index.html"), $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{{name}}</title>
              <style>
                body { font-family: system-ui, sans-serif; margin: 0; padding: 32px 20px; background: #1e1e2e; color: #e6e6f0; }
                main { max-width: 520px; margin: 0 auto; }
                h1 { font-size: 1.6rem; margin: 0 0 8px; }
                p { color: #a8a8c0; line-height: 1.5; }
                input, button { font: inherit; padding: 10px 14px; border-radius: 8px; border: 1px solid #45455a; }
                input { background: #2a2a3c; color: inherit; width: 60%; }
                button { background: #7c6cf0; color: white; border: none; margin-left: 6px; }
                #answer { margin-top: 20px; font-size: 1.2rem; min-height: 1.5em; }
              </style>
            </head>
            <body>
              <main>
                <h1>{{name}}</h1>
                <p>This page is served from <code>wwwroot</code>, and the greeting comes from the API in <code>Program.cs</code>.</p>
                <form id="form">
                  <input id="name" placeholder="Your name" autocomplete="off" />
                  <button type="submit">Greet</button>
                </form>
                <div id="answer"></div>
              </main>
              <script>
                document.getElementById("form").addEventListener("submit", async event => {
                  event.preventDefault();
                  const name = document.getElementById("name").value;
                  const response = await fetch("/api/greeting?name=" + encodeURIComponent(name));
                  const greeting = await response.json();
                  document.getElementById("answer").textContent = greeting.message;
                });
              </script>
            </body>
            </html>

            """);

        File.WriteAllText(Path.Combine(directory, "appsettings.json"), """
            {
              "Logging": {
                "LogLevel": {
                  "Default": "Information",
                  "Microsoft.AspNetCore": "Warning"
                }
              },
              "AllowedHosts": "*"
            }

            """);

        File.WriteAllText(Path.Combine(directory, "Properties", "launchSettings.json"), $$"""
            {
              "profiles": {
                "{{name}}": {
                  "commandName": "Project",
                  "launchBrowser": true,
                  "applicationUrl": "http://localhost:5000",
                  "environmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                  }
                }
              }
            }

            """);
        return project;
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)*$")]
    private static partial Regex ProjectNamePattern();
}
