namespace TabStudio.App.Services;

/// <summary>Templates for programs that run with a toolchain from the Toolchain Store: Python and Node.js.</summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A Python console program.</summary>
    public const string PythonId = "python";

    /// <summary>A Node.js console program.</summary>
    public const string NodeId = "node";

    /// <summary>A Flutter counter app.</summary>
    public const string FlutterId = "flutter";
    /// <summary>A Flutter web app.</summary>
    public const string FlutterWebId = "flutter-web";

    /// <summary>The Python and Node.js templates, for the start screen.</summary>
    private static ProjectTemplate[] ToolchainTemplates =>
    [
        new(PythonId, "Python Application", "A Python program that reads input and prints in the Output panel, run with Python from the Toolchain Store.", ["Python", "Console"]),
        new(NodeId, "Node.js Console App", "A JavaScript program for Node.js that reads input and prints in the Output panel, run with Node.js from the Toolchain Store.", ["JavaScript", "Node.js", "Console"]),
        new(FlutterId, "Flutter App", "A Material counter app. Run in the web preview; native targets require Android SDK support.", ["Dart", "Flutter"]),
        new(FlutterWebId, "Flutter Web App", "A Flutter counter app in the web preview, with Hot Restart on save.", ["Dart", "Flutter", "Web"]),
    ];

    /// <summary>Writes a toolchain project into a new folder, which is the workspace: there is no solution file.</summary>
    /// <returns>The folder, or <see langword="null"/> when the template is not a toolchain one.</returns>
    private static string? CreateToolchainProject(string templateId, string root, string name)
    {
        switch (templateId)
        {
            case FlutterId or FlutterWebId:
                return CreateFlutterProject(root, name);
            case PythonId:
                File.WriteAllText(Path.Combine(root, "main.py"), """"
                    """Press F5 to run this program with Python on your tablet.

                    Type your answer in the box under the Output panel. To use packages from PyPI,
                    list them in a requirements.txt file: TabStudio installs them into .venv.
                    """

                    import platform


                    def greet(name: str) -> str:
                        return f"Hello, {name}! This is Python {platform.python_version()} on TabStudio."


                    def main() -> None:
                        name = input("What is your name? ").strip() or "world"
                        print(greet(name))


                    if __name__ == "__main__":
                        main()

                    """");
                File.WriteAllText(Path.Combine(root, ".gitignore"), ".venv/\n__pycache__/\n");
                return root;

            case NodeId:
                var package = name.ToLowerInvariant().Replace('.', '-').Replace('_', '-');
                File.WriteAllText(Path.Combine(root, "package.json"), $$"""
                    {
                      "name": "{{package}}",
                      "version": "1.0.0",
                      "description": "A Node.js console app made with TabStudio.",
                      "type": "module",
                      "main": "index.js",
                      "scripts": {
                        "start": "node index.js"
                      },
                      "license": "MIT"
                    }

                    """);
                File.WriteAllText(Path.Combine(root, "index.js"), """
                    // Press F5 to run this program with Node.js on your tablet.
                    // Type your answer in the box under the Output panel. Packages you add to
                    // package.json are installed with npm install.
                    import { createInterface } from 'node:readline/promises';
                    import { stdin as input, stdout as output } from 'node:process';

                    const rl = createInterface({ input, output });
                    const name = (await rl.question('What is your name? ')).trim() || 'world';
                    console.log(`Hello, ${name}! This is Node.js ${process.version} on TabStudio.`);
                    rl.close();

                    """);
                File.WriteAllText(Path.Combine(root, ".gitignore"), "node_modules/\n");
                return root;

            default:
                return null;
        }
    }
}
