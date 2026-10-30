namespace TabStudio.App.Services;

/// <summary>Templates for programs that run with a toolchain from the Toolchain Store: Python and Node.js.</summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A Python console program.</summary>
    public const string PythonId = "python";

    /// <summary>A Node.js console program.</summary>
    public const string NodeId = "node";

    /// <summary>A PIC18 LED blink, built with the official XC8 compiler.</summary>
    public const string Pic18Id = "pic18-xc8";

    /// <summary>A PIC32MX LED blink, built with the official XC32 compiler.</summary>
    public const string Pic32Id = "pic32-xc32";

    /// <summary>A Go console program.</summary>
    public const string GoId = "go";

    /// <summary>A Rust console program.</summary>
    public const string RustId = "rust";

    /// <summary>A CMake C++ console program.</summary>
    public const string CppId = "cpp";

    /// <summary>A Java Maven console program.</summary>
    public const string JavaId = "java";

    /// <summary>A static web site: an HTML page with a style sheet and a script, served on the tablet.</summary>
    public const string StaticSiteId = "static-site";

    /// <summary>A Kotlin Android application.</summary>
    public const string AndroidId = "android-kotlin";

    /// <summary>A Capacitor app with a Vite TypeScript web part.</summary>
    public const string CapacitorId = "capacitor";

    /// <summary>A Flutter counter app.</summary>
    public const string FlutterId = "flutter";

    /// <summary>A Flutter web app.</summary>
    public const string FlutterWebId = "flutter-web";

    /// <summary>The installed-language and web/mobile templates, for the start screen.</summary>
    private static ProjectTemplate[] ToolchainTemplates =>
    [
        .. EmbeddedTemplates,
        new(PythonId, "Python Application", "A Python program that reads input and prints in the Output panel, run with Python from the Toolchain Store.", ["Python", "Console"]),
        new(NodeId, "Node.js Console App", "A JavaScript program for Node.js that reads input and prints in the Output panel, run with Node.js from the Toolchain Store.", ["JavaScript", "Node.js", "Console"]),
        new(Pic18Id, "PIC18 Blink (XC8)", "PIC18F14K22, 16 MHz internal oscillator, LED on RC0. Builds with XC8 and simulates with gpsim on a 64-bit tablet.", ["C", "Microchip", "PIC18"]),
        new(Pic32Id, "PIC32 Blink (XC32)", "PIC32MX250F128B, internal FRC oscillator, LED on RB0. Builds with XC32 on a 64-bit tablet.", ["C", "Microchip", "PIC32"]),

        new(GoId, "Go Console App", "A Go module with console input and output, gopls IntelliSense and Delve debugging.", ["Go", "Console"]),

        new(RustId, "Rust Console App", "A Cargo console app with rust-analyzer IntelliSense and LLDB debugging.", ["Rust", "Console"]),

        new(CppId, "C++ Console App (CMake)", "A C++ console app with CMake, clangd IntelliSense and LLDB debugging.", ["C++", "CMake", "Console"]),

        new(JavaId, "Java Console App (Maven)", "A Java 21 Maven app with JDT IntelliSense and java-debug debugging.", ["Java", "Maven", "Console"]),
        .. KotlinTemplates,
        .. PhpTemplates,
        .. SwiftTemplates,
        new(StaticSiteId, "Static Web Site", "An HTML page with a style sheet and a script, served on the tablet and previewed beside the code, reloading as you save.", ["HTML", "CSS", "JavaScript", "Web"]),
        new(AndroidId, "Android App (Kotlin)", "Build and install a Kotlin Android app directly on this tablet with the Android SDK from the Toolchain Store.", ["Kotlin", "Android"]),
        new(CapacitorId, "Capacitor App", "A Vite and TypeScript web app with a live web preview, Android builds on the tablet, and iOS builds through GitHub Actions.", ["TypeScript", "Android", "iOS", "Web"]),
        new(FlutterId, "Flutter App", "A Material counter app. Run in the web preview; native targets require Android SDK support.", ["Dart", "Flutter"]),
        new(FlutterWebId, "Flutter Web App", "A Flutter counter app in the web preview, with Hot Restart on save.", ["Dart", "Flutter", "Web"]),
    ];

    /// <summary>Writes a toolchain project into a new folder, which is the workspace: there is no solution file.</summary>
    /// <returns>The folder, or <see langword="null"/> when the template is not a toolchain one.</returns>
    private static string? CreateToolchainProject(string templateId, string root, string name)
    {
        if (CreateEmbeddedProject(templateId, root, name) is { } embedded)
        {
            return embedded;
        }

        switch (templateId)
        {
            case AndroidId:
                TabStudio.Toolchains.Projects.MobileTemplates.CreateAndroid(root, name);
                return root;
            case CapacitorId:
                TabStudio.Toolchains.Projects.MobileTemplates.CreateCapacitor(root, name);
                return root;
            case Pic18Id or Pic32Id:
                var template = templateId == Pic18Id ? "PIC18Blink" : "PIC32Blink";
                var prefix = "Templates/PIC/" + template + "/";
                var assembly = typeof(ProjectTemplates).Assembly;
                foreach (var resource in assembly.GetManifestResourceNames().Where(r => r.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal)))
                {
                    var relative = resource.Replace('\\', '/')[prefix.Length..];
                    var destination = Path.Combine(root, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    using var stream = assembly.GetManifestResourceStream(resource)!;
                    using var reader = new StreamReader(stream);
                    File.WriteAllText(destination, reader.ReadToEnd().Replace(template, name, StringComparison.Ordinal));
                }

                return root;

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

            case GoId:
                return CreateGoProject(root, name);

            case RustId:
                return CreateRustProject(root, name);

            case CppId:
                return CreateCppProject(root);

            case JavaId:
                return CreateJavaProject(root, name);
            case KotlinId:
                return CreateKotlinProject(root, name);
            case KotlinScriptId:
                return CreateKotlinScript(root, name);
            case PhpId:
                return CreatePhpProject(root, name);
            case PhpSiteId:
                return CreatePhpSite(root, name);
            case SwiftId:
                return CreateSwiftProject(root, name);
            case StaticSiteId:
                WriteStaticSite(root, name);
                return root;

            default:
                return null;
        }
    }
}
