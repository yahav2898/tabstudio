using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.App.Swarm.Recording;

/// <summary>A recorded UI test, read back from its C#, as TabStudio replays it on every screen.</summary>
/// <param name="FilePath">The test's file.</param>
/// <param name="Line">The line of the test method's name, from 1.</param>
/// <param name="TypeFullName">The test class's full name.</param>
/// <param name="MethodName">The test method's name.</param>
/// <param name="Width">The width the test starts the app at, in device-independent pixels.</param>
/// <param name="Height">The height the test starts the app at.</param>
/// <param name="Steps">The steps, in order.</param>
/// <param name="Unsupported">Statements that only run with <c>dotnet test</c>, with their lines, such as code added by hand.</param>
public sealed record UiTestDefinition(
    string FilePath,
    int Line,
    string TypeFullName,
    string MethodName,
    double Width,
    double Height,
    IReadOnlyList<UiTestStep> Steps,
    IReadOnlyList<string> Unsupported)
{
    /// <summary>The method's full name, as the test runner names the test: <c>Namespace.Class.Method</c>.</summary>
    public string MethodFullName => TypeFullName + "." + MethodName;

    /// <summary>The test's name in words: <c>Counting three taps</c> for <c>Counting_three_taps</c>.</summary>
    public string DisplayName => MethodName.Replace('_', ' ');
}

/// <summary>
/// Reads recorded UI tests back from C#: the test methods of the <see cref="UiTestWriter.Category"/> category and the
/// <c>RecordedApp</c> steps they take, so the Test Explorer replays them on every screen. A test someone changed by hand
/// still reads, as long as its steps are calls on <c>RecordedApp</c>.
/// </summary>
public static class UiTestReader
{
    /// <summary>Reads the recorded tests of a C# file.</summary>
    /// <param name="filePath">The file, for the definitions.</param>
    /// <param name="text">Its C#.</param>
    public static IReadOnlyList<UiTestDefinition> Read(string filePath, string text)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains(UiTestWriter.Category, StringComparison.Ordinal))
        {
            return [];
        }

        var root = CSharpSyntaxTree.ParseText(text, path: filePath).GetCompilationUnitRoot();
        List<UiTestDefinition> tests = [];
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (method.Parent is ClassDeclarationSyntax type && IsRecorded(method) && Read(filePath, type, method) is { } test)
            {
                tests.Add(test);
            }
        }

        return tests;
    }

    /// <summary>Reads the recorded tests of the C# files of a folder and its subfolders, skipping build output.</summary>
    /// <param name="directory">The test project's folder.</param>
    public static IReadOnlyList<UiTestDefinition> ReadDirectory(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        List<UiTestDefinition> tests = [];
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                tests.AddRange(Read(file, File.ReadAllText(file)));
            }
            catch (IOException)
            {
                // A file being written is read again on the next discovery.
            }
        }

        return tests;
    }

    private static bool IsRecorded(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(l => l.Attributes).Any(a =>
            Name(a.Name) is "Trait" or "TraitAttribute"
            && a.ArgumentList?.Arguments is [{ Expression: var name }, { Expression: var value }]
            && StringOf(name) == "Category"
            && StringOf(value) == UiTestWriter.Category);

    private static UiTestDefinition? Read(string filePath, ClassDeclarationSyntax type, MethodDeclarationSyntax method)
    {
        if (method.Body is not { } body)
        {
            return null;
        }

        double width = 0, height = 0;
        string? app = null;
        List<UiTestStep> steps = [];
        List<string> unsupported = [];
        foreach (var statement in body.Statements)
        {
            if (Start(statement) is { } start)
            {
                (app, width, height) = start;
            }
            else if (app is not null && Step(statement, app) is { } step)
            {
                steps.Add(step);
            }
            else
            {
                var line = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                unsupported.Add(string.Create(CultureInfo.InvariantCulture, $"line {line}: {statement.ToString().Trim()}"));
            }
        }

        if (app is null)
        {
            return null;
        }

        return new UiTestDefinition(
            filePath,
            method.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            FullName(type),
            method.Identifier.ValueText,
            width,
            height,
            steps,
            unsupported);
    }

    /// <summary>The variable and size of <c>using var app = RecordedApp.Start(width: 412, height: 915);</c>.</summary>
    private static (string App, double Width, double Height)? Start(StatementSyntax statement)
    {
        if (statement is not LocalDeclarationStatementSyntax { Declaration.Variables: [var variable] }
            || variable.Initializer?.Value is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } call
            || access.Name.Identifier.ValueText != "Start"
            || Name(access.Expression) != "RecordedApp")
        {
            return null;
        }

        var arguments = call.ArgumentList.Arguments;
        double width = 0, height = 0;
        for (var i = 0; i < arguments.Count; i++)
        {
            var name = arguments[i].NameColon?.Name.Identifier.ValueText ?? (i == 0 ? "width" : "height");
            var value = NumberOf(arguments[i].Expression) ?? 0;
            if (name == "width")
            {
                width = value;
            }
            else
            {
                height = value;
            }
        }

        return (variable.Identifier.ValueText, width, height);
    }

    /// <summary>A step such as <c>app.Tap("CountButton");</c>.</summary>
    private static UiTestStep? Step(StatementSyntax statement, string app)
    {
        if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } call }
            || Name(access.Expression) != app)
        {
            return null;
        }

        var arguments = call.ArgumentList.Arguments.Select(a => a.Expression).ToList();
        string? Text(int index) => index < arguments.Count ? StringOf(arguments[index]) : null;
        double Number(int index) => index < arguments.Count ? NumberOf(arguments[index]) ?? 0 : 0;
        return access.Name.Identifier.ValueText switch
        {
            "Tap" when Text(0) is { } element => new UiTestStep(UiTestStepKind.Tap, element),
            "LongPress" when Text(0) is { } element => new UiTestStep(UiTestStepKind.LongPress, element),
            "Scroll" when Text(0) is { } element => new UiTestStep(UiTestStepKind.Scroll, element, ScrollX: Number(1), ScrollY: Number(2)),
            "Type" when Text(0) is { } text => new UiTestStep(UiTestStepKind.Type, string.Empty, text),
            "Press" when arguments.Count > 0 && EnumMember(arguments[0], "Key") is { } key =>
                new UiTestStep(UiTestStepKind.Press, string.Empty, Key: key, Modifiers: arguments.Count > 1 ? Modifiers(arguments[1]) : null),
            "ShouldShow" when Text(0) is { } element && Text(1) is { } text => UiTestStep.Check(element, text),
            _ => null,
        };
    }

    /// <summary><c>KeyModifiers.Control | KeyModifiers.Shift</c> as <c>Control, Shift</c>.</summary>
    private static string? Modifiers(ExpressionSyntax expression)
    {
        List<string> names = [];
        void Collect(ExpressionSyntax part)
        {
            if (part is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.BitwiseOrExpression))
            {
                Collect(binary.Left);
                Collect(binary.Right);
            }
            else if (EnumMember(part, "KeyModifiers") is { } name && name != "None")
            {
                names.Add(name);
            }
        }

        Collect(expression);
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static string? EnumMember(ExpressionSyntax expression, string enumName) =>
        expression is MemberAccessExpressionSyntax access && Name(access.Expression) == enumName ? access.Name.Identifier.ValueText : null;

    private static string? StringOf(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) ? literal.Token.ValueText : null;

    private static double? NumberOf(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression) => Convert.ToDouble(literal.Token.Value, CultureInfo.InvariantCulture),
        PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.UnaryMinusExpression) => -NumberOf(prefix.Operand),
        _ => null,
    };

    /// <summary>The last part of a name, such as <c>RecordedApp</c> for <c>Tests.RecordedApp</c>.</summary>
    private static string? Name(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };

    private static string Name(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => name.ToString(),
    };

    /// <summary>A class's full name, with <c>+</c> between nested classes, as the test runner writes it.</summary>
    private static string FullName(ClassDeclarationSyntax type)
    {
        var name = type.Identifier.ValueText;
        SyntaxNode? parent = type.Parent;
        while (parent is ClassDeclarationSyntax outer)
        {
            name = outer.Identifier.ValueText + "+" + name;
            parent = outer.Parent;
        }

        List<string> namespaces = [];
        for (; parent is not null; parent = parent.Parent)
        {
            if (parent is BaseNamespaceDeclarationSyntax ns)
            {
                namespaces.Insert(0, ns.Name.ToString());
            }
        }

        return namespaces.Count == 0 ? name : string.Join('.', namespaces) + "." + name;
    }
}
