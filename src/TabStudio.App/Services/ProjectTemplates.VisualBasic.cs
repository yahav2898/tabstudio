using System.Text;

namespace TabStudio.App.Services;

/// <summary>
/// Visual Basic templates, file for file the ones Visual Studio and <c>dotnet new</c> write, so they also open in
/// Visual Studio on a PC: a console app, a Windows Forms app with the Application Framework (<c>My Project</c>), and
/// a class library. TabStudio compiles them in-process, as it does C#.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A Visual Basic console app.</summary>
    public const string VisualBasicConsoleId = "vb-console";

    /// <summary>A Visual Basic Windows Forms app with the Application Framework.</summary>
    public const string VisualBasicWindowsFormsId = "vb-winforms";

    /// <summary>A Visual Basic class library.</summary>
    public const string VisualBasicLibraryId = "vb-classlib";

    /// <summary>Visual Studio writes Visual Basic files in UTF-8 with a byte order mark.</summary>
    private static readonly Encoding VisualBasicEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static ProjectTemplate[] VisualBasicTemplates =>
    [
        new(VisualBasicConsoleId, "Console App (Visual Basic)", "A Visual Basic command-line program, compiled and debugged on the tablet like C#.", ["Visual Basic", "Console"]),
        new(VisualBasicWindowsFormsId, "Windows Forms App (Visual Basic)", "A Visual Basic Windows Forms app with My.Application, designed by touch and run on TabStudio's Forms layer.", ["Visual Basic", "Windows", "Desktop", "WinForms"]),
        new(VisualBasicLibraryId, "Class Library (Visual Basic)", "Reusable Visual Basic code for other projects to reference.", ["Visual Basic", "Library"]),
    ];

    private static void WriteVisualBasic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, VisualBasicEncoding);
    }

    private static string WriteVisualBasicConsole(string root, string name)
    {
        var directory = Path.Combine(root, name);
        var project = Path.Combine(directory, name + ".vbproj");
        WriteVisualBasic(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <RootNamespace>{name}</RootNamespace>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>

            </Project>

            """);
        WriteVisualBasic(Path.Combine(directory, "Program.vb"), """
            Imports System

            ' Press F5 to build and run this program on your tablet; set a breakpoint first to stop in the debugger.
            Module Program
                Sub Main(args As String())
                    Console.Write("What is your name? ")
                    Dim name = Console.ReadLine()?.Trim()
                    Console.WriteLine(Greet(If(String.IsNullOrEmpty(name), "world", name)))
                End Sub

                Function Greet(name As String) As String
                    Dim answer = 42
                    Return $"Hello, {name}! This is Visual Basic on TabStudio. The answer is {answer}."
                End Function
            End Module

            """);
        return project;
    }

    private static string WriteVisualBasicLibrary(string root, string name)
    {
        var directory = Path.Combine(root, name);
        var project = Path.Combine(directory, name + ".vbproj");
        WriteVisualBasic(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <RootNamespace>{name}</RootNamespace>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>

            </Project>

            """);
        WriteVisualBasic(Path.Combine(directory, "Class1.vb"), """
            Public Class Class1

            End Class

            """);
        return project;
    }

    /// <summary>
    /// Visual Studio's Windows Forms App (Visual Basic): the Application Framework starts Form1 from
    /// <c>My Project\Application.myapp</c>, whose generated <c>Application.Designer.vb</c> TabStudio keeps in step.
    /// </summary>
    private static string WriteVisualBasicWindowsForms(string root, string name)
    {
        var directory = Path.Combine(root, name);
        var project = Path.Combine(directory, name + ".vbproj");
        WriteVisualBasic(project, """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net10.0-windows</TargetFramework>
                <StartupObject>Sub Main</StartupObject>
                <UseWindowsForms>true</UseWindowsForms>
                <MyType>WindowsForms</MyType>
              </PropertyGroup>

              <ItemGroup>
                <Import Include="System.Data" />
                <Import Include="System.Drawing" />
                <Import Include="System.Windows.Forms" />
              </ItemGroup>

              <ItemGroup>
                <Compile Update="My Project\Application.Designer.vb">
                  <DesignTime>True</DesignTime>
                  <AutoGen>True</AutoGen>
                  <DependentUpon>Application.myapp</DependentUpon>
                </Compile>
              </ItemGroup>

              <ItemGroup>
                <None Update="My Project\Application.myapp">
                  <Generator>MyApplicationCodeGenerator</Generator>
                  <LastGenOutput>Application.Designer.vb</LastGenOutput>
                </None>
              </ItemGroup>

            </Project>
            """);
        WriteVisualBasic(Path.Combine(directory, "My Project", "Application.myapp"), """
            <?xml version="1.0" encoding="utf-8"?>
            <MyApplicationData xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <MySubMain>true</MySubMain>
              <MainForm>Form1</MainForm>
              <SingleInstance>false</SingleInstance>
              <ShutdownMode>0</ShutdownMode>
              <EnableVisualStyles>true</EnableVisualStyles>
              <AuthenticationMode>0</AuthenticationMode>
              <SaveMySettingsOnExit>true</SaveMySettingsOnExit>
            </MyApplicationData>
            """);
        WriteVisualBasic(Path.Combine(directory, "My Project", "Application.Designer.vb"), """
            '------------------------------------------------------------------------------
            ' <auto-generated>
            '     This code was generated by a tool.
            '     Runtime Version:4.0.30319.42000
            '
            '     Changes to this file may cause incorrect behavior and will be lost if
            '     the code is regenerated.
            ' </auto-generated>
            '------------------------------------------------------------------------------

            Namespace My

                'NOTE: This file is auto-generated; do not modify it directly. To make changes,
                ' or if you encounter build errors in this file, go to the Project Designer
                ' (go to Project Properties or double-click the My Project node in
                ' Solution Explorer), and make changes on the Application tab.
                '
                Partial Friend Class MyApplication

                    <Global.System.Diagnostics.DebuggerStepThroughAttribute()>
                    Public Sub New()
                        MyBase.New(Global.Microsoft.VisualBasic.ApplicationServices.AuthenticationMode.Windows)
                        Me.IsSingleInstance = False
                        Me.EnableVisualStyles = True
                        Me.SaveMySettingsOnExit = True
                        Me.ShutDownStyle = Global.Microsoft.VisualBasic.ApplicationServices.ShutdownMode.AfterMainFormCloses
                    End Sub

                    <Global.System.Diagnostics.DebuggerStepThroughAttribute()>
                    Protected Overrides Sub OnCreateMainForm()
                        Me.MainForm = Form1
                    End Sub
                End Class
            End Namespace

            """);
        WriteVisualBasic(Path.Combine(directory, "ApplicationEvents.vb"), """
            Imports Microsoft.VisualBasic.ApplicationServices

            Namespace My
                ' The following events are available for MyApplication:
                ' Startup: Raised when the application starts, before the startup form is created.
                ' Shutdown: Raised after all application forms are closed. This event is not raised if the application terminates abnormally.
                ' UnhandledException: Raised if the application encounters an unhandled exception.
                ' StartupNextInstance: Raised when launching a single-instance application and the application is already active.
                ' NetworkAvailabilityChanged: Raised when the network connection is connected or disconnected.

                ' **NEW** ApplyApplicationDefaults: Raised when the application queries default values to be set for the application.

                ' Example:
                ' Private Sub MyApplication_ApplyApplicationDefaults(sender As Object, e As ApplyApplicationDefaultsEventArgs) Handles Me.ApplyApplicationDefaults
                '
                '   ' Setting the application-wide default Font:
                '   e.Font = New Font(FontFamily.GenericSansSerif, 12, FontStyle.Regular)
                '
                '   ' Setting the HighDpiMode for the Application:
                '   e.HighDpiMode = HighDpiMode.PerMonitorV2
                '
                '   ' If a splash dialog is used, this sets the minimum display time:
                '   e.MinimumSplashScreenDisplayTime = 4000
                ' End Sub

                Partial Friend Class MyApplication

                End Class
            End Namespace

            """);
        WriteVisualBasic(Path.Combine(directory, "Form1.vb"), """
            Public Class Form1

            End Class

            """);
        WriteVisualBasic(Path.Combine(directory, "Form1.Designer.vb"), """
            <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
            Partial Class Form1
                Inherits System.Windows.Forms.Form

                'Form overrides dispose to clean up the component list.
                <System.Diagnostics.DebuggerNonUserCode()>
                Protected Overrides Sub Dispose(disposing As Boolean)
                    Try
                        If disposing AndAlso components IsNot Nothing Then
                            components.Dispose()
                        End If
                    Finally
                        MyBase.Dispose(disposing)
                    End Try
                End Sub

                'Required by the Windows Form Designer
                Private components As System.ComponentModel.IContainer

                'NOTE: The following procedure is required by the Windows Form Designer
                'It can be modified using the Windows Form Designer.
                'Do not modify it using the code editor.
                <System.Diagnostics.DebuggerStepThrough()>
                Private Sub InitializeComponent()
                    components = New System.ComponentModel.Container()
                    AutoScaleMode = AutoScaleMode.Font
                    ClientSize = New Size(800, 450)
                    Text = "Form1"
                End Sub

            End Class

            """);
        return project;
    }
}
