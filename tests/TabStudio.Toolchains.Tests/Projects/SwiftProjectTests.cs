using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>Swift packages: their executables, Vapor apps, and how F5 builds and runs them with SwiftPM.</summary>
public sealed class SwiftProjectTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    private static ToolchainDefinition Swift => ToolchainCatalog.Default.Find("swift")!;

    private const string Manifest = """
        // swift-tools-version:6.0
        import PackageDescription

        let package = Package(
            name: "Tool",
            products: [.executable(name: "tool", targets: ["Tool"])],
            targets: [
                .executableTarget(name: "Tool"),
                .testTarget(name: "ToolTests", dependencies: ["Tool"]),
            ]
        )
        """;

    [Fact]
    public void A_package_builds_its_product_and_runs_it_from_the_debug_build()
    {
        _folder.Write("Package.swift", Manifest);
        _folder.Write("Sources/Tool/main.swift", "print(1)\n");

        Assert.Equal(["tool"], SwiftProject.Executables(_folder.Path));
        Assert.True(SwiftProject.HasTests(_folder.Path));
        var plan = RunPlanner.PlanProject(new(Swift, _folder.Path, "Package.swift"))!;

        Assert.Equal(["swift", "build", "--product", "tool"], [plan.Build!.Program, .. plan.Build.Arguments]);
        Assert.Equal(Path.Combine(_folder.Path, ".build", "debug", "tool"), plan.Run.Program);
        Assert.Empty(plan.Run.Arguments);
        Assert.Equal("dumb", plan.Run.Environment["TERM"]);
    }

    [Fact]
    public void A_package_with_a_CMake_build_beside_it_is_a_Swift_project_first()
    {
        _folder.Write("Package.swift", Manifest);
        _folder.Write("CMakeLists.txt", "project(Tool LANGUAGES Swift)\n");
        _folder.Write("Sources/Tool/main.swift", "print(1)\n");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        Assert.Equal("swift", projects[0].Toolchain.Id);
    }

    [Fact]
    public void A_Vapor_app_serves_on_a_free_local_port()
    {
        _folder.Write("Package.swift", """
            // swift-tools-version:6.0
            import PackageDescription
            let package = Package(
                name: "hello",
                dependencies: [.package(url: "https://github.com/vapor/vapor.git", from: "4.115.0")],
                targets: [.executableTarget(name: "App", dependencies: [.product(name: "Vapor", package: "vapor")])]
            )
            """);

        Assert.True(SwiftProject.IsVapor(_folder.Path));
        var plan = RunPlanner.PlanProject(new(Swift, _folder.Path, "Package.swift"))!;

        Assert.Equal(["serve", "--hostname", "127.0.0.1", "--port"], plan.Run.Arguments.Take(4));
        Assert.True(int.Parse(plan.Run.Arguments[4], System.Globalization.CultureInfo.InvariantCulture) > 0);
    }

    [Fact]
    public void A_loose_file_compiles_with_debug_information()
    {
        var file = _folder.Write("hello.swift", "print(\"hi\")\n");

        var plan = RunPlanner.PlanFile(new(Swift, _folder.Path, null), file);

        Assert.Equal(["swiftc", "-g", "-Onone", "hello.swift", "-o"], [plan.Build!.Program, .. plan.Build.Arguments.Take(4)]);
        Assert.Equal(Path.Combine(_folder.Path, ".build", "tabstudio", "hello"), plan.Run.Program);
    }

    [Fact]
    public void The_toolchain_runs_in_the_glibc_environment_with_its_own_programs()
    {
        Assert.NotNull(Swift.Glibc);
        Assert.Empty(Swift.Packages);
        Assert.True(Swift.Glibc!.Runs("swift"));
        Assert.True(Swift.Glibc.Runs("/projects/app/.build/debug/tool"));
        Assert.False(Swift.Glibc.Runs("php"));
        Assert.Contains("lldb-dap", Swift.Glibc.Commands);
    }

    [Fact]
    public void Swift_compiler_errors_reach_the_error_list()
    {
        var errors = new ProblemMatcher(Swift).FindErrors(
            ["/work/tool/Sources/Tool/main.swift:2:7: error: cannot find 'prin' in scope", "/work/tool/Sources/Tool/main.swift:1:1: warning: unused"], _ => true);

        var error = Assert.Single(errors);
        Assert.Equal(2, error.Location!.Line);
        Assert.Equal("cannot find 'prin' in scope", error.Message);
    }

    /// <summary>
    /// IceCubesApp's packages import SwiftUI, which only Apple's SDKs have: the Doctor said they need the Swift
    /// toolchain, and the build listed "no such module 'SwiftUI'". Alamofire imports UIKit only inside #if canImport,
    /// and builds on Linux.
    /// </summary>
    [Fact]
    public void An_app_that_imports_SwiftUI_is_for_Xcode_but_a_conditional_import_is_not()
    {
        _folder.Write("app/Package.swift", Manifest);
        _folder.Write("app/Sources/Account/AccountView.swift", "import Foundation\nimport SwiftUI\n\nstruct AccountView: View {}\n");
        _folder.Write("lib/Package.swift", Manifest);
        _folder.Write("lib/Sources/Alamofire/Session.swift", "import Foundation\n#if canImport(UIKit)\nimport UIKit\n#endif\n");

        Assert.Equal("SwiftUI", SwiftProject.AppleFramework(_folder.Resolve("app")));
        Assert.Null(SwiftProject.AppleFramework(_folder.Resolve("lib")));
        Assert.StartsWith("Account is an Apple-platform app (SwiftUI), which builds only with Xcode", SwiftProject.AppleExplanation("Account", "SwiftUI"), StringComparison.Ordinal);
    }

    public void Dispose() => _folder.Dispose();
}
