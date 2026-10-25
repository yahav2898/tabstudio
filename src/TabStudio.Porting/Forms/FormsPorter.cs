using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.CSharp;
using TabStudio.Porting.Output;
using TabStudio.Porting.Wpf;
using TabStudio.Porting.Xaml;

namespace TabStudio.Porting.Forms;

/// <summary>Ports Windows Forms designer layouts, event wiring, user code and application startup.</summary>
internal static class FormsPorter
{
    /// <summary>Converts the project's forms and code.</summary>
    public static List<PortedFile> Port(PortContext context)
    {
        var files = new List<PortedFile>();
        // Designer partial declarations usually omit the base list. Read it from the user's other partial file
        // before deciding whether the XAML root is a form or a reusable UserControl.
        var bases = context.Source.Files.Where(f => f.Key.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => CSharpSyntaxTree.ParseText(f.Value).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(c => c.BaseList?.Types.FirstOrDefault() is not null)
            .Select(c => (Name: c.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault() is { } ns
                ? ns.Name + "." + c.Identifier.ValueText : c.Identifier.ValueText,
                Base: DesignerReader.SimpleName(c.BaseList!.Types[0].Type)))
            .GroupBy(c => c.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Base, StringComparer.Ordinal);
        var designs = context.Source.Files.Where(f => f.Key.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => (f.Key, Design: DesignerReader.Read(f.Value)))
            .Select(f => (f.Key, Design: f.Design is { } design && bases.TryGetValue(design.ClassName, out var baseType)
                ? design with { BaseType = baseType } : f.Design))
            .Where(f => f.Design is not null).ToList();
        var types = context.Source.Files.Where(f => f.Key.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => CSharpSyntaxTree.ParseText(f.Value).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Select(c => c.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
        var classes = new Dictionary<string, FormsClassInfo>(StringComparer.Ordinal);
        foreach (var (path, design) in designs)
        {
            var target = path[..^".Designer.cs".Length] + ".axaml";
            var report = context.Report(target);
            var builder = new FormsXamlBuilder(design!, context, report, types);
            var document = builder.Build();
            classes[design!.ClassName] = builder.Info;
            var text = XamlWriter.Write(document, new XamlFormat(4, SpaceBeforeSlash: true), out var lines);
            for (var i = 0; i < report.Notes.Count; i++)
            {
                var note = report.Notes[i];
                report.Notes[i] = note with { Line = note.XamlAnchor is { } anchor && lines.TryGetValue(anchor, out var line) ? line : 1, XamlAnchor = null };
            }

            files.Add(new PortedFile(target, text, null, path, PortedFileKind.Xaml, report.Conversions()));
            files.Add(Events(context, design, builder.Info, target[..^6] + ".Events.cs"));
        }

        foreach (var (path, text) in context.Source.Files.Where(f => f.Key.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            if (designs.Any(d => d.Key == path))
            {
                continue;
            }

            if (Path.GetFileName(path) == "Program.cs")
            {
                continue;
            }

            var file = new CodeFile(text, context.Report(path));
            file.Root = (CompilationUnitSyntax)new FormsCodeRewriter(context, file, classes, types).Visit(file.Root);
            file.Root = AsyncCalls.MakeAsync(file, file.Root);
            file.Root = UnsupportedCode.CommentOut(file, file.Root, n => n is IdentifierNameSyntax id
                && (id.Identifier.ValueText is "Graphics" or "PaintEventArgs" or "OpenFileDialog" or "SaveFileDialog" or "Screen" or "ApplicationConfiguration"
                    || IsUnportedComponent(id, classes))
                ? new Unported(id.ToString(), "This Windows API or placeholder component needs a cross-platform implementation") : null);
            file.Root = Usings.Update(file.Root, ns => ns.StartsWith("System.Windows.Forms", StringComparison.Ordinal) || ns == "System.Drawing",
                new[] { "System", "System.Linq", "Avalonia.Controls", "Avalonia.Input", context.SupportNamespace }.Concat(classes.Values.SelectMany(c => c.Usings)), file.Newline);
            files.Add(new PortedFile(path, file.Finish(), null, path, PortedFileKind.Code, file.Report.Conversions()));
        }

        files.AddRange(Application(context, designs.Select(d => d.Design!).ToList()));
        return files;
    }

    private static bool IsUnportedComponent(IdentifierNameSyntax identifier, Dictionary<string, FormsClassInfo> classes)
    {
        var type = identifier.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var ns = type?.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        var name = ns is null ? type?.Identifier.ValueText : ns + "." + type?.Identifier.ValueText;
        return name is not null && classes.TryGetValue(name, out var info) && info.UnportedComponents.Contains(identifier.Identifier.ValueText);
    }

    private static PortedFile Events(PortContext context, FormsDesign design, FormsClassInfo info, string path)
    {
        var report = context.Report(path);
        var text = new StringBuilder();
        text.AppendLine("// Generated by Port to Avalonia: designer event and component wiring.");
        text.AppendLine("using System;");
        foreach (var ns in info.Usings.Append(context.SupportNamespace).Order(StringComparer.Ordinal))
        {
            text.Append("using ").Append(ns).AppendLine(";");
        }

        if (design.Namespace.Length > 0)
        {
            text.Append("namespace ").Append(design.Namespace).AppendLine(";");
        }

        text.Append("partial class ").Append(design.ClassName[(design.ClassName.LastIndexOf('.') + 1)..]).AppendLine("\n{");
        foreach (var field in info.Fields)
        {
            text.Append("    ").AppendLine(field);
        }

        text.AppendLine("    private void InitializePortedEvents()\n    {");
        foreach (var statement in info.Statements)
        {
            text.Append("        ").AppendLine(statement);
        }

        foreach (var (what, why, original) in info.Todos)
        {
            report.Note(text.ToString().Count(c => c == '\n') + 1, what, why);
            text.Append("        // TODO(port): ").Append(what.ReplaceLineEndings(" ")).Append(". ").AppendLine(why);
            if (original is not null)
            {
                text.Append("        // ").AppendLine(original.ReplaceLineEndings(" "));
            }
        }

        text.AppendLine("    }\n}");
        return new PortedFile(path, text.ToString(), null, null, PortedFileKind.Code, report.Conversions());
    }

    private static IEnumerable<PortedFile> Application(PortContext context, List<FormsDesign> designs)
    {
        var program = context.Source.Files.FirstOrDefault(f => Path.GetFileName(f.Key) == "Program.cs").Value;
        var run = program is null ? null : CSharpSyntaxTree.ParseText(program).GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(i => i.Expression.ToString().EndsWith("Application.Run", StringComparison.Ordinal));
        var main = run?.ArgumentList.Arguments.FirstOrDefault()?.Expression as ObjectCreationExpressionSyntax;
        var type = main?.Type.ToString() ?? designs.FirstOrDefault(d => d.BaseType == "Form")?.ClassName;
        var report = context.Report("App.axaml.cs");
        if (main?.ArgumentList?.Arguments.Count > 0 || type is null)
        {
            report.Note(1, "Application startup", "Select and construct the main form in OnFrameworkInitializationCompleted");
        }

        if (program is not null)
        {
            var root = CSharpSyntaxTree.ParseText(program).GetRoot();
            foreach (var statement in root.DescendantNodes().OfType<StatementSyntax>().Where(s => s is not BlockSyntax && s.Parent is BlockSyntax))
            {
                if (statement.ToString().Contains("Application.Run", StringComparison.Ordinal) || statement.ToString().StartsWith("ApplicationConfiguration.Initialize", StringComparison.Ordinal)
                    || statement.ToString().StartsWith("Application.EnableVisualStyles", StringComparison.Ordinal) || statement.ToString().StartsWith("Application.SetCompatibleTextRenderingDefault", StringComparison.Ordinal))
                {
                    continue;
                }

                report.Note(1, "Custom Program.cs startup: " + statement, "Move this initialization into the Avalonia application; Windows Forms startup was replaced");
            }
        }

        context.Needs(SupportNeeds.Form);
        var xaml = new XDocument(new XElement(XamlNamespaces.Avalonia + "Application",
            new XAttribute("xmlns", XamlNamespaces.Avalonia.NamespaceName), new XAttribute(XNamespace.Xmlns + "x", XamlNamespaces.Xaml.NamespaceName),
            new XAttribute(XamlNamespaces.Xaml + "Class", context.RootNamespace + ".App"), new XAttribute("RequestedThemeVariant", "Light")));
        WpfXamlConverter.FinishApplication(xaml, context);
        yield return new PortedFile("App.axaml", XamlWriter.Write(xaml, new XamlFormat(4, SpaceBeforeSlash: true), out _), null, null, PortedFileKind.Xaml, []);
        yield return new PortedFile("App.axaml.cs", $$"""
            using Avalonia;
            using Avalonia.Markup.Xaml;
            using {{context.SupportNamespace}};

            namespace {{context.RootNamespace}};

            public partial class App : Application
            {
                public override void Initialize() => AvaloniaXamlLoader.Load(this);

                public override void OnFrameworkInitializationCompleted()
                {
                    {{(type is null ? "// TODO(port): Select the main form." : "WindowView.ShowMain(ApplicationLifetime, new " + type + "());")}}
                    base.OnFrameworkInitializationCompleted();
                }
            }

            """, null, null, PortedFileKind.Code, []);
    }
}
