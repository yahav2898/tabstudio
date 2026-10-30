using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using TabStudio.Core.Workspaces;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation.VisualBasic;

/// <summary>
/// Compiles Visual Basic as the .NET SDK does: its default imports and options (<c>Option Explicit On</c>,
/// <c>Option Strict Off</c>, <c>Option Infer On</c>, <c>Option Compare Binary</c>), the project's
/// <c>&lt;Import&gt;</c> items, <c>RootNamespace</c>, <c>MyType</c> and <c>DefineConstants</c>, and the
/// <c>Microsoft.VisualBasic</c> runtime of .NET itself.
/// </summary>
/// <remarks>
/// <para>
/// The <c>My</c> namespace comes from the compiler: Roslyn embeds Visual Basic's My template and compiles it with the
/// project's <c>_MyType</c>, which is <c>Empty</c> unless the project says otherwise, as the SDK defines it for .NET.
/// <c>Console</c> and <c>WindowsForms</c> give <c>My.Application</c>, <c>My.Computer</c>, <c>My.User</c> and
/// <c>My.Forms</c>, whose base classes TabStudio provides in its own <c>Microsoft.VisualBasic.Forms</c>.
/// </para>
/// <para>
/// The template is parsed with the compilation options' own parse options, so they always carry the project's
/// preprocessor symbols as well as each document's.
/// </para>
/// </remarks>
public static class VisualBasicCompiler
{
    /// <summary>The namespaces the SDK imports into every Visual Basic project for .NET (<c>Microsoft.NET.Sdk.VisualBasic.targets</c>).</summary>
    public static IReadOnlyList<string> DefaultImports { get; } =
    [
        "Microsoft.VisualBasic",
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Diagnostics",
        "System.Linq",
        "System.Xml.Linq",
        "System.Threading.Tasks",
    ];

    /// <summary>What the Windows Desktop SDK adds for a Visual Basic project with <c>UseWindowsForms</c>.</summary>
    public static IReadOnlyList<string> WindowsFormsImports { get; } = ["System.Drawing", "System.Windows.Forms"];

    /// <summary>
    /// The warnings the SDK's default <c>NoWarn</c> turns off in a Visual Basic project, and BC40000-range ones that a
    /// reference to an older framework assembly gives, as CS1701 does in C#.
    /// </summary>
    private static readonly string[] SdkNoWarn =
        ["BC41999", "BC42016", "BC42017", "BC42018", "BC42019", "BC42020", "BC42021", "BC42022", "BC42032", "BC42036"];

    /// <summary>The target framework's symbols, as the SDK defines them for .NET 10.</summary>
    private static readonly string[] FrameworkSymbols =
    [
        "NET", "NETCOREAPP", "NET10_0", "NET10_0_OR_GREATER", "NET9_0_OR_GREATER", "NET8_0_OR_GREATER",
        "NET7_0_OR_GREATER", "NET6_0_OR_GREATER", "NET5_0_OR_GREATER", "NETCOREAPP3_1_OR_GREATER", "NETCOREAPP3_0_OR_GREATER",
        "NETCOREAPP2_2_OR_GREATER", "NETCOREAPP2_1_OR_GREATER", "NETCOREAPP2_0_OR_GREATER", "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP1_0_OR_GREATER",
        "TABSTUDIO",
    ];

    /// <summary>The file extension of Visual Basic sources.</summary>
    public const string SourceExtension = ".vb";

    /// <summary>Whether a file is a Visual Basic source file.</summary>
    /// <param name="path">The file.</param>
    public static bool IsSource(string path) => path.EndsWith(SourceExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The project's <c>MyType</c>: what the My namespace offers. <c>Empty</c> unless the project sets it, as the SDK
    /// does for .NET (<c>Microsoft.NET.Sdk.VisualBasic.targets</c>).
    /// </summary>
    /// <param name="project">The project, or <see langword="null"/>.</param>
    public static string MyTypeOf(TsProject? project) =>
        project?.GetProperty("MyType")?.Trim() is { Length: > 0 } myType && !myType.Contains("$(", StringComparison.Ordinal) ? myType : "Empty";

    /// <summary>
    /// The preprocessor symbols a project compiles with: <c>CONFIG</c>, <c>DEBUG</c> or the configuration's own,
    /// <c>TRACE</c>, <c>PLATFORM</c>, <c>_MyType</c>, the framework's, then the project's <c>DefineConstants</c>, which
    /// win.
    /// </summary>
    /// <param name="project">The project, or <see langword="null"/> for a console program.</param>
    /// <param name="optimize">Whether this is a Release build.</param>
    public static IReadOnlyList<KeyValuePair<string, object>> PreprocessorSymbols(TsProject? project, bool optimize)
    {
        var symbols = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["CONFIG"] = optimize ? "Release" : "Debug",
            ["TRACE"] = true,
            ["PLATFORM"] = "AnyCPU",
            ["_MyType"] = MyTypeOf(project),
            // As vbc defines them: the My template writes Sub Main only for a "winexe" TARGET.
            ["TARGET"] = (project?.GetProperty("OutputType") ?? "Library").ToLowerInvariant() switch
            {
                "exe" => "exe",
                "winexe" => "winexe",
                _ => "library",
            },
            ["VBC_VER"] = 16.9,
        };
        symbols[optimize ? "RELEASE" : "DEBUG"] = true;
        foreach (var symbol in FrameworkSymbols)
        {
            symbols[symbol] = true;
        }

        if (project?.GetProperty("DefineConstants") is { Length: > 0 } defines)
        {
            foreach (var (name, value) in ParseDefineConstants(defines))
            {
                symbols[name] = value;
            }
        }

        if (project?.GetProperty("DefineDebug") is { } defineDebug && string.Equals(defineDebug.Trim(), "false", StringComparison.OrdinalIgnoreCase))
        {
            symbols.Remove("DEBUG");
        }

        if (project?.GetProperty("DefineTrace") is { } defineTrace && string.Equals(defineTrace.Trim(), "false", StringComparison.OrdinalIgnoreCase))
        {
            symbols.Remove("TRACE");
        }

        return [.. symbols];
    }

    /// <summary>
    /// Reads Visual Basic's <c>DefineConstants</c>: <c>A=1,B="text",C</c> or <c>A;B</c>. A name without a value is
    /// <c>True</c>; quoted values are strings, numbers are integers and <c>True</c> and <c>False</c> are booleans.
    /// </summary>
    /// <param name="defines">The property's value.</param>
    public static IEnumerable<KeyValuePair<string, object>> ParseDefineConstants(string defines)
    {
        ArgumentNullException.ThrowIfNull(defines);
        foreach (var part in SplitDefines(defines))
        {
            var text = part.Trim();
            if (text.Length == 0 || text.Contains("$(", StringComparison.Ordinal))
            {
                continue;
            }

            var equals = text.IndexOf('=', StringComparison.Ordinal);
            var name = (equals < 0 ? text : text[..equals]).Trim();
            if (name.Length == 0 || !SyntaxFacts.IsValidIdentifier(name))
            {
                continue;
            }

            object value = true;
            if (equals >= 0)
            {
                var raw = text[(equals + 1)..].Trim();
                if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
                {
                    value = raw[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
                }
                else if (bool.TryParse(raw, out var boolean))
                {
                    value = boolean;
                }
                else if (int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                {
                    value = number;
                }
                else
                {
                    value = raw;
                }
            }

            yield return new(name, value);
        }
    }

    /// <summary>Splits defines on commas and semicolons outside quotes.</summary>
    private static IEnumerable<string> SplitDefines(string defines)
    {
        var start = 0;
        var quoted = false;
        for (var i = 0; i < defines.Length; i++)
        {
            var c = defines[i];
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && c is ',' or ';')
            {
                yield return defines[start..i];
                start = i + 1;
            }
        }

        yield return defines[start..];
    }

    /// <summary>Parse options for a project, with its preprocessor symbols and the latest Visual Basic.</summary>
    /// <param name="project">The project, or <see langword="null"/>.</param>
    /// <param name="optimize">Whether this is a Release build.</param>
    public static VisualBasicParseOptions CreateParseOptions(TsProject? project = null, bool optimize = false) => new(
        languageVersion: LanguageVersion.Latest,
        documentationMode: DocumentationMode.Parse,
        kind: SourceCodeKind.Regular,
        preprocessorSymbols: PreprocessorSymbols(project, optimize));

    /// <summary>
    /// The namespaces imported into every file: the SDK's defaults (unless <c>DisableImplicitNamespaceImports</c>),
    /// those of Windows Forms when the project uses it, and its <c>&lt;Import&gt;</c> items, without those it removes.
    /// </summary>
    /// <param name="project">The project, or <see langword="null"/> for the SDK's defaults.</param>
    public static IReadOnlyList<string> ImportsOf(TsProject? project)
    {
        var implicitImports = project is null || !IsTrue(project.GetProperty("DisableImplicitNamespaceImports"))
            ? DefaultImports.Concat(project is not null && WindowsForms.WindowsFormsProject.UsesWindowsForms(project) ? WindowsFormsImports : [])
            : [];
        return [.. implicitImports.Concat(project?.Imports ?? [])
            .Where(name => !(project?.RemovedImports ?? []).Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The compiler's <c>OutputKind</c> for a project: a console program, a Windows program (<c>WinExe</c>) or a library.</summary>
    /// <param name="project">The project.</param>
    /// <param name="kind">Whether TabStudio runs it as a program or loads it as a library.</param>
    public static OutputKind OutputKindOf(TsProject? project, AssemblyKind kind) => kind switch
    {
        AssemblyKind.Library => OutputKind.DynamicallyLinkedLibrary,
        _ when string.Equals(project?.OutputType, "WinExe", StringComparison.OrdinalIgnoreCase) => OutputKind.WindowsApplication,
        _ => OutputKind.ConsoleApplication,
    };

    /// <summary>
    /// The project's startup object, or <see langword="null"/> to let the compiler find <c>Sub Main</c>, which is
    /// what <c>Sub Main</c> means in a project file.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string? MainTypeOf(TsProject? project) =>
        project?.GetProperty("StartupObject")?.Trim() is { Length: > 0 } startup
        && !string.Equals(startup, "Sub Main", StringComparison.OrdinalIgnoreCase)
        && !startup.Contains("$(", StringComparison.Ordinal)
            ? startup
            : null;

    /// <summary>Compilation options for a project, with its options, imports and root namespace.</summary>
    /// <param name="project">The project, or <see langword="null"/> for the SDK's defaults.</param>
    /// <param name="kind">Whether to produce a program or a library.</param>
    /// <param name="optimize">Whether to optimize.</param>
    /// <param name="concurrentBuild">Whether Roslyn may compile on several threads.</param>
    public static VisualBasicCompilationOptions CreateCompilationOptions(
        TsProject? project,
        AssemblyKind kind,
        bool optimize = false,
        bool concurrentBuild = true)
    {
        var parseOptions = CreateParseOptions(project, optimize);
        var noWarn = SdkNoWarn.Concat(NoWarnOf(project)).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(id => id, _ => ReportDiagnostic.Suppress, StringComparer.OrdinalIgnoreCase);
        return new VisualBasicCompilationOptions(
            outputKind: OutputKindOf(project, kind),
            mainTypeName: kind == AssemblyKind.Library ? null : MainTypeOf(project),
            globalImports: GlobalImport.Parse(ImportsOf(project)),
            rootNamespace: RootNamespaceOf(project),
            optionStrict: OptionValue(project, "OptionStrict", "Off") switch
            {
                "on" => OptionStrict.On,
                "custom" => OptionStrict.Custom,
                _ => OptionStrict.Off,
            },
            optionInfer: OptionValue(project, "OptionInfer", "On") != "off",
            optionExplicit: OptionValue(project, "OptionExplicit", "On") != "off",
            optionCompareText: OptionValue(project, "OptionCompare", "Binary") == "text",
            parseOptions: parseOptions,
            embedVbCoreRuntime: false,
            optimizationLevel: optimize ? OptimizationLevel.Release : OptimizationLevel.Debug,
            checkOverflow: !IsTrue(project?.GetProperty("RemoveIntegerChecks")),
            specificDiagnosticOptions: noWarn,
            concurrentBuild: concurrentBuild,
            deterministic: true);
    }

    /// <summary>The project's <c>RootNamespace</c>; its assembly name or project name when it sets none, as the SDK defaults it.</summary>
    /// <param name="project">The project.</param>
    public static string RootNamespaceOf(TsProject? project)
    {
        if (project is null)
        {
            return string.Empty;
        }

        var value = project.GetProperty("RootNamespace") ?? project.GetProperty("AssemblyName") ?? project.Name;
        return value.Contains("$(", StringComparison.Ordinal) ? project.Name.Replace(' ', '_').Replace('-', '_') : value.Trim();
    }

    /// <summary>The project's own <c>NoWarn</c>, as Visual Basic writes it: numbers, with or without <c>BC</c>.</summary>
    private static IEnumerable<string> NoWarnOf(TsProject? project) =>
        (project?.GetProperty("NoWarn") ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => !id.Contains("$(", StringComparison.Ordinal))
            .Select(id => id.All(char.IsAsciiDigit) ? "BC" + id : id);

    private static string OptionValue(TsProject? project, string name, string fallback) =>
        (project?.GetProperty(name)?.Trim() is { Length: > 0 } value && !value.Contains("$(", StringComparison.Ordinal) ? value : fallback)
            .ToLowerInvariant();

    private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The source of the assembly attributes the SDK generates into <c>AssemblyInfo.vb</c>, or <see langword="null"/>
    /// when the project has none.
    /// </summary>
    /// <param name="project">The project.</param>
    public static string? CreateAssemblyAttributesSource(TsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var version = RoslynWorkspaceHost.AssemblyVersionOf(project);
        if (project.AssemblyAttributes.Count == 0 && version is null)
        {
            return null;
        }

        var builder = new StringBuilder("' <auto-generated/>\nOption Strict Off\nOption Explicit On\n\n");
        if (version is not null)
        {
            builder.Append("<Assembly: Global.System.Reflection.AssemblyVersionAttribute(").Append(Literal(version)).Append(")>\n");
        }

        foreach (var attribute in project.AssemblyAttributes)
        {
            var name = attribute.TypeName.Contains('.', StringComparison.Ordinal) ? "Global." + attribute.TypeName : attribute.TypeName;
            var arguments = attribute.Arguments.Select(a =>
                (a.Name is null ? string.Empty : a.Name + ":=") + (a.IsLiteral ? a.Value : Literal(a.Value)));
            builder.Append("<Assembly: ").Append(name).Append('(').AppendJoin(", ", arguments).Append(")>\n");
        }

        return builder.ToString();
    }

    /// <summary>A Visual Basic string literal.</summary>
    /// <param name="value">The text.</param>
    public static string Literal(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>Builds the Roslyn compilation of Visual Basic documents for a request without emitting it.</summary>
    /// <param name="request">What to compile; its documents are Visual Basic.</param>
    /// <param name="references">The framework references, with the Visual Basic runtime.</param>
    /// <param name="cancellationToken">Cancels parsing.</param>
    public static VisualBasicCompilation CreateCompilation(CompilationRequest request, IReadOnlyList<MetadataReference>? references = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = CreateCompilationOptions(null, request.Kind, request.Optimize);
        var parseOptions = options.ParseOptions;
        var trees = request.Documents
            .Select(d => VisualBasicSyntaxTree.ParseText(SourceText.From(d.Text, Encoding.UTF8), parseOptions, d.FilePath, cancellationToken))
            .ToList();
        return VisualBasicCompilation.Create(
            request.AssemblyName,
            trees,
            [.. references ?? ReferenceAssemblies.Net100, .. request.AdditionalReferences],
            options);
    }

    /// <summary>Compiles Visual Basic documents into an assembly in memory.</summary>
    /// <param name="request">What to compile.</param>
    /// <param name="cancellationToken">Cancels compilation.</param>
    public static CompilationResult Compile(CompilationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var compilation = CreateCompilation(request, cancellationToken: cancellationToken);
        return CSharpCompiler.Emit(compilation, request.Kind == AssemblyKind.ConsoleApplication, stopwatch, null, resources: null, cancellationToken);
    }
}
