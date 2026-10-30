using System.Xml.Linq;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

/// <summary>
/// Legacy .NET Framework projects, read from their explicit items as MSBuild reads them: a WPF or Windows Forms
/// tutorial that uses only the framework runs like the SDK-style project it would be upgraded to, one that needs
/// Windows says why, and Upgrade project writes the SDK-style project .NET Upgrade Assistant would.
/// </summary>
public sealed class LegacyFrameworkProjectTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void A_legacy_wpf_app_is_read_from_its_items_as_a_wpf_project()
    {
        LegacyFrameworkProjects.WriteWpfCalculator(_temp.Path, "Calc");
        _temp.Write("Calc/Stray.cs", "class Stray { }");

        var project = ProjectParser.Load(_temp.Resolve("Calc/Calc.csproj"));

        Assert.True(ProjectParser.IsLegacyFrameworkProject(project));
        Assert.Equal(ProjectKind.Wpf, project.Kind);
        Assert.Equal("true", project.GetProperty("UseWPF"));
        Assert.Null(project.GetProperty("UseWindowsForms"));
        Assert.Equal("false", project.GetProperty("EnableDefaultCompileItems"));
        Assert.Contains(project.AssemblyReferences, r => r.Name == "PresentationFramework");

        // Only the files the project lists, as MSBuild builds the legacy format: not Stray.cs.
        var sources = ProjectSourceFiles.Enumerate(project).Select(f => Path.GetRelativePath(project.DirectoryPath, f).Replace('\\', '/')).ToList();
        Assert.Equal(
            ["App.xaml.cs", "MainWindow.xaml.cs", "Properties/AssemblyInfo.cs", "Properties/Resources.Designer.cs", "Properties/Settings.Designer.cs"],
            sources);
        Assert.Equal(["Properties\\Resources.resx"], project.EmbeddedResourceIncludes);
    }

    [Fact]
    public void A_legacy_windows_forms_app_is_a_windows_forms_project()
    {
        LegacyFrameworkProjects.WriteWindowsFormsApp(_temp.Path, "Hello");

        var project = ProjectParser.Load(_temp.Resolve("Hello/Hello.csproj"));

        Assert.Equal(ProjectKind.WinForms, project.Kind);
        Assert.Equal("true", project.GetProperty("UseWindowsForms"));
        Assert.Null(project.GetProperty("UseWPF"));
        Assert.Contains("Form1.resx", project.EmbeddedResourceIncludes);
    }

    [Fact]
    public void The_doctor_runs_a_framework_only_tutorial_on_the_tablet()
    {
        LegacyFrameworkProjects.WriteWpfCalculator(_temp.Path, "Calc");
        LegacyFrameworkProjects.WriteWindowsFormsApp(_temp.Path, "Hello");

        var wpf = CompatibilityDoctor.Diagnose(ProjectParser.Load(_temp.Resolve("Calc/Calc.csproj")));
        var forms = CompatibilityDoctor.Diagnose(ProjectParser.Load(_temp.Resolve("Hello/Hello.csproj")));

        Assert.Equal(CompatibilityStatus.Ready, wpf.Status);
        Assert.Equal(RunHost.WebView, wpf.Host);
        Assert.StartsWith("A .NET Framework project, run with .NET on this tablet.", wpf.Explanation, StringComparison.Ordinal);
        Assert.Contains("OpenSilver", wpf.Explanation, StringComparison.Ordinal);
        Assert.True(wpf.CanUpgradeProject);
        Assert.Equal(CompatibilityStatus.Ready, forms.Status);
        Assert.Equal(RunHost.AndroidView, forms.Host);
        Assert.StartsWith(NetFrameworkCompatibility.RunsHereSummary, forms.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void A_legacy_console_app_runs_in_process()
    {
        _temp.Write("Tool/Program.cs", "class Program { static void Main() { System.Console.WriteLine(1); } }");
        // A package install adds System.Web to console apps that never use it, as in example-console-visualbasic.
        var path = _temp.Write("Tool/Tool.csproj", Legacy("Exe", """<Reference Include="System" /><Reference Include="System.Core" /><Reference Include="System.Web" />""", """<Compile Include="Program.cs" />"""));

        var report = CompatibilityDoctor.Diagnose(ProjectParser.Load(path));

        Assert.Equal(CompatibilityStatus.Ready, report.Status);
        Assert.Equal(RunHost.InProcess, report.Host);
    }

    [Theory]
    [InlineData("""<Reference Include="System.Speech" />""", "", "references System.Speech, which is part of the .NET Framework on Windows only")]
    [InlineData("""<Reference Include="Vendor.Charts"><HintPath>..\lib\Vendor.Charts.dll</HintPath></Reference>""", "", "references Vendor.Charts.dll, built for the .NET Framework")]
    [InlineData("""<COMReference Include="WMPLib"><Guid>{6BF52A50-394A-11D3-B153-00C04F79FAA6}</Guid></COMReference>""", "", "uses the COM component WMPLib")]
    [InlineData("", "[System.Runtime.InteropServices.DllImport(\"user32.dll\")] static extern int MessageBeep(uint type);", "calls Windows through P/Invoke in Form1.cs")]
    public void A_program_that_needs_windows_says_why(string reference, string member, string reason)
    {
        _temp.Write("App/Form1.cs", $"class Form1 : System.Windows.Forms.Form {{ {member} }}");
        var path = _temp.Write("App/App.csproj", Legacy(
            "WinExe",
            $"""<Reference Include="System" /><Reference Include="System.Windows.Forms" /><Reference Include="System.Drawing" />{reference}""",
            """<Compile Include="Form1.cs" />"""));

        var project = ProjectParser.Load(path);
        var report = CompatibilityDoctor.Diagnose(project);

        Assert.True(NetFrameworkCompatibility.NeedsWindows(project));
        Assert.Equal(CompatibilityStatus.NeedsWindows, report.Status);
        Assert.Equal(RunHost.WindowsBridge, report.Host);
        Assert.Contains(reason, report.Explanation, StringComparison.Ordinal);
        Assert.Contains("Upgrade project", report.Explanation, StringComparison.Ordinal);
        Assert.Equal($"App is a .NET Framework project that needs Windows: it {reason}.", NetFrameworkCompatibility.NeedsWindowsMessage(project));
    }

    [Fact]
    public void Windows_forms_inside_wpf_needs_windows()
    {
        LegacyFrameworkProjects.WriteWpfCalculator(_temp.Path, "Calc");
        var path = _temp.Resolve("Calc/Calc.csproj");
        File.WriteAllText(path, File.ReadAllText(path).Replace("""<Reference Include="WindowsBase" />""", """<Reference Include="WindowsBase" /><Reference Include="System.Windows.Forms" />""", StringComparison.Ordinal));

        var project = ProjectParser.Load(path);

        Assert.Equal(ProjectKind.Wpf, project.Kind);
        Assert.Equal(["uses Windows Forms inside WPF"], NetFrameworkCompatibility.WindowsOnlyReasons(project));
    }

    [Fact]
    public void Packages_config_packages_are_package_references_and_their_dlls_are_not_windows_only()
    {
        _temp.Write("App/Program.cs", "class Program { static void Main() { } }");
        _temp.Write("App/packages.config", """
            <?xml version="1.0" encoding="utf-8"?>
            <packages>
              <package id="Newtonsoft.Json" version="13.0.3" targetFramework="net472" />
              <package id="StyleCop.Analyzers" version="1.1.118" targetFramework="net472" developmentDependency="true" />
            </packages>
            """);
        var path = _temp.Write("App/App.csproj", Legacy(
            "Exe",
            """
            <Reference Include="System" />
            <Reference Include="Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed, processorArchitecture=MSIL">
              <HintPath>..\packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll</HintPath>
            </Reference>
            """,
            """<Compile Include="Program.cs" /><None Include="packages.config" />"""));

        var project = ProjectParser.Load(path);

        Assert.Contains(new PackageReferenceInfo("Newtonsoft.Json", "13.0.3"), project.PackageReferences);
        Assert.Contains(project.PackageReferences, p => p.Name == "StyleCop.Analyzers" && p.IsPrivate);
        Assert.Empty(NetFrameworkCompatibility.WindowsOnlyReasons(project));
        Assert.True(NetFrameworkCompatibility.RunsHere(project));
    }

    [Fact]
    public void A_file_listed_in_another_letter_case_is_found()
    {
        _temp.Write("App/MainForm.cs", "class MainForm { }");
        var path = _temp.Write("App/App.csproj", Legacy("WinExe", """<Reference Include="System" />""", """<Compile Include="mainform.cs" />"""));

        var project = ProjectParser.Load(path);

        Assert.Equal([_temp.Resolve("App/MainForm.cs")], ProjectSourceFiles.Enumerate(project));
    }

    [Fact]
    public void An_sdk_style_net48_project_still_needs_windows()
    {
        var project = ProjectParser.Parse("/src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net48</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        Assert.False(ProjectParser.IsLegacyFrameworkProject(project));
        Assert.Equal(CompatibilityStatus.NeedsWindows, CompatibilityDoctor.Diagnose(project).Status);
        Assert.False(CompatibilityDoctor.Diagnose(project).CanUpgradeProject);
    }

    [Fact]
    public void Upgrade_writes_an_sdk_style_visual_basic_windows_forms_project()
    {
        _temp.Write("Pong/Pong.vbproj", """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <StartupObject>Pong.My.MyApplication</StartupObject>
                <RootNamespace>Pong</RootNamespace>
                <AssemblyName>Pong</AssemblyName>
                <MyType>WindowsForms</MyType>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <OptionStrict>On</OptionStrict>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Reference Include="System.Drawing" />
                <Reference Include="System.Windows.Forms" />
              </ItemGroup>
              <ItemGroup>
                <Import Include="Microsoft.VisualBasic" />
                <Import Include="System.Windows.Forms" />
              </ItemGroup>
              <ItemGroup>
                <Compile Include="Form1.vb"><SubType>Form</SubType></Compile>
                <Compile Include="Form1.Designer.vb"><DependentUpon>Form1.vb</DependentUpon></Compile>
                <Compile Include="My Project\AssemblyInfo.vb" />
                <EmbeddedResource Include="Form1.resx"><DependentUpon>Form1.vb</DependentUpon></EmbeddedResource>
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.VisualBasic.targets" />
            </Project>
            """);
        _temp.Write("Pong/Form1.vb", "Public Class Form1\nEnd Class\n");
        _temp.Write("Pong/Form1.Designer.vb", "Partial Class Form1\nEnd Class\n");
        _temp.Write("Pong/My Project/AssemblyInfo.vb", "");
        _temp.Write("Pong/Form1.resx", "<root />");

        var upgrade = LegacyProjectUpgrader.Plan(_temp.Resolve("Pong/Pong.vbproj"));
        var project = XElement.Parse(upgrade.UpgradedXml);

        Assert.Equal("Microsoft.NET.Sdk", project.Attribute("Sdk")?.Value);
        Assert.Equal("WindowsForms", Property(project, "MyType"));
        Assert.Equal("On", Property(project, "OptionStrict"));
        Assert.Equal("false", Property(project, "GenerateAssemblyInfo"));
        Assert.Contains(upgrade.Notes, n => n.Contains("My Project/AssemblyInfo.vb", StringComparison.Ordinal));
        // The .vb files and the form's resources are the SDK's default items, nested by name.
        Assert.DoesNotContain(project.Descendants(), e => e.Name.LocalName == "Compile" && e.Attribute("Include")?.Value.EndsWith(".vb", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(project.Descendants(), e => e.Name.LocalName == "DependentUpon");
    }

    [Fact]
    public void Upgrade_writes_an_sdk_style_wpf_project_without_default_items()
    {
        LegacyFrameworkProjects.WriteWpfCalculator(_temp.Path, "Calc");
        _temp.Write("Calc/Unused.cs", "class Unused { }");
        var path = _temp.Resolve("Calc/Calc.csproj");

        var upgrade = LegacyProjectUpgrader.Plan(path);
        var project = XElement.Parse(upgrade.UpgradedXml);

        Assert.Equal("Microsoft.NET.Sdk", project.Attribute("Sdk")?.Value);
        Assert.Equal("net10.0-windows", upgrade.TargetFramework);
        Assert.Equal("WinExe", Property(project, "OutputType"));
        Assert.Equal("net10.0-windows", Property(project, "TargetFramework"));
        Assert.Equal("true", Property(project, "UseWPF"));
        Assert.Equal("false", Property(project, "GenerateAssemblyInfo"));
        foreach (var gone in new[] { "ProjectGuid", "TargetFrameworkVersion", "ProjectTypeGuids", "OutputPath", "DebugType", "WarningLevel" })
        {
            Assert.Null(Property(project, gone));
        }

        Assert.DoesNotContain(project.Descendants(), e => e.Name.LocalName is "Reference" or "Page" or "ApplicationDefinition" or "Import");
        Assert.DoesNotContain(project.Descendants(), e => e.Attribute("Include") is not null && e.Name.LocalName == "Compile");
        Assert.Contains(project.Descendants("Compile"), e => e.Attribute("Remove")?.Value == "Unused.cs");
        var resx = Assert.Single(project.Descendants("EmbeddedResource"));
        Assert.Equal("Properties\\Resources.resx", resx.Attribute("Update")?.Value);
        Assert.Equal("ResXFileCodeGenerator", resx.Element("Generator")?.Value);
        Assert.Contains(project.Descendants("None"), e => e.Attribute("Update")?.Value == "Properties\\Settings.settings");
        Assert.DoesNotContain("xmlns", upgrade.UpgradedXml, StringComparison.Ordinal);
        Assert.Empty(upgrade.RemovedFiles);

        // Written, it reads as the SDK-style WPF project it is.
        upgrade.Apply();
        var upgraded = ProjectParser.Load(path);
        Assert.False(ProjectParser.IsLegacyFrameworkProject(upgraded));
        Assert.Equal(ProjectKind.Wpf, upgraded.Kind);
        Assert.Equal(["net10.0-windows"], upgraded.TargetFrameworks);
        Assert.DoesNotContain(ProjectSourceFiles.Enumerate(upgraded), f => f.EndsWith("Unused.cs", StringComparison.Ordinal));
        Assert.Contains(ProjectSourceFiles.Enumerate(upgraded), f => f.EndsWith("MainWindow.xaml.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Upgrade_turns_packages_config_into_package_references_and_removes_it()
    {
        LegacyFrameworkProjects.WriteWindowsFormsApp(_temp.Path, "Hello");
        _temp.Write("Hello/packages.config", """
            <?xml version="1.0" encoding="utf-8"?>
            <packages>
              <package id="Newtonsoft.Json" version="13.0.3" targetFramework="net48" />
            </packages>
            """);
        var path = _temp.Resolve("Hello/Hello.csproj");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("""<Reference Include="System.Xml" />""", """
                <Reference Include="System.Xml" />
                    <Reference Include="Newtonsoft.Json, Version=13.0.0.0"><HintPath>..\packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll</HintPath></Reference>
                    <Reference Include="System.Speech" />
                """, StringComparison.Ordinal)
            .Replace("</Project>", """
                  <Import Project="..\packages\Some.Build.1.0.0\build\Some.Build.targets" Condition="Exists('..\packages\Some.Build.1.0.0\build\Some.Build.targets')" />
                  <Target Name="EnsureNuGetPackageBuildImports" BeforeTargets="PrepareForBuild" />
                </Project>
                """, StringComparison.Ordinal));

        var upgrade = LegacyProjectUpgrader.Plan(path);
        var project = XElement.Parse(upgrade.UpgradedXml);

        Assert.Equal("true", Property(project, "UseWindowsForms"));
        var packages = project.Descendants("PackageReference").ToDictionary(p => p.Attribute("Include")!.Value, p => p.Attribute("Version")?.Value);
        Assert.Equal("13.0.3", packages["Newtonsoft.Json"]);
        Assert.Equal("10.0.0", packages["System.Speech"]);
        Assert.DoesNotContain(project.Descendants(), e => e.Name.LocalName is "Reference" or "Import" or "Target");
        Assert.Equal([_temp.Resolve("Hello/packages.config")], upgrade.RemovedFiles);
        Assert.Contains(upgrade.Notes, n => n.Contains("packages.config", StringComparison.Ordinal));

        // Form1.resx and Form1.Designer.cs nest under Form1.cs by the SDK's convention, so nothing is said about them.
        Assert.DoesNotContain(project.Descendants(), e => e.Attribute("Update")?.Value is "Form1.resx" or "Form1.Designer.cs" or "Form1.cs");

        upgrade.Apply();
        Assert.False(File.Exists(_temp.Resolve("Hello/packages.config")));
        Assert.Equal(ProjectKind.WinForms, ProjectParser.Load(path).Kind);
    }

    [Fact]
    public void Upgrade_refuses_a_project_that_is_already_sdk_style()
    {
        var path = _temp.Write("App/App.csproj", """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");

        Assert.Throws<InvalidOperationException>(() => LegacyProjectUpgrader.Plan(path));
    }

    private static string? Property(XElement project, string name) =>
        project.Elements("PropertyGroup").Where(g => g.Attribute("Condition") is null).Elements(name).FirstOrDefault()?.Value;

    private static string Legacy(string outputType, string references, string items) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <PropertyGroup>
            <OutputType>{outputType}</OutputType>
            <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
          </PropertyGroup>
          <ItemGroup>
            {references}
          </ItemGroup>
          <ItemGroup>
            {items}
          </ItemGroup>
          <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
        </Project>
        """;
}
