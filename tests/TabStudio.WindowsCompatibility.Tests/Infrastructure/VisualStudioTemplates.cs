using TabStudio.Testing;

namespace TabStudio.WindowsCompatibility.Tests.Infrastructure;

/// <summary>
/// Visual Studio's Windows Forms App and WPF Application templates, file for file as <c>dotnet new winforms</c> and
/// <c>dotnet new wpf</c> write them (and as TabStudio's own templates do).
/// </summary>
internal static class VisualStudioTemplates
{
    /// <summary>Writes the Windows Forms App template.</summary>
    /// <returns>The project file.</returns>
    public static string WriteWindowsForms(TempDirectory temp, string name)
    {
        temp.Write($"{name}/Program.cs", $$"""
            namespace {{name}}
            {
                internal static class Program
                {
                    /// <summary>
                    ///  The main entry point for the application.
                    /// </summary>
                    [STAThread]
                    static void Main()
                    {
                        // To customize application configuration such as set high DPI settings or default font,
                        // see https://aka.ms/applicationconfiguration.
                        ApplicationConfiguration.Initialize();
                        Application.Run(new Form1());
                    }
                }
            }
            """);
        temp.Write($"{name}/Form1.cs", $$"""
            namespace {{name}}
            {
                public partial class Form1 : Form
                {
                    public Form1()
                    {
                        InitializeComponent();
                    }
                }
            }
            """);
        temp.Write($"{name}/Form1.Designer.cs", $$"""
            namespace {{name}}
            {
                partial class Form1
                {
                    /// <summary>
                    ///  Required designer variable.
                    /// </summary>
                    private System.ComponentModel.IContainer components = null;

                    /// <summary>
                    ///  Clean up any resources being used.
                    /// </summary>
                    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
                    protected override void Dispose(bool disposing)
                    {
                        if (disposing && (components != null))
                        {
                            components.Dispose();
                        }
                        base.Dispose(disposing);
                    }

                    #region Windows Form Designer generated code

                    /// <summary>
                    ///  Required method for Designer support - do not modify
                    ///  the contents of this method with the code editor.
                    /// </summary>
                    private void InitializeComponent()
                    {
                        this.components = new System.ComponentModel.Container();
                        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                        this.ClientSize = new System.Drawing.Size(800, 450);
                        this.Text = "Form1";
                    }

                    #endregion
                }
            }
            """);
        return temp.Write($"{name}/{name}.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <Nullable>enable</Nullable>
                <UseWindowsForms>true</UseWindowsForms>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>

            </Project>
            """);
    }

    /// <summary>Writes the WPF Application template.</summary>
    /// <returns>The project file.</returns>
    public static string WriteWpf(TempDirectory temp, string name)
    {
        temp.Write($"{name}/App.xaml", $$"""
            <Application x:Class="{{name}}.App"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="clr-namespace:{{name}}"
                         StartupUri="MainWindow.xaml">
                <Application.Resources>

                </Application.Resources>
            </Application>
            """);
        temp.Write($"{name}/App.xaml.cs", $$"""
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
        temp.Write($"{name}/AssemblyInfo.cs", """
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
        temp.Write($"{name}/MainWindow.xaml", $$"""
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
        temp.Write($"{name}/MainWindow.xaml.cs", $$"""
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
        return temp.Write($"{name}/{name}.csproj", """
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
    }

    /// <summary>Writes the template's MainWindow.xaml with other content, which starts on line 10.</summary>
    public static void WriteWpfWindow(TempDirectory temp, string name, string content) =>
        temp.Write($"{name}/MainWindow.xaml", $$"""
            <Window x:Class="{{name}}.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                    xmlns:local="clr-namespace:{{name}}"
                    mc:Ignorable="d"
                    Title="MainWindow" Height="450" Width="800">
                <!-- The window's content. -->
                {{content}}
            </Window>
            """);
}
