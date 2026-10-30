using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// What real repositories such as MediatR and ardalis/CleanArchitecture rely on the SDK for: transitive project
/// references, the assembly attributes it generates, strong-name signing and generators whose dependencies live in
/// another folder of their package.
/// </summary>
public sealed class RealRepositoryProjectTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    [Fact]
    public async Task A_project_compiles_against_the_projects_its_references_reference_as_the_SDK_passes_them()
    {
        // MediatR's samples: Stashbox -> Examples -> MediatR, and Stashbox uses IMediator from MediatR itself.
        Library("Core", "namespace Core; public interface IMediator { string Send(); }");
        _temp.Write("Examples/Examples.csproj", Project(references: ["..\\Core\\Core.csproj"]));
        _temp.Write("Examples/Handler.cs", "namespace Examples; public sealed class Handler : Core.IMediator { public string Send() => \"sent\"; }");
        _temp.Write("App/App.csproj", Project(exe: true, references: ["..\\Examples\\Examples.csproj"]));
        _temp.Write("App/Program.cs", "Core.IMediator mediator = new Examples.Handler(); System.Console.WriteLine(mediator.Send());");
        Load("App/App.csproj", "Examples/Examples.csproj", "Core/Core.csproj");

        var build = await BuildAsync("App/App.csproj");

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
    }

    [Fact]
    public async Task A_projects_DefineConstants_choose_its_conditional_code()
    {
        // Humanizer's tests define SYSTEM_COLLECTIONS_IMMUTABLE_MAJOR_VERSION_LE_7 and #error otherwise: C# projects
        // never got their DefineConstants, so the build stopped at the #error.
        // A symbol for another framework stays out, as Serilog's FEATURE_REMOTING for net48 does on .NET 10.
        _temp.Write("App/App.csproj", Project(exe: true, properties: "<DefineConstants>$(DefineConstants);FEATURE_X, LEGACY_API</DefineConstants></PropertyGroup><PropertyGroup Condition=\"'$(TargetFramework)' == 'net48'\"><DefineConstants>$(DefineConstants);FEATURE_REMOTING</DefineConstants>"));
        _temp.Write("App/Program.cs", """
            #if FEATURE_REMOTING
            #error A symbol of another framework was defined.
            #endif
            #if FEATURE_X && LEGACY_API && DEBUG
            System.Console.WriteLine("feature");
            #else
            #error The project's symbols are missing.
            #endif
            """);
        Load("App/App.csproj");

        var build = await BuildAsync("App/App.csproj");

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
    }

    [Fact]
    public async Task NoWarn_and_WarningsAsErrors_apply_as_csc_reads_them()
    {
        // AutoMapper's tests turn off 649 and 618; TabStudio showed their 48 CS0649 warnings all the same.
        _temp.Write("App/App.csproj", Project(properties: "<NoWarn>$(NoWarn);649;CS0618</NoWarn><WarningsAsErrors>CS0168</WarningsAsErrors>"));
        _temp.Write("App/Code.cs", """
            namespace App;
            public sealed class Holder { private int _never; public int Read() => _never; }
            public static class Old { [System.Obsolete("old")] public static void Gone() { } public static void Call() { Gone(); int unused; } }
            """);
        Load("App/App.csproj");

        var build = await BuildAsync("App/App.csproj");

        Assert.DoesNotContain(build.Diagnostics, d => d.Id is "CS0649" or "CS0618");
        Assert.Contains(build.Diagnostics, d => d.Id == "CS0168" && d.Severity == DiagnosticSeverityLevel.Error);
        Assert.False(build.Success);
    }

    [Fact]
    public async Task Build_only_references_private_references_and_DisableTransitiveProjectReferences_are_not_compiled_against()
    {
        Library("Core", "namespace Core; public static class Answer { public const int Value = 42; }");
        Library("Tool", "namespace Tool; public static class Version { public const int Value = 1; }");
        _temp.Write("Middle/Middle.csproj", Project(extraItems: """
            <ProjectReference Include="..\Core\Core.csproj" PrivateAssets="all" />
            <ProjectReference Include="..\Tool\Tool.csproj" ReferenceOutputAssembly="false" />
            """));
        _temp.Write("Middle/Middle.cs", "namespace Middle; public static class Uses { public const int Value = Core.Answer.Value; }");
        _temp.Write("App/App.csproj", Project(references: ["..\\Middle\\Middle.csproj"]));
        _temp.Write("App/App.cs", "namespace App; public static class Uses { public const int Value = Core.Answer.Value + Tool.Version.Value; }");
        Load("App/App.csproj", "Middle/Middle.csproj", "Core/Core.csproj", "Tool/Tool.csproj");

        var middle = await BuildAsync("Middle/Middle.csproj");
        var app = await BuildAsync("App/App.csproj");

        Assert.True(middle.Success, string.Join('\n', middle.Diagnostics));
        Assert.Contains(app.Diagnostics, d => d.Id == "CS0103" && d.Message.Contains("'Core'", StringComparison.Ordinal));
        Assert.Contains(app.Diagnostics, d => d.Id == "CS0103" && d.Message.Contains("'Tool'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InternalsVisibleTo_items_and_AssemblyAttribute_items_with_the_public_key_of_a_signed_friend_open_internals()
    {
        // MediatR: both assemblies are signed with MediatR.snk, and an AssemblyAttribute item names the friend's key.
        var key = _temp.Resolve("Repo.snk");
        File.WriteAllBytes(key, CreateStrongNameKey());
        var publicKey = PublicKeyOf(key);
        const string Signing = "<SignAssembly>true</SignAssembly><AssemblyOriginatorKeyFile>..\\Repo.snk</AssemblyOriginatorKeyFile>";
        _temp.Write("Lib/Lib.csproj", Project(properties: Signing, extraItems: $"""
            <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
              <_Parameter1>Lib.Tests, PublicKey={publicKey}</_Parameter1>
            </AssemblyAttribute>
            <InternalsVisibleTo Include="$(AssemblyName).Benchmarks" Key="{publicKey}" />
            """));
        _temp.Write("Lib/BuildInfo.cs", "namespace Lib; internal static class BuildInfo { internal const string Date = \"today\"; }");
        foreach (var friend in new[] { "Tests", "Benchmarks" })
        {
            _temp.Write($"Lib.{friend}/Lib.{friend}.csproj", Project(properties: Signing, references: ["..\\Lib\\Lib.csproj"]));
            _temp.Write($"Lib.{friend}/Uses.cs", $"namespace Lib.{friend}; public static class Uses {{ public const string Date = Lib.BuildInfo.Date; }}");
        }

        _temp.Write("Stranger/Stranger.csproj", Project(references: ["..\\Lib\\Lib.csproj"]));
        _temp.Write("Stranger/Uses.cs", "namespace Stranger; public static class Uses { public const string Date = Lib.BuildInfo.Date; }");
        Load("Lib/Lib.csproj", "Lib.Tests/Lib.Tests.csproj", "Lib.Benchmarks/Lib.Benchmarks.csproj", "Stranger/Stranger.csproj");

        var tests = await BuildAsync("Lib.Tests/Lib.Tests.csproj");
        var benchmarks = await BuildAsync("Lib.Benchmarks/Lib.Benchmarks.csproj");
        var stranger = await BuildAsync("Stranger/Stranger.csproj");

        Assert.True(tests.Success, string.Join('\n', tests.Diagnostics));
        Assert.True(benchmarks.Success, string.Join('\n', benchmarks.Diagnostics));
        Assert.Contains(stranger.Diagnostics, d => d.Id == "CS0122");
        Assert.NotEmpty(System.Reflection.AssemblyName.GetAssemblyName(Save(tests.Dependencies.Single(a => a.Name == "Lib").Image)).GetPublicKey()!);
    }

    [Fact]
    public async Task A_generator_finds_a_dependency_in_another_folder_of_its_package_and_a_load_failure_is_a_warning()
    {
        // Vogen: analyzers/dotnet/roslyn4.12/cs/Vogen.dll needs analyzers/dotnet/cs/Vogen.SharedTypes.dll.
        var package = _temp.Resolve("packages/shapes/1.0.0/analyzers/dotnet");
        var shared = Emit("Shapes.SharedTypes", "namespace Shapes; public static class Names { public static string Of(string type) => type + \"Id\"; }", Path.Combine(package, "cs"));
        var generator = Emit("Shapes.Generator", """
            using Microsoft.CodeAnalysis;
            [Generator]
            public sealed class ShapesGenerator : IIncrementalGenerator
            {
                public void Initialize(IncrementalGeneratorInitializationContext context) =>
                    context.RegisterPostInitializationOutput(output =>
                        output.AddSource("Shapes.g.cs", "public static class Generated { public const string Name = \"" + Shapes.Names.Of("Contributor") + "\"; }"));
            }
            """, Path.Combine(package, "roslyn4.12", "cs"), MetadataReference.CreateFromFile(shared), MetadataReference.CreateFromFile(typeof(IIncrementalGenerator).Assembly.Location));
        _temp.Write("App/App.csproj", Project());
        _temp.Write("App/Uses.cs", "public static class Uses { public const string Name = Generated.Name; }");
        Load("App/App.csproj");

        _host.SetAnalyzers(_temp.Resolve("App/App.csproj"), [shared, generator]);
        var build = await BuildAsync("App/App.csproj");
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));

        // Without its dependency the generator cannot run: the Error List says so instead of only showing missing code.
        var lonelyShared = Emit("Lonely.SharedTypes", "namespace Lonely; public static class Names { public static string Of(string type) => type; }", _temp.Resolve("build/lonely"));
        var lonely = Emit("Lonely.Generator", """
            using Microsoft.CodeAnalysis;
            [Generator]
            public sealed class LonelyGenerator : IIncrementalGenerator
            {
                private static readonly string Name = Lonely.Names.Of("Lonely");
                public void Initialize(IncrementalGeneratorInitializationContext context) =>
                    context.RegisterPostInitializationOutput(output => output.AddSource("Lonely.g.cs", "public static class Lonely" + Name + " { }"));
            }
            """, _temp.Resolve("packages/lonely/1.0.0/analyzers/dotnet/cs"), MetadataReference.CreateFromFile(lonelyShared), MetadataReference.CreateFromFile(typeof(IIncrementalGenerator).Assembly.Location));
        using var other = new RoslynWorkspaceHost();
        other.Load(WorkspaceLoader.Load(_temp.Resolve("App/App.csproj")), FileSystemTextProvider.Instance);
        other.SetAnalyzers(_temp.Resolve("App/App.csproj"), [lonely]);
        var failed = await new ProjectBuilder(other).BuildAsync(other.FindProject(_temp.Resolve("App/App.csproj"))!);
        Assert.Contains(failed.Diagnostics, d => d.Id is "CS8032" or "CS8034" or "CS8784" or "CS8785" && d.Severity == DiagnosticSeverityLevel.Warning);
    }

    [Fact]
    public async Task An_Aspire_AppHost_is_not_compiled_and_says_why()
    {
        Library("Web", "namespace Web; public static class Site { }");
        _temp.Write("AppHost/AppHost.csproj", """
            <Project Sdk="Aspire.AppHost.Sdk/13.3.5">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Web\Web.csproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("AppHost/AppHost.cs", "var builder = DistributedApplication.CreateBuilder(args); builder.AddProject<Projects.Web>(\"web\");");
        _temp.Write("Repo.slnx", "<Solution><Project Path=\"AppHost/AppHost.csproj\" /><Project Path=\"Web/Web.csproj\" /></Solution>");
        var workspace = WorkspaceLoader.Load(_temp.Resolve("Repo.slnx"));
        _host.Load(workspace, FileSystemTextProvider.Instance);
        var appHost = workspace.Projects.Single(p => p.Name == "AppHost");

        Assert.Equal(ProjectKind.AspireAppHost, appHost.Kind);
        Assert.Null(_host.FindProject(appHost.FilePath));
        Assert.Empty(await _host.GetProjectDiagnosticsAsync(appHost.FilePath));
        var build = await new ProjectBuilder(_host).BuildAsync(appHost);
        Assert.Contains("Aspire AppHost", Assert.Single(build.Diagnostics).Message, StringComparison.Ordinal);
        Assert.NotEqual(appHost, workspace.DefaultStartupProject);
    }

    /// <summary>
    /// maui-samples' TodoSQLite: a .NET MAUI app is not compiled, which gave 48 errors from its Platforms folders, and a
    /// build of it is one diagnostic that says why, not "only C# projects are compiled".
    /// </summary>
    [Fact]
    public async Task A_MAUI_app_is_not_compiled_and_its_build_says_it_needs_the_MAUI_workload()
    {
        _temp.Write("TodoSQLite/TodoSQLite.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
                <OutputType>Exe</OutputType>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("TodoSQLite/Platforms/Android/MainActivity.cs", "using Android.App; public class MainActivity : MauiAppCompatActivity { }");
        Library("Core", "namespace Core; public static class Todo { }");
        Load("TodoSQLite/TodoSQLite.csproj", "Core/Core.csproj");
        var workspace = WorkspaceLoader.Load(_temp.Resolve("All.slnx"));
        var app = workspace.Projects.Single(p => p.Name == "TodoSQLite");

        Assert.Null(_host.FindProject(app.FilePath));
        Assert.Empty(await _host.GetProjectDiagnosticsAsync(app.FilePath));
        var build = await new ProjectBuilder(_host).BuildAsync(app);
        var diagnostic = Assert.Single(build.Diagnostics);
        Assert.Equal(ProjectBuilder.NeedsPlatformWorkloadId, diagnostic.Id);
        Assert.Contains("MAUI workload", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("only C# projects", diagnostic.Message, StringComparison.Ordinal);
        Assert.False(build.Success);
    }

    [Fact]
    public async Task An_executable_test_project_builds_without_a_Main_and_the_assembly_version_follows_Version()
    {
        // Polly's xUnit v3 test projects are executables whose Main the package's targets generate.
        _temp.Write("Core/Core.csproj", Project(properties: "<Version>8.7.1-beta.2</Version>"));
        _temp.Write("Core/Core.cs", "namespace Core; public static class Answer { public const int Value = 42; }");
        _temp.Write("Tests/Tests.csproj", Project(exe: true, references: ["..\\Core\\Core.csproj"], properties: "<IsTestProject>true</IsTestProject>"));
        _temp.Write("Tests/AnswerTests.cs", "namespace Tests; public sealed class AnswerTests { public int Value() => Core.Answer.Value; }");
        Load("Tests/Tests.csproj", "Core/Core.csproj");

        var build = await BuildAsync("Tests/Tests.csproj");

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var core = _host.FindProject(_temp.Resolve("Core/Core.csproj"))!;
        Assert.Contains("AssemblyVersionAttribute(\"8.7.1\")", RoslynWorkspaceHost.CreateAssemblyAttributesSource(core)!, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }

    private void Library(string name, string source)
    {
        _temp.Write($"{name}/{name}.csproj", Project());
        _temp.Write($"{name}/{name}.cs", source);
    }

    private static string Project(bool exe = false, string[]? references = null, string properties = "", string extraItems = "")
    {
        var items = string.Concat((references ?? []).Select(r => $"<ProjectReference Include=\"{r}\" />"));
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                {(exe ? "<OutputType>Exe</OutputType>" : string.Empty)}
                {properties}
              </PropertyGroup>
              <ItemGroup>{items}{extraItems}</ItemGroup>
            </Project>
            """;
    }

    /// <summary>Loads the projects as one solution, the first being the one that is opened.</summary>
    private void Load(params string[] projects)
    {
        var solution = _temp.Write("All.slnx", "<Solution>" + string.Concat(projects.Select(p => $"<Project Path=\"{p}\" />")) + "</Solution>");
        _host.Load(WorkspaceLoader.Load(solution), FileSystemTextProvider.Instance);
    }

    private Task<BuildResult> BuildAsync(string project) =>
        new ProjectBuilder(_host).BuildAsync(_host.FindProject(_temp.Resolve(project))!);

    private string Save(byte[] image)
    {
        var path = _temp.Resolve($"images/{Guid.NewGuid():N}.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, image);
        return path;
    }

    private static string Emit(string name, string source, string folder, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            [.. Basic.Reference.Assemblies.Net100.References.All, .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".dll");
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return path;
    }

    /// <summary>The public key a key file signs with, in hexadecimal, as <c>InternalsVisibleTo</c> names it.</summary>
    private static string PublicKeyOf(string keyFile)
    {
        var compilation = CSharpCompilation.Create("Key", options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithCryptoKeyFile(keyFile).WithStrongNameProvider(new DesktopStrongNameProvider()));
        return Convert.ToHexStringLower([.. compilation.Assembly.Identity.PublicKey]);
    }

    /// <summary>A new strong-name key file, as <c>sn -k</c> writes it: a CryptoAPI <c>PRIVATEKEYBLOB</c>.</summary>
    private static byte[] CreateStrongNameKey()
    {
        using var rsa = RSA.Create(1024);
        var p = rsa.ExportParameters(includePrivateParameters: true);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)0x07); // PRIVATEKEYBLOB
        writer.Write((byte)0x02); // version
        writer.Write((ushort)0);
        writer.Write(0x00002400); // CALG_RSA_SIGN
        writer.Write(0x32415352); // "RSA2"
        writer.Write(1024);
        var exponent = new byte[4];
        p.Exponent!.CopyTo(exponent, 4 - p.Exponent.Length);
        writer.Write(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(exponent));
        void Write(byte[] bigEndian, int length)
        {
            var little = new byte[length];
            for (var i = 0; i < bigEndian.Length && i < length; i++)
            {
                little[i] = bigEndian[bigEndian.Length - 1 - i];
            }

            writer.Write(little);
        }

        Write(p.Modulus!, 128);
        Write(p.P!, 64);
        Write(p.Q!, 64);
        Write(p.DP!, 64);
        Write(p.DQ!, 64);
        Write(p.InverseQ!, 64);
        Write(p.D!, 128);
        writer.Flush();
        return stream.ToArray();
    }
}
