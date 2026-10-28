using TabStudio.Compilation.Features;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests.Features;

/// <summary>
/// A small workspace on disk for IntelliSense tests, with C# and Visual Basic projects. Sources mark the caret with
/// <c>$$</c>; the marker is removed before the file is written, and its offset is kept for <see cref="Caret"/>.
/// </summary>
internal sealed class FeatureWorkspace : IDisposable
{
    public const string CaretMarker = "$$";

    private readonly TempDirectory _temp = new();
    private readonly Dictionary<string, int> _carets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);

    public RoslynWorkspaceHost Host { get; } = new();

    public LanguageFeatures Features { get; private set; } = null!;

    /// <summary>One console project, <c>App</c>, with one file, <c>App/Program.cs</c>.</summary>
    public static FeatureWorkspace WithProgram(string source, bool implicitUsings = true) =>
        new FeatureWorkspace()
            .Project("App", executable: true, implicitUsings)
            .File("App/Program.cs", source)
            .Load();

    /// <summary>One Visual Basic console project, <c>App</c>, with one file, <c>App/Program.vb</c>.</summary>
    public static FeatureWorkspace WithModule(string source) =>
        new FeatureWorkspace()
            .VisualBasicProject("App", executable: true)
            .File("App/Program.vb", source)
            .Load();

    /// <summary>Adds a project in a folder of its name.</summary>
    /// <param name="name">The project's name and folder.</param>
    /// <param name="executable">Whether it is a console program.</param>
    /// <param name="implicitUsings">Whether it has implicit global usings.</param>
    /// <param name="references">
    /// The projects it references: a name for a C# project, or a project file name such as <c>Shapes.vbproj</c>.
    /// </param>
    public FeatureWorkspace Project(string name, bool executable = false, bool implicitUsings = true, params string[] references)
    {
        var outputType = executable ? "<OutputType>Exe</OutputType>" : string.Empty;
        var usings = implicitUsings ? "enable" : "disable";
        var projectReferences = ProjectReferences(references, ".csproj");
        _temp.Write($"{name}/{name}.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {outputType}
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>{usings}</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>{projectReferences}</ItemGroup>
            </Project>
            """);
        return this;
    }

    /// <summary>
    /// Adds a Visual Basic project in a folder of its name, as <c>dotnet new console -lang VB</c> makes it, with the
    /// project's name as its <c>RootNamespace</c>.
    /// </summary>
    /// <param name="name">The project's name and folder.</param>
    /// <param name="executable">Whether it is a console program.</param>
    /// <param name="properties">More properties, as XML, such as <c>&lt;OptionStrict&gt;On&lt;/OptionStrict&gt;</c>.</param>
    /// <param name="references">
    /// The projects it references: a name for a Visual Basic project, or a project file name such as <c>Lib.csproj</c>.
    /// </param>
    public FeatureWorkspace VisualBasicProject(string name, bool executable = false, string properties = "", params string[] references)
    {
        var outputType = executable ? "<OutputType>Exe</OutputType>" : string.Empty;
        var projectReferences = ProjectReferences(references, ".vbproj");
        _temp.Write($"{name}/{name}.vbproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {outputType}
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>{name}</RootNamespace>
                {properties}
              </PropertyGroup>
              <ItemGroup>{projectReferences}</ItemGroup>
            </Project>
            """);
        return this;
    }

    private static string ProjectReferences(string[] references, string defaultExtension) =>
        string.Concat(references.Select(r =>
        {
            var fileName = r.EndsWith("proj", StringComparison.OrdinalIgnoreCase) ? r : r + defaultExtension;
            var folder = Path.GetFileNameWithoutExtension(fileName);
            return $"<ProjectReference Include=\"..\\{folder}\\{fileName}\" />";
        }));

    /// <summary>Adds a source file. A <c>$$</c> in it marks the caret.</summary>
    public FeatureWorkspace File(string relativePath, string source)
    {
        var caret = source.IndexOf(CaretMarker, StringComparison.Ordinal);
        var text = caret < 0 ? source : source.Remove(caret, CaretMarker.Length);
        if (caret >= 0)
        {
            _carets[relativePath] = caret;
        }

        _texts[relativePath] = text;
        _temp.Write(relativePath, text);
        return this;
    }

    public FeatureWorkspace Load()
    {
        Host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
        Features = new LanguageFeatures(Host);
        return this;
    }

    public string PathOf(string relativePath) => _temp.Resolve(relativePath);

    public string TextOf(string relativePath) => _texts[relativePath];

    public int Caret(string relativePath = "App/Program.cs") => _carets[relativePath];

    /// <summary>Replaces a file's text in the host, as the editor does while the user types.</summary>
    public void Type(string relativePath, string source)
    {
        File(relativePath, source);
        Assert.True(Host.UpdateText(PathOf(relativePath), _texts[relativePath]));
    }

    /// <summary>Applies edits, given in document order, to a text.</summary>
    public static string Apply(string text, IEnumerable<TextEdit> edits)
    {
        foreach (var edit in edits.OrderByDescending(e => e.Start))
        {
            text = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText);
        }

        return text;
    }

    /// <summary>Applies the edits of one file and returns its new text.</summary>
    public string ApplyTo(string relativePath, IEnumerable<DocumentEdit> edits)
    {
        var path = PathOf(relativePath);
        var edit = Assert.Single(edits, e => e.FilePath == path);
        return Apply(TextOf(relativePath), edit.Edits);
    }

    public void Dispose()
    {
        Host.Dispose();
        _temp.Dispose();
    }
}
