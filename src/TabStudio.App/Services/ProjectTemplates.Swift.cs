namespace TabStudio.App.Services;

/// <summary>
/// Swift: a Swift package with a command-line program, a library it uses and Swift Testing tests, built with SwiftPM
/// from the official Swift toolchain in TabStudio's glibc environment.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A Swift package: an executable, a library and its tests.</summary>
    public const string SwiftId = "swift";

    private static ProjectTemplate[] SwiftTemplates =>
    [
        new(SwiftId, "Swift Command-Line Tool (experimental)", "A Swift package with a program, a library and Swift Testing tests, sourcekit-lsp IntelliSense and LLDB debugging.", ["Swift", "SwiftPM", "Console"]),
    ];

    private static string CreateSwiftProject(string root, string name)
    {
        // Swift module names are identifiers.
        var module = name.Replace('.', '_');
        var library = module + "Core";
        File.WriteAllText(Path.Combine(root, "Package.swift"), $$"""
            // swift-tools-version:6.0
            import PackageDescription

            let package = Package(
                name: "{{module}}",
                targets: [
                    .target(name: "{{library}}"),
                    .executableTarget(name: "{{module}}", dependencies: ["{{library}}"]),
                    .testTarget(name: "{{library}}Tests", dependencies: ["{{library}}"]),
                ]
            )

            """);
        var executable = Path.Combine(root, "Sources", module);
        Directory.CreateDirectory(executable);
        File.WriteAllText(Path.Combine(executable, "main.swift"), $$"""
            // Ctrl+F5 runs; F5 debugs with LLDB. Answer in the Output panel.
            // Test Explorer runs the tests in Tests/{{library}}Tests with swift test.
            import Foundation
            import {{library}}

            print("What is your name? ", terminator: "")
            fflush(nil) // Every output stream, as Swift 6 forbids the shared stdout here.
            let name = readLine()?.trimmingCharacters(in: .whitespaces) ?? ""
            print(greet(name.isEmpty ? "world" : name))

            """);
        var core = Path.Combine(root, "Sources", library);
        Directory.CreateDirectory(core);
        File.WriteAllText(Path.Combine(core, "Greeting.swift"), """
            import Foundation

            /// A greeting for someone.
            public func greet(_ name: String) -> String {
                let answer = 42
                let message = "Hello, \(name)! This is Swift on TabStudio. The answer is \(answer)."
                return message
            }

            """);
        var tests = Path.Combine(root, "Tests", library + "Tests");
        Directory.CreateDirectory(tests);
        File.WriteAllText(Path.Combine(tests, "GreetingTests.swift"), $$"""
            import Testing
            @testable import {{library}}

            @Test func greetsByName() {
                #expect(greet("Ada").hasPrefix("Hello, Ada!"))
            }

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".build/\n.swiftpm/\n.tabstudio/\n");
        return root;
    }
}
