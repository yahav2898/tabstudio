namespace TabStudio.Wpf.Templates;

/// <summary>
/// Visual Studio's "WPF Application" template (<c>dotnet new wpf</c>), file for file, so the project also opens and runs
/// in Visual Studio on a PC. In TabStudio it builds on OpenSilver and runs in the web preview.
/// </summary>
public static class WpfTemplates
{
    /// <summary>The template's id, as <c>dotnet new</c> knows it.</summary>
    public const string WpfApplicationId = "wpf";

    /// <summary>Writes the template's project into a folder of its own under a solution folder.</summary>
    /// <param name="solutionDirectory">The solution folder.</param>
    /// <param name="name">The project's name, which is also its namespace.</param>
    /// <returns>The project file.</returns>
    public static string Create(string solutionDirectory, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(solutionDirectory);
        ArgumentException.ThrowIfNullOrEmpty(name);
        var directory = Path.Combine(solutionDirectory, name);
        Directory.CreateDirectory(directory);

        var project = Path.Combine(directory, name + ".csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <UseWPF>true</UseWPF>
              </PropertyGroup>

            </Project>

            """);

        File.WriteAllText(Path.Combine(directory, "App.xaml"), $$"""
            <Application x:Class="{{name}}.App"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="clr-namespace:{{name}}"
                         StartupUri="MainWindow.xaml">
                <Application.Resources>

                </Application.Resources>
            </Application>

            """);

        File.WriteAllText(Path.Combine(directory, "App.xaml.cs"), $$"""
            using System.Configuration;
            using System.Data;
            using System.Windows;

            namespace {{name}}
            {
                /// <summary>
                /// Interaction logic for App.xaml
                /// </summary>
                public partial class App : Application
                {
                }

            }

            """);

        File.WriteAllText(Path.Combine(directory, "AssemblyInfo.cs"), """
            using System.Windows;

            [assembly: ThemeInfo(
                ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                            //(used if a resource is not found in the page,
                                                            // or application resource dictionaries)
                ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                            //(used if a resource is not found in the page,
                                                            // app, or any theme specific resource dictionaries)
            )]

            """);

        File.WriteAllText(Path.Combine(directory, "MainWindow.xaml"), $$"""
            <Window x:Class="{{name}}.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                    xmlns:local="clr-namespace:{{name}}"
                    mc:Ignorable="d"
                    Title="MainWindow" Height="450" Width="800">
                <Grid>

                </Grid>
            </Window>

            """);

        File.WriteAllText(Path.Combine(directory, "MainWindow.xaml.cs"), $$"""
            using System.Text;
            using System.Windows;
            using System.Windows.Controls;
            using System.Windows.Data;
            using System.Windows.Documents;
            using System.Windows.Input;
            using System.Windows.Media;
            using System.Windows.Media.Imaging;
            using System.Windows.Navigation;
            using System.Windows.Shapes;

            namespace {{name}}
            {
                /// <summary>
                /// Interaction logic for MainWindow.xaml
                /// </summary>
                public partial class MainWindow : Window
                {
                    public MainWindow()
                    {
                        InitializeComponent();
                    }
                }
            }

            """);

        return project;
    }
}
