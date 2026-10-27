using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Packages.Tests;

/// <summary>
/// End-to-end tests against the real nuget.org: restore, build with the workspace host and run in-process.
/// They need the internet, which CI has; without it they fail and say so.
/// </summary>
public sealed class NuGetOrgTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };

    [Fact]
    public async Task Restores_builds_and_runs_a_project_that_uses_Humanizer_and_Newtonsoft_Json()
    {
        var projectPath = _temp.Write("Hello/Hello.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Humanizer.Core" Version="2.14.1" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Hello/Program.cs", """
            using Humanizer;
            using Newtonsoft.Json;

            Console.WriteLine("hello world".Humanize(LetterCasing.Title));
            Console.WriteLine(JsonConvert.SerializeObject(new { Name = "tablet", Cores = 8 }));
            """);

        var workspace = WorkspaceLoader.Load(projectPath);
        var project = Assert.Single(workspace.Projects);
        var restorer = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = _temp.Resolve("packages"),
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
            },
            _http);

        var restore = await restorer.RestoreAsync(project);

        AssertNuGetOrgReachable(restore);
        Assert.True(restore.Success, string.Join('\n', restore.Diagnostics));
        Assert.Contains(restore.Packages, p => p.Id == "Humanizer.Core" && p.Version == "2.14.1" && p.IsDirect);
        Assert.Contains(restore.Packages, p => p.Id == "Newtonsoft.Json" && p.Version == "13.0.3" && p.IsDirect);
        Assert.Contains(restore.CompileAssemblies, path => Path.GetFileName(path) == "Humanizer.dll");
        Assert.Contains(restore.RuntimeAssemblies, path => Path.GetFileName(path) == "Newtonsoft.Json.dll");

        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
        var build = await new ProjectBuilder(host).BuildAsync(project);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.DoesNotContain(build.Diagnostics, d => d.Id == ProjectBuilder.UnrestoredPackageId);

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, restore.RuntimeAssemblies, [], console);

        Assert.Empty(console.Error);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("Hello World\n{\"Name\":\"tablet\",\"Cores\":8}\n", console.Output);

        var again = await restorer.RestoreAsync(project);
        Assert.True(again.FromCache);
        Assert.Equal(restore.CompileAssemblies, again.CompileAssemblies);
    }

    [Fact]
    public async Task Restores_a_package_with_transitive_dependencies_and_prunes_the_framework_ones()
    {
        // Microsoft.Extensions.Logging.Abstractions 8.0.0 depends on Microsoft.Extensions.DependencyInjection.Abstractions,
        // and, for older frameworks, on System.* packages that .NET 10 provides itself.
        var projectPath = _temp.Write("Logging/Logging.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);
        var restorer = new PackageRestorer(
            new PackageRestoreOptions
            {
                GlobalPackagesFolder = _temp.Resolve("packages"),
                FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
            },
            _http);

        var restore = await restorer.RestoreAsync(ProjectParser.Load(projectPath));

        AssertNuGetOrgReachable(restore);
        Assert.True(restore.Success, string.Join('\n', restore.Diagnostics));
        Assert.Contains(restore.Packages, p => p.Id == "Microsoft.Extensions.DependencyInjection.Abstractions" && !p.IsDirect);
        Assert.DoesNotContain(restore.Packages, p => p.Id.StartsWith("System.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Runs_the_source_generators_of_CommunityToolkit_Mvvm()
    {
        var projectPath = _temp.Write("Mvvm/Mvvm.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Mvvm/Program.cs", """
            using CommunityToolkit.Mvvm.ComponentModel;

            var counter = new Counter();
            counter.PropertyChanged += (_, e) => Console.WriteLine($"{e.PropertyName} changed");
            counter.Count = 3;
            Console.WriteLine(counter.Count);

            public partial class Counter : ObservableObject
            {
                [ObservableProperty]
                private int _count;
            }
            """);

        var workspace = WorkspaceLoader.Load(projectPath);
        var project = Assert.Single(workspace.Projects);
        var restore = await CreateRestorer().RestoreAsync(project);
        AssertNuGetOrgReachable(restore);
        Assert.True(restore.Success, string.Join('\n', restore.Diagnostics));
        Assert.Contains(restore.Analyzers, path => Path.GetFileName(path) == "CommunityToolkit.Mvvm.SourceGenerators.dll");

        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
        host.SetAnalyzers(project.FilePath, restore.Analyzers);
        var build = await new ProjectBuilder(host).BuildAsync(project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, restore.RuntimeAssemblies, [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("Count changed\n3\n", console.Output);
    }

    /// <summary>
    /// Riok.Mapperly packs its generator in analyzers/roslyn5.0/dotnet/cs/, not analyzers/dotnet/roslyn5.0/cs/; in
    /// 0.19 it never ran, so gothinkster/aspnetcore-realworld-example-app's mapper had no implementation (CS8795).
    /// </summary>
    [Fact]
    public async Task Riok_Mapperly_generates_the_mapper_as_in_the_Conduit_repository()
    {
        var projectPath = _temp.Write("Mapping/Mapping.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Riok.Mapperly" Version="4.3.1" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Mapping/Program.cs", """
            using Riok.Mapperly.Abstractions;

            var user = new ConduitMapper().PersonToUser(new Person { Username = "tablet", Bio = "runs here" });
            Console.WriteLine($"{user.Username}: {user.Bio}");

            public sealed class Person { public string Username { get; set; } = ""; public string? Bio { get; set; } }
            public sealed class User { public string Username { get; set; } = ""; public string? Bio { get; set; } }

            [Mapper]
            public partial class ConduitMapper
            {
                public partial User PersonToUser(Person person);
            }
            """);

        var workspace = WorkspaceLoader.Load(projectPath);
        var project = Assert.Single(workspace.Projects);
        var restore = await CreateRestorer().RestoreAsync(project);
        AssertNuGetOrgReachable(restore);
        Assert.True(restore.Success, string.Join('\n', restore.Diagnostics));
        Assert.Contains(restore.Analyzers, path => path.Replace('\\', '/').EndsWith("analyzers/roslyn5.0/dotnet/cs/Riok.Mapperly.dll", StringComparison.Ordinal));
        Assert.Contains(restore.BuildFiles, path => Path.GetFileName(path) == "Riok.Mapperly.targets");

        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
        host.SetAnalyzers(project.FilePath, restore.Analyzers);
        host.SetPackageBuildFiles(project.FilePath, restore.BuildFiles);
        var build = await new ProjectBuilder(host).BuildAsync(project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.DoesNotContain(build.Diagnostics, d => d.Id == "CS8795");

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, restore.RuntimeAssemblies, [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("tablet: runs here\n", console.Output);
    }

    /// <summary>
    /// CommunityToolkit.Mvvm 8.4.2's generators on partial properties, as Avalonia.Samples' TestableApp uses them, with
    /// the properties its build files make visible (MvvmToolkitEnableINotifyPropertyChangingSupport) read from them.
    /// </summary>
    [Fact]
    public async Task CommunityToolkit_Mvvm_generates_partial_properties_and_commands_with_its_build_properties()
    {
        var projectPath = _temp.Write("Calculator/Calculator.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Calculator/Program.cs", """
            using System;
            using CommunityToolkit.Mvvm.ComponentModel;
            using CommunityToolkit.Mvvm.Input;

            var model = new MainWindowViewModel { FirstOperand = 2, SecondOperand = 3 };
            model.PropertyChanging += (_, e) => Console.Write($"<{e.PropertyName}>");
            model.AddCommand.Execute(null);
            Console.WriteLine(model.Result);

            public partial class MainWindowViewModel : ObservableObject
            {
                [ObservableProperty]
                public partial double? FirstOperand { get; set; }

                [ObservableProperty]
                public partial double? SecondOperand { get; set; }

                [ObservableProperty]
                public partial string? Result { get; set; }

                [RelayCommand]
                private void Add() => Result = (FirstOperand + SecondOperand)?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            """);

        var workspace = WorkspaceLoader.Load(projectPath);
        var project = Assert.Single(workspace.Projects);
        var restore = await CreateRestorer().RestoreAsync(project);
        AssertNuGetOrgReachable(restore);
        Assert.True(restore.Success, string.Join('\n', restore.Diagnostics));

        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        host.SetPackageReferences(project.FilePath, restore.CompileAssemblies);
        host.SetAnalyzers(project.FilePath, restore.Analyzers);
        host.SetPackageBuildFiles(project.FilePath, restore.BuildFiles);
        var visible = host.FindProject(project.FilePath)!;
        Assert.Contains("MvvmToolkitEnableINotifyPropertyChangingSupport", visible.CompilerVisibleProperties);
        Assert.Equal("true", visible.GetProperty("MvvmToolkitEnableINotifyPropertyChangingSupport"));
        Assert.Equal("false", visible.GetProperty("_MvvmToolkitIsUsingWindowsRuntimePack"));

        var build = await new ProjectBuilder(host).BuildAsync(project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, restore.RuntimeAssemblies, [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("<Result>5\n", console.Output);
    }

    [Fact]
    public async Task Runs_the_regular_expression_generator_of_dotnet_itself()
    {
        var projectPath = _temp.Write("Regex/Regex.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("Regex/Program.cs", """
            using System.Text.RegularExpressions;

            Console.WriteLine(Patterns.Digits().Match("tablet 2026").Value);

            static partial class Patterns
            {
                [GeneratedRegex(@"\d+")]
                public static partial Regex Digits();
            }
            """);

        var restorer = CreateRestorer();
        var (analyzers, problems) = await restorer.RestoreFrameworkAnalyzersAsync();
        Assert.DoesNotContain(problems, d => d.Severity == Core.Diagnostics.DiagnosticSeverityLevel.Error);
        Assert.Contains(analyzers, path => Path.GetFileName(path) == "System.Text.RegularExpressions.Generator.dll");

        var workspace = WorkspaceLoader.Load(projectPath);
        var project = Assert.Single(workspace.Projects);
        using var host = new RoslynWorkspaceHost();
        host.SetFrameworkAnalyzers(analyzers);
        host.Load(workspace, FileSystemTextProvider.Instance);
        var build = await new ProjectBuilder(host).BuildAsync(project);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("2026\n", console.Output);
    }

    public void Dispose()
    {
        _http.Dispose();
        _temp.Dispose();
    }

    private PackageRestorer CreateRestorer() => new(
        new PackageRestoreOptions
        {
            GlobalPackagesFolder = _temp.Resolve("packages"),
            FrameworkAssemblyNames = ReferenceAssemblyNames.Net100,
        },
        _http);

    internal static void AssertNuGetOrgReachable(RestoreResult restore)
    {
        var unreachable = restore.Diagnostics.FirstOrDefault(d => d.Id == PackageDiagnosticIds.NetworkUnreachable);
        if (unreachable is not null)
        {
            Assert.Fail($"These tests restore from nuget.org, which this machine cannot reach. Run them with internet access (CI has it). {unreachable.Message}");
        }
    }
}
