using TabStudio.Testing;
using TabStudio.WindowsBridge.Hosting;

namespace TabStudio.WindowsBridge.Windows.Tests;

/// <summary>
/// The apps the Windows tests build: the .NET SDK's WPF and Windows Forms templates with a button that turns the
/// window green, a Visual C++ console program and a .NET Framework one, each in a folder of the tablet's.
/// </summary>
internal static class TemplateApps
{
    /// <summary>The window's color after its button was clicked.</summary>
    public const uint Green = 0xFF20A040;

    /// <summary>The line of <c>InitializeComponent()</c> in the WPF app's MainWindow.xaml.cs, for breakpoints.</summary>
    public const int WpfConstructorLine = 10;

    /// <summary>A WPF app from <c>dotnet new wpf</c>, titled "Hello WPF".</summary>
    public static async Task<string> CreateWpfAsync(TempDirectory folder)
    {
        var directory = folder.Resolve("Tablet/HelloWpf");
        await DotNetNewAsync("wpf", "HelloWpf", directory);
        folder.Write("Tablet/HelloWpf/MainWindow.xaml", """
            <Window x:Class="HelloWpf.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Hello WPF" Width="480" Height="360" WindowStartupLocation="CenterScreen">
                <Grid x:Name="Root" Background="#F3F3F3">
                    <Button x:Name="Greet" Content="Click me" Width="200" Height="80" FontSize="20" Click="OnClick" />
                </Grid>
            </Window>
            """);
        folder.Write("Tablet/HelloWpf/MainWindow.xaml.cs", """
            using System.Windows;
            using System.Windows.Media;

            namespace HelloWpf;

            public partial class MainWindow : Window
            {
                public MainWindow()
                {
                    InitializeComponent();
                }

                private void OnClick(object sender, RoutedEventArgs e)
                {
                    Root.Background = new SolidColorBrush(Color.FromRgb(0x20, 0xA0, 0x40));
                    Greet.Content = "Clicked";
                }
            }
            """);
        return Path.Combine(directory, "HelloWpf.csproj");
    }

    /// <summary>A Windows Forms app from <c>dotnet new winforms</c>, titled "Hello Forms".</summary>
    public static async Task<string> CreateWinFormsAsync(TempDirectory folder)
    {
        var directory = folder.Resolve("Tablet/HelloForms");
        await DotNetNewAsync("winforms", "HelloForms", directory);
        File.Delete(Path.Combine(directory, "Form1.Designer.cs"));
        File.Delete(Path.Combine(directory, "Form1.resx"));
        folder.Write("Tablet/HelloForms/Form1.cs", """
            namespace HelloForms;

            public sealed class Form1 : Form
            {
                private readonly Button _greet = new() { Text = "Click me", Size = new Size(200, 80) };

                public Form1()
                {
                    Text = "Hello Forms";
                    ClientSize = new Size(480, 320);
                    BackColor = Color.FromArgb(0xF3, 0xF3, 0xF3);
                    StartPosition = FormStartPosition.CenterScreen;
                    _greet.Location = new Point((ClientSize.Width - _greet.Width) / 2, (ClientSize.Height - _greet.Height) / 2);
                    _greet.Click += (_, _) => BackColor = Color.FromArgb(0x20, 0xA0, 0x40);
                    Controls.Add(_greet);
                }
            }
            """);
        return Path.Combine(directory, "HelloForms.csproj");
    }

    /// <summary>A Visual C++ console program, with <paramref name="body"/> as its <c>main</c>.</summary>
    public static string CreateCpp(TempDirectory folder, string body)
    {
        folder.Write("Tablet/HelloCpp/main.cpp", $$"""
            #include <cstdio>

            int main()
            {
                {{body}}
            }
            """);
        return folder.Write("Tablet/HelloCpp/HelloCpp.vcxproj", """
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup Label="ProjectConfigurations">
                <ProjectConfiguration Include="Debug|x64">
                  <Configuration>Debug</Configuration>
                  <Platform>x64</Platform>
                </ProjectConfiguration>
              </ItemGroup>
              <PropertyGroup Label="Globals">
                <VCProjectVersion>17.0</VCProjectVersion>
                <ProjectGuid>{5B0C6A8E-2F0F-4C55-9E32-7C0D5A1E9B21}</ProjectGuid>
                <RootNamespace>HelloCpp</RootNamespace>
                <WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>
              </PropertyGroup>
              <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
              <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'" Label="Configuration">
                <ConfigurationType>Application</ConfigurationType>
                <UseDebugLibraries>true</UseDebugLibraries>
                <PlatformToolset>$(DefaultPlatformToolset)</PlatformToolset>
                <CharacterSet>Unicode</CharacterSet>
              </PropertyGroup>
              <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
              <ItemDefinitionGroup>
                <ClCompile>
                  <WarningLevel>Level3</WarningLevel>
                </ClCompile>
                <Link>
                  <SubSystem>Console</SubSystem>
                </Link>
              </ItemDefinitionGroup>
              <ItemGroup>
                <ClCompile Include="main.cpp" />
              </ItemGroup>
              <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
            </Project>
            """);
    }

    /// <summary>A .NET Framework 4.8 console program.</summary>
    public static string CreateNetFramework(TempDirectory folder)
    {
        folder.Write("Tablet/HelloFramework/Program.cs", """
            internal static class Program
            {
                private static int Main()
                {
                    System.Console.WriteLine("Hello from .NET Framework " + System.Environment.Version);
                    return 0;
                }
            }
            """);
        return folder.Write("Tablet/HelloFramework/HelloFramework.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net48</TargetFramework>
                <LangVersion>latest</LangVersion>
              </PropertyGroup>
            </Project>
            """);
    }

    private static async Task DotNetNewAsync(string template, string name, string directory)
    {
        Directory.CreateDirectory(directory);
        using var process = BridgeProcess.Start(new BridgeProcessStart("dotnet", ["new", template, "-n", name, "-o", directory, "--force"], directory));
        var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
        process.OutputLine += (_, line) => output.Enqueue(line);
        process.ErrorLine += (_, line) => output.Enqueue(line);
        process.BeginReading();
        var code = await process.Exited.WaitAsync(TimeSpan.FromMinutes(3));
        if (code != 0)
        {
            throw new InvalidOperationException($"dotnet new {template} failed with {code}:\n{string.Join('\n', output)}");
        }
    }
}
