namespace TabStudio.App.Services;

/// <summary>
/// The Windows Forms App template: Visual Studio's own, file for file, so the project also opens and runs in Visual
/// Studio on a PC. In TabStudio it runs on the Forms layer in the app preview, and Form1 opens in the Forms designer.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A Windows Forms app with a form, run in the app preview and designed in the Forms designer.</summary>
    public const string WindowsFormsId = "winforms";

    /// <summary>The start screen's entry for the Windows Forms App template.</summary>
    private static ProjectTemplate WindowsFormsTemplate => new(
        WindowsFormsId,
        "Windows Forms App",
        "A Windows desktop app with a form you design by touch, run beside the code on TabStudio's Forms layer.",
        ["C#", "Windows", "Desktop", "WinForms"]);

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
