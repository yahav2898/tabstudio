using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualBasic.ApplicationServices;
using TabStudio.Compilation;

namespace TabStudio.VisualBasic.Forms.Tests;

/// <summary>My.Application's assembly information, cultures, environment and command line.</summary>
public sealed class ApplicationBaseTests
{
    [Fact]
    public void AssemblyInfo_reads_the_attributes_of_an_assembly()
    {
        var assembly = CompileLibrary("Inventory", """
            [assembly: System.Reflection.AssemblyTitle("Inventory")]
            [assembly: System.Reflection.AssemblyDescription("Keeps the stock of a shop.")]
            [assembly: System.Reflection.AssemblyCompany("Contoso")]
            [assembly: System.Reflection.AssemblyProduct("Inventory Manager")]
            [assembly: System.Reflection.AssemblyCopyright("Copyright (c) Contoso 2026")]
            [assembly: System.Reflection.AssemblyTrademark("Contoso is a trademark of Contoso")]
            [assembly: System.Reflection.AssemblyVersion("2.5.1.0")]
            """);

        var info = new AssemblyInfo(assembly);

        Assert.Equal("Inventory", info.AssemblyName);
        Assert.Equal("Inventory", info.Title);
        Assert.Equal("Keeps the stock of a shop.", info.Description);
        Assert.Equal("Contoso", info.CompanyName);
        Assert.Equal("Inventory Manager", info.ProductName);
        Assert.Equal("Copyright (c) Contoso 2026", info.Copyright);
        Assert.Equal("Contoso is a trademark of Contoso", info.Trademark);
        Assert.Equal(new Version(2, 5, 1, 0), info.Version);
        Assert.True(Directory.Exists(info.DirectoryPath), info.DirectoryPath);
        Assert.Contains(assembly, info.LoadedAssemblies);
        Assert.True(info.WorkingSet > 0);
        Assert.False(string.IsNullOrEmpty(info.StackTrace));
    }

    [Fact]
    public void AssemblyInfo_gives_empty_strings_for_attributes_an_assembly_does_not_have()
    {
        var info = new AssemblyInfo(CompileLibrary("Bare", "public class Empty { }"));

        Assert.Equal(string.Empty, info.Trademark);
        Assert.Equal(string.Empty, info.Description);
        Assert.Equal(new Version(0, 0, 0, 0), info.Version);
        Assert.Throws<ArgumentNullException>(() => new AssemblyInfo(null!));
    }

    [Fact]
    public void Info_describes_the_program_that_declares_the_My_class()
    {
        var application = new TestConsoleApplication();

        Assert.Equal(typeof(ApplicationBaseTests).Assembly.GetName().Name, application.Info.AssemblyName);
        Assert.Same(application.Info, application.Info);
    }

    [Fact]
    public void CommandLineArgs_are_the_ones_the_framework_was_given()
    {
        var application = new TestConsoleApplication();

        application.SetCommandLine(["/data", "stock.csv"]);

        Assert.Equal(["/data", "stock.csv"], application.CommandLineArgs);
    }

    [Fact]
    public void GetEnvironmentVariable_reads_a_variable_and_throws_for_one_that_is_not_defined()
    {
        var name = $"TABSTUDIO_VB_TEST_{Guid.NewGuid():N}";
        var application = new TestConsoleApplication();
        Environment.SetEnvironmentVariable(name, "stock");
        try
        {
            Assert.Equal("stock", application.GetEnvironmentVariable(name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        var error = Assert.Throws<ArgumentException>(() => application.GetEnvironmentVariable(name));
        Assert.Contains(name, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangeCulture_changes_the_cultures_of_the_current_thread()
    {
        string? culture = null;
        string? uiCulture = null;
        var thread = new Thread(() =>
        {
            var application = new TestConsoleApplication();
            application.ChangeCulture("fr-FR");
            application.ChangeUICulture("he-IL");
            culture = application.Culture.Name;
            uiCulture = application.UICulture.Name;
        });
        thread.Start();
        thread.Join();

        Assert.Equal("fr-FR", culture);
        Assert.Equal("he-IL", uiCulture);
    }

    /// <summary>Compiles a C# library with Roslyn and loads it.</summary>
    private static Assembly CompileLibrary(string name, string source)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            ReferenceAssemblies.Net100,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return Assembly.Load(image.ToArray());
    }

    /// <summary>A console app's My.Application, declared in the program as the My namespace declares it.</summary>
    private sealed class TestConsoleApplication : ConsoleApplicationBase
    {
        public void SetCommandLine(string[] arguments) => InternalCommandLine = new ReadOnlyCollection<string>(arguments);
    }
}
