using System.Reflection;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.VisualBasic.ApplicationServices;
using TabStudio.Compilation;
using TabStudio.Compilation.VisualBasic;
using TabStudio.VisualBasic.Forms.Tests.Infrastructure;

namespace TabStudio.VisualBasic.Forms.Tests;

/// <summary>
/// Visual Basic programs compiled with Roslyn's My namespace (<c>_MyType</c> WindowsForms and Console) against .NET's
/// reference assemblies and TabStudio's Microsoft.VisualBasic.Forms, as Visual Studio's templates write them, and run.
/// </summary>
public sealed class VisualBasicProgramTests
{
    private static readonly MetadataReference VisualBasicForms = MetadataReference.CreateFromFile(typeof(ApplicationBase).Assembly.Location);
    private static readonly MetadataReference FormsLayer = MetadataReference.CreateFromFile(typeof(Form).Assembly.Location);

    private const string ApplicationDesigner = """
        Namespace My
            Partial Friend Class MyApplication
                <Global.System.Diagnostics.DebuggerStepThroughAttribute()>
                Public Sub New()
                    MyBase.New(Global.Microsoft.VisualBasic.ApplicationServices.AuthenticationMode.Windows)
                    Me.IsSingleInstance = False
                    Me.EnableVisualStyles = True
                    Me.SaveMySettingsOnExit = True
                    Me.ShutDownStyle = Global.Microsoft.VisualBasic.ApplicationServices.ShutdownMode.AfterMainFormCloses
                    Me.HighDpiMode = HighDpiMode.DpiUnawareGdiScaled
                End Sub

                <Global.System.Diagnostics.DebuggerStepThroughAttribute()>
                Protected Overrides Sub OnCreateMainForm()
                    Me.MainForm = Form1
                End Sub
            End Class
        End Namespace
        """;

    private const string ApplicationEvents = """
        Imports Microsoft.VisualBasic.ApplicationServices

        Namespace My
            Partial Friend Class MyApplication
                Private Sub MyApplication_ApplyApplicationDefaults(sender As Object, e As ApplyApplicationDefaultsEventArgs) Handles Me.ApplyApplicationDefaults
                    e.MinimumSplashScreenDisplayTime = 0
                    AppLog.Events.Add("ApplyApplicationDefaults")
                End Sub

                Private Sub MyApplication_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
                    AppLog.Events.Add("Startup")
                End Sub

                Private Sub MyApplication_Shutdown(sender As Object, e As EventArgs) Handles Me.Shutdown
                    AppLog.Events.Add("Shutdown")
                End Sub

                Private Sub MyApplication_UnhandledException(sender As Object, e As Global.Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs) Handles Me.UnhandledException
                    AppLog.Events.Add("UnhandledException " & e.Exception.Message)
                End Sub
            End Class
        End Namespace
        """;

    private const string Form1 = """
        Public Class Form1
            Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
                AppLog.Events.Add("Load " & My.Application.Info.AssemblyName)
            End Sub

            ' Never called: it shows that the My namespace's members compile.
            Friend Sub UsesMy()
                Dim localTime As Date = My.Computer.Clock.LocalTime
                Dim utcTime As Date = My.Computer.Clock.GmtTime
                Dim osName As String = My.Computer.Info.OSFullName
                Dim memory As ULong = My.Computer.Info.TotalPhysicalMemory
                Dim userName As String = My.User.Name
                Dim appTitle As String = My.Application.Info.Title
                Dim appVersion As Version = My.Application.Info.Version
                Dim temp As String = My.Computer.FileSystem.SpecialDirectories.Temp
                Dim exists As Boolean = My.Computer.FileSystem.FileExists(IO.Path.Combine(temp, "stock.txt"))
                My.Computer.Clipboard.SetText("hello")
                Dim answer As MsgBoxResult = MsgBox("Continue?", MsgBoxStyle.YesNo Or MsgBoxStyle.Question, "Question")
                Dim typed As String = InputBox("Name?", "Greeting", "Ada")
                My.Application.Log.WriteEntry("started")
                Dim online As Boolean = My.Computer.Network.IsAvailable
                Dim screenBounds As Rectangle = My.Computer.Screen.Bounds
                Dim openCount As Integer = My.Application.OpenForms.Count
                Dim arguments = My.Application.CommandLineArgs
                Dim shift As Boolean = My.Computer.Keyboard.ShiftKeyDown
                Dim wheel As Boolean = My.Computer.Mouse.WheelExists
                Dim defaultForm As Form1 = My.Forms.Form1
                Dim culture As Globalization.CultureInfo = My.Application.Culture
                My.Application.DoEvents()
            End Sub
        End Class
        """;

    private const string Form1Designer = """
        <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
        Partial Class Form1
            Inherits System.Windows.Forms.Form

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

            Private components As System.ComponentModel.IContainer

            <System.Diagnostics.DebuggerStepThrough()>
            Private Sub InitializeComponent()
                components = New System.ComponentModel.Container()
                AutoScaleMode = AutoScaleMode.Font
                ClientSize = New Size(800, 450)
                Text = "Form1"
            End Sub

        End Class
        """;

    private const string AppLog = """
        Public Module AppLog
            Public ReadOnly Events As New List(Of String)
        End Module
        """;

    private const string ConsoleProgram = """
        Module Program
            Sub Main(args As String())
                Console.WriteLine(Describe())
            End Sub

            Public Function Describe() As String
                Dim computer As String = My.Computer.Name & My.Computer.Info.OSFullName & My.Computer.Clock.GmtTime.ToString()
                Dim folder As String = My.Computer.FileSystem.CurrentDirectory
                Dim authenticated As Boolean = My.User.IsAuthenticated
                Return My.Application.Info.AssemblyName & "|" & My.Application.CommandLineArgs.Count
            End Function
        End Module
        """;

    [Fact]
    public void A_Windows_Forms_app_with_the_application_framework_compiles_with_Sub_Main_from_the_My_namespace()
    {
        var compilation = CompileWindowsFormsApp();

        AssertNoErrors(compilation);
        var entryPoint = compilation.GetEntryPoint(CancellationToken.None);
        Assert.NotNull(entryPoint);
        Assert.Equal("MyApplication", entryPoint.ContainingType.Name);
    }

    [AvaloniaFact]
    public void A_Windows_Forms_app_with_the_application_framework_runs_its_main_form_and_events()
    {
        var assembly = Load(CompileWindowsFormsApp());
        var entryPoint = assembly.EntryPoint!;

        using var run = FormsTestRun.StartMain(() =>
        {
            entryPoint.Invoke(null, [Array.Empty<string>()]);
            return 0;
        });

        Assert.True(run.Presenter.WaitFor(frames => frames.Count > 0, FormsTestRun.Timeout), "Form1 was never shown.");
        Assert.Equal("Form1", run.Invoke(() => Application.OpenForms[0]!.Text));
        run.Post(() => Application.OpenForms[0]!.Close());

        Assert.True(run.Exit.Wait(FormsTestRun.Timeout), "The app did not end when Form1 closed.");
        Assert.Null(run.Exit.Result.Error);
        var events = (List<string>)assembly.GetType("WinFormsApp1.AppLog", throwOnError: true)!.GetField("Events")!.GetValue(null)!;
        Assert.Equal(["ApplyApplicationDefaults", "Startup", "Load WinFormsApp1", "Shutdown"], events);
    }

    [Fact]
    public void A_console_app_with_My_compiles_without_the_Forms_layer_and_runs()
    {
        var compilation = Compile("ConsoleApp1", "Console", OutputKind.ConsoleApplication, [VisualBasicForms], ("Program.vb", ConsoleProgram));

        AssertNoErrors(compilation);
        var describe = Load(compilation).GetType("ConsoleApp1.Program", throwOnError: true)!.GetMethod("Describe")!;
        Assert.Equal("ConsoleApp1|0", describe.Invoke(null, null));
    }

    private static VisualBasicCompilation CompileWindowsFormsApp() =>
        Compile(
            "WinFormsApp1",
            "WindowsForms",
            OutputKind.WindowsApplication,
            [VisualBasicForms, FormsLayer],
            (Path.Combine("My Project", "Application.Designer.vb"), ApplicationDesigner),
            ("ApplicationEvents.vb", ApplicationEvents),
            ("Form1.vb", Form1),
            ("Form1.Designer.vb", Form1Designer),
            ("AppLog.vb", AppLog));

    /// <summary>
    /// Compiles Visual Basic as TabStudio does for a project: the SDK's imports, Option Strict Off, and the
    /// <c>_MyType</c> and <c>TARGET</c> symbols the My namespace is compiled with.
    /// </summary>
    private static VisualBasicCompilation Compile(string name, string myType, OutputKind kind, IReadOnlyList<MetadataReference> references, params (string Path, string Text)[] sources)
    {
        KeyValuePair<string, object>[] symbols =
        [
            new("_MyType", myType),
            new("CONFIG", "Debug"),
            new("DEBUG", true),
            new("TRACE", true),
            new("PLATFORM", "AnyCPU"),
        ];
        var parseOptions = new VisualBasicParseOptions(
            languageVersion: LanguageVersion.Latest,
            documentationMode: DocumentationMode.Parse,
            kind: SourceCodeKind.Regular,
            preprocessorSymbols: PredefinedPreprocessorSymbols.AddPredefinedPreprocessorSymbols(kind, symbols));
        var imports = myType == "WindowsForms"
            ? VisualBasicCompiler.DefaultImports.Concat(VisualBasicCompiler.WindowsFormsImports)
            : VisualBasicCompiler.DefaultImports;
        var options = new VisualBasicCompilationOptions(
            kind,
            globalImports: GlobalImport.Parse(imports),
            rootNamespace: name,
            optionStrict: OptionStrict.Off,
            optionInfer: true,
            optionExplicit: true,
            parseOptions: parseOptions,
            embedVbCoreRuntime: false);
        var trees = sources.Select(source => VisualBasicSyntaxTree.ParseText(source.Text, parseOptions, source.Path));
        return VisualBasicCompilation.Create(name, trees, [.. ReferenceAssemblies.Net100, .. references], options);
    }

    private static void AssertNoErrors(Microsoft.CodeAnalysis.Compilation compilation) =>
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);

    private static Assembly Load(Microsoft.CodeAnalysis.Compilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(image.ToArray());
    }
}
