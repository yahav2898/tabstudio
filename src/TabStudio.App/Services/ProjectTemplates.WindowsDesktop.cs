namespace TabStudio.App.Services;

/// <summary>
/// The Windows desktop templates, Visual Studio's own file for file, so the projects also open and run in Visual
/// Studio on a PC: WPF Application and Windows Forms App. On the tablet they run as the real Windows app under
/// Windows Compatibility, or on another host the Compatibility Doctor offers.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A WPF app with a main window.</summary>
    public const string WpfId = "wpf";

    /// <summary>A Windows Forms app with a form.</summary>
    public const string WindowsFormsId = "winforms";

    /// <summary>The Windows desktop templates, for the start screen.</summary>
    private static ProjectTemplate[] WindowsDesktopTemplates =>
    [
        new(WpfId, "WPF Application", "A Windows desktop app with XAML windows, run on the tablet as the real Windows app, beside the code.", ["C#", "Windows", "Desktop", "WPF"]),
        new(WindowsFormsId, "Windows Forms App", "A Windows desktop app with a form, run on the tablet as the real Windows app, beside the code.", ["C#", "Windows", "Desktop", "WinForms"]),
    ];

    private static string WriteWpf(string root, string name)
    {
        var directory = Path.Combine(root, name);
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

    private static string WriteWindowsForms(string root, string name)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);

        var project = Path.Combine(directory, name + ".csproj");
        File.WriteAllText(project, """
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

        File.WriteAllText(Path.Combine(directory, "Program.cs"), $$"""
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

        File.WriteAllText(Path.Combine(directory, "Form1.cs"), $$"""
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

        File.WriteAllText(Path.Combine(directory, "Form1.Designer.cs"), $$"""
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
        return project;
    }
}
