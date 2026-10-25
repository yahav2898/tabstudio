using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// Compiles a recorded UI test project's C# as the .NET SDK would, against the app the swarm ran and the same Avalonia,
/// xUnit and headless packages, and fails on any error.
/// </summary>
internal static class RecordedTestCompilation
{
    /// <summary>The global usings of a test project with implicit usings and <c>&lt;Using Include="Xunit" /&gt;</c>.</summary>
    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        global using Xunit;
        """;

    public static void AssertCompiles(string projectFolder, SwarmBuild app)
    {
        List<SyntaxTree> trees = [CSharpSyntaxTree.ParseText(GlobalUsings, path: "GlobalUsings.g.cs")];
        foreach (var file in Directory.EnumerateFiles(projectFolder, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetRelativePath(projectFolder, file).Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj")
            {
                continue;
            }

            trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Latest), file));
        }

        // One reference per assembly name: the app's build first, then what this process loaded, then the framework.
        Dictionary<string, MetadataReference> references = new(StringComparer.OrdinalIgnoreCase);
        void Add(string name, Func<MetadataReference> reference)
        {
            if (!references.ContainsKey(name))
            {
                references[name] = reference();
            }
        }

        Add(app.Entry.Name, () => MetadataReference.CreateFromImage(app.Entry.Image));
        foreach (var dependency in app.Dependencies)
        {
            Add(dependency.Name, () => MetadataReference.CreateFromImage(dependency.Image));
        }

        var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.Location.Length > 0).Select(a => a.Location);
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        foreach (var path in app.PackageAssemblies.Concat(loaded).Concat(platform).Where(File.Exists))
        {
            Add(Path.GetFileNameWithoutExtension(path), () => MetadataReference.CreateFromFile(path));
        }

        var compilation = CSharpCompilation.Create(
            "HelloAvalonia.UITests",
            trees,
            references.Values,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();
        Assert.True(result.Success, string.Join('\n', errors));
    }
}
