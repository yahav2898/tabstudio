namespace TabStudio.Testing;

/// <summary>
/// .NET Framework projects in the legacy format, as Visual Studio's "WPF App (.NET Framework)" and "Windows Forms App
/// (.NET Framework)" templates write them, with a tutorial's code: a WPF calculator and a Windows Forms greeter. Each
/// has the template's Properties folder (AssemblyInfo.cs, Resources.resx, Settings.settings and their designer files)
/// and App.config.
/// </summary>
public static class LegacyFrameworkProjects
{
    /// <summary>Writes the WPF calculator and its solution.</summary>
    /// <param name="root">The folder for the solution.</param>
    /// <param name="name">The project's name, also its namespace.</param>
    /// <returns>The solution's path.</returns>
    public static string WriteWpfCalculator(string root, string name = "WpfCalculator")
    {
        var project = Path.Combine(root, name);
        Write(project, "App.xaml", $"""
            <Application x:Class="{name}.App"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="clr-namespace:{name}"
                         StartupUri="MainWindow.xaml">
                <Application.Resources>
                </Application.Resources>
            </Application>
            """);
        Write(project, "App.xaml.cs", $$"""
            using System;
            using System.Collections.Generic;
            using System.Configuration;
            using System.Data;
            using System.Linq;
            using System.Threading.Tasks;
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
        Write(project, "MainWindow.xaml", $"""
            <Window x:Class="{name}.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                    xmlns:local="clr-namespace:{name}"
                    mc:Ignorable="d"
                    Title="Calculator" Height="300" Width="420">
                <StackPanel Margin="16">
                    <TextBlock Text="WPF calculator from .NET Framework" FontSize="18" />
                    <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
                        <TextBox x:Name="numA" Width="80" Text="6" />
                        <TextBlock Text=" x " VerticalAlignment="Center" />
                        <TextBox x:Name="numB" Width="80" Text="7" />
                    </StackPanel>
                    <Button x:Name="multiply" Content="Multiply" Click="Multiply_Click" Margin="0,12,0,0" />
                    <TextBlock x:Name="result" Text="Result" FontSize="18" Margin="0,12,0,0" />
                </StackPanel>
            </Window>
            """);
        Write(project, "MainWindow.xaml.cs", $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
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

                    private void Multiply_Click(object sender, RoutedEventArgs e)
                    {
                        var product = Convert.ToInt32(numA.Text) * Convert.ToInt32(numB.Text);
                        result.Text = "Result: " + product;
                        Console.WriteLine("Multiplied to " + product + ", " + Properties.Settings.Default.Greeting);
                    }
                }
            }
            """);
        Write(project, "Properties/AssemblyInfo.cs", $$"""
            using System.Reflection;
            using System.Resources;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using System.Windows;

            [assembly: AssemblyTitle("{{name}}")]
            [assembly: AssemblyDescription("")]
            [assembly: AssemblyConfiguration("")]
            [assembly: AssemblyCompany("")]
            [assembly: AssemblyProduct("{{name}}")]
            [assembly: AssemblyCopyright("Copyright ©  2021")]
            [assembly: AssemblyTrademark("")]
            [assembly: AssemblyCulture("")]
            [assembly: ComVisible(false)]
            [assembly: ThemeInfo(
                ResourceDictionaryLocation.None,
                ResourceDictionaryLocation.SourceAssembly
            )]
            [assembly: AssemblyVersion("1.0.0.0")]
            [assembly: AssemblyFileVersion("1.0.0.0")]
            """);
        WriteProperties(project, name);
        Write(project, name + ".csproj", $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
              <PropertyGroup>
                <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
                <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
                <ProjectGuid>{E94ABD56-431F-4868-8A33-E51F60594E0E}</ProjectGuid>
                <OutputType>WinExe</OutputType>
                <RootNamespace>{{name}}</RootNamespace>
                <AssemblyName>{{name}}</AssemblyName>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <FileAlignment>512</FileAlignment>
                <ProjectTypeGuids>{60dc8134-eba5-43b8-bcc9-bb4bc16c2548};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
                <WarningLevel>4</WarningLevel>
                <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
                <Deterministic>true</Deterministic>
              </PropertyGroup>
              <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
                <PlatformTarget>AnyCPU</PlatformTarget>
                <DebugSymbols>true</DebugSymbols>
                <DebugType>full</DebugType>
                <Optimize>false</Optimize>
                <OutputPath>bin\Debug\</OutputPath>
                <DefineConstants>DEBUG;TRACE</DefineConstants>
                <ErrorReport>prompt</ErrorReport>
                <WarningLevel>4</WarningLevel>
              </PropertyGroup>
              <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
                <PlatformTarget>AnyCPU</PlatformTarget>
                <DebugType>pdbonly</DebugType>
                <Optimize>true</Optimize>
                <OutputPath>bin\Release\</OutputPath>
                <DefineConstants>TRACE</DefineConstants>
                <ErrorReport>prompt</ErrorReport>
                <WarningLevel>4</WarningLevel>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Reference Include="System.Data" />
                <Reference Include="System.Xml" />
                <Reference Include="Microsoft.CSharp" />
                <Reference Include="System.Core" />
                <Reference Include="System.Xml.Linq" />
                <Reference Include="System.Data.DataSetExtensions" />
                <Reference Include="System.Net.Http" />
                <Reference Include="System.Xaml">
                  <RequiredTargetFramework>4.0</RequiredTargetFramework>
                </Reference>
                <Reference Include="WindowsBase" />
                <Reference Include="PresentationCore" />
                <Reference Include="PresentationFramework" />
              </ItemGroup>
              <ItemGroup>
                <ApplicationDefinition Include="App.xaml">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </ApplicationDefinition>
                <Page Include="MainWindow.xaml">
                  <Generator>MSBuild:Compile</Generator>
                  <SubType>Designer</SubType>
                </Page>
                <Compile Include="App.xaml.cs">
                  <DependentUpon>App.xaml</DependentUpon>
                  <SubType>Code</SubType>
                </Compile>
                <Compile Include="MainWindow.xaml.cs">
                  <DependentUpon>MainWindow.xaml</DependentUpon>
                  <SubType>Code</SubType>
                </Compile>
              </ItemGroup>
              <ItemGroup>
            {{PropertiesItems}}
              </ItemGroup>
              <ItemGroup>
                <None Include="App.config" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
            </Project>
            """);
        return WriteSolution(root, name);
    }

    /// <summary>Writes the Windows Forms greeter and its solution.</summary>
    /// <param name="root">The folder for the solution.</param>
    /// <param name="name">The project's name, also its namespace.</param>
    /// <returns>The solution's path.</returns>
    public static string WriteWindowsFormsApp(string root, string name = "HelloFramework")
    {
        var project = Path.Combine(root, name);
        Write(project, "Program.cs", $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;
            using System.Windows.Forms;

            namespace {{name}}
            {
                internal static class Program
                {
                    /// <summary>
                    /// The main entry point for the application.
                    /// </summary>
                    [STAThread]
                    static void Main()
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new Form1());
                    }
                }
            }
            """);
        Write(project, "Form1.cs", $$"""
            using System;
            using System.Collections.Generic;
            using System.ComponentModel;
            using System.Data;
            using System.Drawing;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Windows.Forms;

            namespace {{name}}
            {
                public partial class Form1 : Form
                {
                    public Form1()
                    {
                        InitializeComponent();
                    }

                    private void button1_Click(object sender, EventArgs e)
                    {
                        label1.Text = "Hello from .NET Framework, " + Properties.Settings.Default.Greeting;
                    }

                    private void Form1_Load(object sender, EventArgs e)
                    {
                        Console.WriteLine("Form1 is ready.");
                    }
                }
            }
            """);
        Write(project, "Form1.Designer.cs", $$"""
            namespace {{name}}
            {
                partial class Form1
                {
                    /// <summary>
                    /// Required designer variable.
                    /// </summary>
                    private System.ComponentModel.IContainer components = null;

                    /// <summary>
                    /// Clean up any resources being used.
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
                    /// Required method for Designer support - do not modify
                    /// the contents of this method with the code editor.
                    /// </summary>
                    private void InitializeComponent()
                    {
                        this.button1 = new System.Windows.Forms.Button();
                        this.label1 = new System.Windows.Forms.Label();
                        this.SuspendLayout();
                        //
                        // button1
                        //
                        this.button1.Location = new System.Drawing.Point(24, 24);
                        this.button1.Name = "button1";
                        this.button1.Size = new System.Drawing.Size(120, 32);
                        this.button1.TabIndex = 0;
                        this.button1.Text = "Greet";
                        this.button1.UseVisualStyleBackColor = true;
                        this.button1.Click += new System.EventHandler(this.button1_Click);
                        //
                        // label1
                        //
                        this.label1.AutoSize = true;
                        this.label1.Location = new System.Drawing.Point(24, 80);
                        this.label1.Name = "label1";
                        this.label1.Size = new System.Drawing.Size(35, 13);
                        this.label1.TabIndex = 1;
                        this.label1.Text = "label1";
                        //
                        // Form1
                        //
                        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
                        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                        this.ClientSize = new System.Drawing.Size(400, 200);
                        this.Controls.Add(this.label1);
                        this.Controls.Add(this.button1);
                        this.Name = "Form1";
                        this.Text = "Hello .NET Framework";
                        this.Load += new System.EventHandler(this.Form1_Load);
                        this.ResumeLayout(false);
                        this.PerformLayout();

                    }

                    #endregion

                    private System.Windows.Forms.Button button1;
                    private System.Windows.Forms.Label label1;
                }
            }
            """);
        Write(project, "Form1.resx", Resx(string.Empty));
        Write(project, "Properties/AssemblyInfo.cs", $$"""
            using System.Reflection;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;

            [assembly: AssemblyTitle("{{name}}")]
            [assembly: AssemblyProduct("{{name}}")]
            [assembly: AssemblyCopyright("Copyright ©  2021")]
            [assembly: ComVisible(false)]
            [assembly: Guid("fe1d0e70-41ca-42aa-8740-d4e11e99089a")]
            [assembly: AssemblyVersion("1.0.0.0")]
            [assembly: AssemblyFileVersion("1.0.0.0")]
            """);
        WriteProperties(project, name);
        Write(project, name + ".csproj", $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
              <PropertyGroup>
                <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
                <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
                <ProjectGuid>{FE1D0E70-41CA-42AA-8740-D4E11E99089A}</ProjectGuid>
                <OutputType>WinExe</OutputType>
                <RootNamespace>{{name}}</RootNamespace>
                <AssemblyName>{{name}}</AssemblyName>
                <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
                <FileAlignment>512</FileAlignment>
                <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
                <Deterministic>true</Deterministic>
              </PropertyGroup>
              <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
                <PlatformTarget>AnyCPU</PlatformTarget>
                <DebugSymbols>true</DebugSymbols>
                <DebugType>full</DebugType>
                <Optimize>false</Optimize>
                <OutputPath>bin\Debug\</OutputPath>
                <DefineConstants>DEBUG;TRACE</DefineConstants>
                <ErrorReport>prompt</ErrorReport>
                <WarningLevel>4</WarningLevel>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Reference Include="System.Core" />
                <Reference Include="System.Xml.Linq" />
                <Reference Include="System.Data.DataSetExtensions" />
                <Reference Include="Microsoft.CSharp" />
                <Reference Include="System.Data" />
                <Reference Include="System.Deployment" />
                <Reference Include="System.Drawing" />
                <Reference Include="System.Net.Http" />
                <Reference Include="System.Windows.Forms" />
                <Reference Include="System.Xml" />
              </ItemGroup>
              <ItemGroup>
                <Compile Include="Form1.cs">
                  <SubType>Form</SubType>
                </Compile>
                <Compile Include="Form1.Designer.cs">
                  <DependentUpon>Form1.cs</DependentUpon>
                </Compile>
                <Compile Include="Program.cs" />
                <EmbeddedResource Include="Form1.resx">
                  <DependentUpon>Form1.cs</DependentUpon>
                </EmbeddedResource>
            {{PropertiesItems}}
              </ItemGroup>
              <ItemGroup>
                <None Include="App.config" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
            </Project>
            """);
        return WriteSolution(root, name);
    }

    /// <summary>A <c>.resx</c> file as Visual Studio writes it, with extra <c>data</c> elements.</summary>
    /// <param name="data">The data elements.</param>
    public static string Resx(string data) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="resmimetype">
            <value>text/microsoft-resx</value>
          </resheader>
          <resheader name="version">
            <value>2.0</value>
          </resheader>
          <resheader name="reader">
            <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
          </resheader>
          <resheader name="writer">
            <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
          </resheader>
        {data}
        </root>
        """;

    /// <summary>The template's items for its Properties folder.</summary>
    private const string PropertiesItems = """
            <Compile Include="Properties\AssemblyInfo.cs" />
            <Compile Include="Properties\Resources.Designer.cs">
              <AutoGen>True</AutoGen>
              <DesignTime>True</DesignTime>
              <DependentUpon>Resources.resx</DependentUpon>
            </Compile>
            <Compile Include="Properties\Settings.Designer.cs">
              <AutoGen>True</AutoGen>
              <DependentUpon>Settings.settings</DependentUpon>
              <DesignTimeSharedInput>True</DesignTimeSharedInput>
            </Compile>
            <EmbeddedResource Include="Properties\Resources.resx">
              <Generator>ResXFileCodeGenerator</Generator>
              <LastGenOutput>Resources.Designer.cs</LastGenOutput>
            </EmbeddedResource>
            <None Include="Properties\Settings.settings">
              <Generator>SettingsSingleFileGenerator</Generator>
              <LastGenOutput>Settings.Designer.cs</LastGenOutput>
            </None>
        """;

    /// <summary>The template's Properties folder: resources with one string, and settings with one user setting; and App.config.</summary>
    private static void WriteProperties(string project, string name)
    {
        Write(project, "Properties/Resources.resx", Resx("""
              <data name="Title" xml:space="preserve">
                <value>Made with .NET Framework</value>
              </data>
            """));
        Write(project, "Properties/Resources.Designer.cs", $$"""
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //     Runtime Version:4.0.30319.42000
            // </auto-generated>
            //------------------------------------------------------------------------------

            namespace {{name}}.Properties {
                using System;

                [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
                [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                internal class Resources {

                    private static global::System.Resources.ResourceManager resourceMan;

                    private static global::System.Globalization.CultureInfo resourceCulture;

                    [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
                    internal Resources() {
                    }

                    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
                    internal static global::System.Resources.ResourceManager ResourceManager {
                        get {
                            if (object.ReferenceEquals(resourceMan, null)) {
                                global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("{{name}}.Properties.Resources", typeof(Resources).Assembly);
                                resourceMan = temp;
                            }
                            return resourceMan;
                        }
                    }

                    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
                    internal static global::System.Globalization.CultureInfo Culture {
                        get {
                            return resourceCulture;
                        }
                        set {
                            resourceCulture = value;
                        }
                    }

                    internal static string Title {
                        get {
                            return ResourceManager.GetString("Title", resourceCulture);
                        }
                    }
                }
            }
            """);
        Write(project, "Properties/Settings.settings", $"""
            <?xml version='1.0' encoding='utf-8'?>
            <SettingsFile xmlns="http://schemas.microsoft.com/VisualStudio/2004/01/settings" CurrentProfile="(Default)" GeneratedClassNamespace="{name}.Properties" GeneratedClassName="Settings">
              <Profiles />
              <Settings>
                <Setting Name="Greeting" Type="System.String" Scope="User">
                  <Value Profile="(Default)">from its settings</Value>
                </Setting>
              </Settings>
            </SettingsFile>
            """);
        Write(project, "Properties/Settings.Designer.cs", $$"""
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //     Runtime Version:4.0.30319.42000
            // </auto-generated>
            //------------------------------------------------------------------------------

            namespace {{name}}.Properties {


                [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
                [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
                internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {

                    private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));

                    public static Settings Default {
                        get {
                            return defaultInstance;
                        }
                    }

                    [global::System.Configuration.UserScopedSettingAttribute()]
                    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                    [global::System.Configuration.DefaultSettingValueAttribute("from its settings")]
                    public string Greeting {
                        get {
                            return ((string)(this["Greeting"]));
                        }
                        set {
                            this["Greeting"] = value;
                        }
                    }
                }
            }
            """);
        Write(project, "App.config", """
            <?xml version="1.0" encoding="utf-8" ?>
            <configuration>
                <startup>
                    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" />
                </startup>
            </configuration>
            """);
    }

    private static string WriteSolution(string root, string name) =>
        Write(root, name + ".sln", $$"""

            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}", "{{name}}\{{name}}.csproj", "{E94ABD56-431F-4868-8A33-E51F60594E0E}"
            EndProject
            Global
            	GlobalSection(SolutionConfigurationPlatforms) = preSolution
            		Debug|Any CPU = Debug|Any CPU
            		Release|Any CPU = Release|Any CPU
            	EndGlobalSection
            EndGlobal
            """);

    private static string Write(string folder, string relativePath, string content)
    {
        var path = Path.Combine(folder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.Replace("\n", "\r\n", StringComparison.Ordinal));
        return path;
    }
}
