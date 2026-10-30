using Microsoft.CodeAnalysis;
using TabStudio.TestRunner.Naming;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Source;

/// <summary>Finds the test classes and methods declared in one C# or Visual Basic syntax tree.</summary>
/// <param name="tree">The tree.</param>
/// <param name="model">Its semantic model, or <see langword="null"/> to match attributes by their written names.</param>
/// <param name="rootNamespace">
/// Without a semantic model, the Visual Basic project's root namespace, which the compiler puts in front of every
/// namespace; empty for C#.
/// </param>
/// <param name="cancellationToken">Cancels the search.</param>
internal sealed class SourceTestFinder(SyntaxTree tree, SemanticModel? model, string rootNamespace, CancellationToken cancellationToken)
{
    private readonly SourceLanguage _language = SourceLanguage.Of(tree);

    /// <summary>A method that may be a test, with where it comes from.</summary>
    private sealed record Candidate(SourceMethodDeclaration Declaration, IMethodSymbol? Symbol, bool IsInherited);

    /// <summary>Finds every test class in the tree.</summary>
    public List<SourceTestClass> Find()
    {
        var classes = new List<SourceTestClass>();
        foreach (var declaration in _language.Types(tree.GetRoot(cancellationToken), rootNamespace))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FindClass(declaration) is { } found)
            {
                classes.Add(found);
            }
        }

        return classes;
    }

    private SourceTestClass? FindClass(SourceTypeDeclaration declaration)
    {
        if ((declaration.IsAbstract && !declaration.IsStatic) || declaration.IsGeneric)
        {
            // Abstract classes run through the classes derived from them; generic ones are not supported.
            return null;
        }

        var symbol = model?.GetDeclaredSymbol(declaration.Node, cancellationToken) as INamedTypeSymbol;
        var fullName = symbol is null ? declaration.FullName : SymbolNames.MetadataFullName(symbol);
        var classAttributes = symbol is null
            ? SourceAttribute.FromSyntax(declaration.Attributes, _language)
            : ClassAttributes(symbol);

        var methods = new List<SourceTestMethod>();
        foreach (var candidate in Candidates(declaration, symbol))
        {
            if (FindMethod(candidate, fullName, classAttributes) is { } method)
            {
                methods.Add(method);
            }
        }

        if (methods.Count == 0)
        {
            return null;
        }

        var framework = methods[0].Framework;
        var (namespaceName, name) = SymbolNames.Split(fullName);
        var identifier = declaration.Identifier;
        return new SourceTestClass
        {
            FullName = fullName,
            Name = name,
            NamespaceName = namespaceName,
            Framework = framework,
            Location = LocationOf(identifier),
            SpanStart = identifier.SpanStart,
            SpanLength = identifier.Span.Length,
            SkipReason = framework switch
            {
                TestFramework.NUnit => IgnoreReason(classAttributes, FrameworkNames.NUnitIgnore),
                TestFramework.MSTest => IgnoreReason(classAttributes, FrameworkNames.MSTestIgnore),
                _ => null,
            },
            Methods = methods,
        };
    }

    /// <summary>The class's attributes and, because NUnit's are inherited, its base classes'.</summary>
    private List<SourceAttribute> ClassAttributes(INamedTypeSymbol symbol)
    {
        var attributes = new List<SourceAttribute>();
        for (var current = symbol; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            attributes.AddRange(SourceAttribute.FromSymbol(current.GetAttributes(), _language, cancellationToken));
        }

        return attributes;
    }

    /// <summary>
    /// The class's own methods and, with a semantic model, the ones it inherits from base classes in source.
    /// Inherited methods are listed once, for the first part of a partial class.
    /// </summary>
    private List<Candidate> Candidates(SourceTypeDeclaration declaration, INamedTypeSymbol? symbol)
    {
        var candidates = declaration.Methods
            .Select(m => new Candidate(m, model?.GetDeclaredSymbol(m.Node, cancellationToken) as IMethodSymbol, IsInherited: false))
            .ToList();

        if (symbol is null || !IsFirstPart(declaration, symbol))
        {
            return candidates;
        }

        var declaredNames = symbol.GetMembers().OfType<IMethodSymbol>().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        for (var baseType = symbol.BaseType; baseType is not null && baseType.SpecialType != SpecialType.System_Object; baseType = baseType.BaseType)
        {
            foreach (var method in baseType.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.MethodKind != MethodKind.Ordinary ||
                    method.DeclaredAccessibility == Accessibility.Private ||
                    !declaredNames.Add(method.Name) ||
                    method.DeclaringSyntaxReferences is not [var reference, ..] ||
                    _language.MethodOf(reference.GetSyntax(cancellationToken)) is not { } syntax)
                {
                    continue;
                }

                candidates.Add(new Candidate(syntax, method, IsInherited: true));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Whether a declaration is the first part of its class. The node a class symbol's syntax reference gives is the
    /// one <see cref="SourceTypeDeclaration.Node"/> holds: the declaration in C#, the <c>Class</c> statement in Visual
    /// Basic, whose reference spans the whole block.
    /// </summary>
    private bool IsFirstPart(SourceTypeDeclaration declaration, INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences is [var first, ..] &&
        first.SyntaxTree == declaration.Node.SyntaxTree &&
        first.GetSyntax(cancellationToken).Span == declaration.Node.Span;

    private SourceTestMethod? FindMethod(Candidate candidate, string typeFullName, List<SourceAttribute> classAttributes)
    {
        var attributes = candidate.Symbol is { } symbol
            ? SourceAttribute.FromSymbol(symbol.GetAttributes(), _language, cancellationToken)
            : SourceAttribute.FromSyntax(candidate.Declaration.Attributes, _language);

        var parameterNames = candidate.Symbol is { } methodSymbol
            ? methodSymbol.Parameters.Select(p => p.Name).ToList()
            : candidate.Declaration.ParameterNames.ToList();

        var method = new MethodFacts(candidate, typeFullName, parameterNames, attributes, classAttributes);
        if (attributes.Find(a => a.Is(FrameworkNames.XunitFact, "Fact", "Theory")) is { } fact)
        {
            return Xunit(method, fact);
        }

        if (attributes.Exists(a => a.Is(FrameworkNames.NUnitTest, "Test") || a.Is(FrameworkNames.NUnitTestCase, "TestCase") || a.Is(FrameworkNames.NUnitTestCaseSource, "TestCaseSource")))
        {
            return NUnit(method);
        }

        // MSTest runs instance methods only, as the runner does.
        if (attributes.Find(a => a.Is(FrameworkNames.MSTestTestMethod, "TestMethod", "DataTestMethod")) is { } testMethod &&
            classAttributes.Exists(a => a.Is(FrameworkNames.MSTestTestClass, "TestClass")) &&
            !(candidate.Symbol?.IsStatic ?? candidate.Declaration.IsStatic))
        {
            return MSTest(method, testMethod);
        }

        return null;
    }

    /// <summary>What every framework needs to know about a candidate method.</summary>
    private sealed record MethodFacts(
        Candidate Candidate,
        string TypeFullName,
        List<string> ParameterNames,
        List<SourceAttribute> Attributes,
        List<SourceAttribute> ClassAttributes)
    {
        public string Name => Candidate.Declaration.Identifier.ValueText;

        public string FullyQualifiedName => TestNaming.FullyQualifiedName(TypeFullName, Name);
    }

    private static SourceTestMethod Xunit(MethodFacts method, SourceAttribute fact)
    {
        var displayName = fact.Named("DisplayName") as string;
        var rows = method.Attributes
            .Where(a => a.Is(FrameworkNames.XunitInlineData, "InlineData"))
            .Select(a => (Attribute: a, Values: a.PositionalValues(), Skip: a.Named("Skip") as string))
            .ToList();
        var hasDynamicData = method.Attributes.Exists(a =>
            !a.Is(FrameworkNames.XunitInlineData, "InlineData") &&
            a.Is(FrameworkNames.XunitData, "MemberData", "ClassData"));

        return Create(method, TestFramework.XUnit, displayName, fact.Named("Skip") as string, isExplicit: false,
            traits: [.. XunitTraits(method.ClassAttributes), .. XunitTraits(method.Attributes)],
            rows: rows.Select(r => (r.Attribute, r.Values, (string?)null, r.Skip)).ToList(),
            hasDynamicData);
    }

    private static SourceTestMethod NUnit(MethodFacts method)
    {
        var rows = method.Attributes
            .Where(a => a.Is(FrameworkNames.NUnitTestCase, "TestCase"))
            .Select(a => (Attribute: a, Values: a.PositionalValues(), Display: a.Named("TestName") as string, Skip: a.Named("Ignore") as string ?? a.Named("IgnoreReason") as string))
            .ToList();
        var skip = IgnoreReason(method.Attributes, FrameworkNames.NUnitIgnore) ?? IgnoreReason(method.ClassAttributes, FrameworkNames.NUnitIgnore);
        var isExplicit = method.Attributes.Exists(a => a.Is(FrameworkNames.NUnitExplicit, "Explicit")) ||
            method.ClassAttributes.Exists(a => a.Is(FrameworkNames.NUnitExplicit, "Explicit"));
        var traits = method.ClassAttributes.Concat(method.Attributes)
            .Where(a => a.Is(FrameworkNames.NUnitCategory, "Category"))
            .Select(a => a.FirstString())
            .OfType<string>()
            .Select(c => new TestTrait("Category", c))
            .ToList();

        return Create(method, TestFramework.NUnit, null, skip, isExplicit, traits,
            rows.Select(r => (r.Attribute, r.Values, r.Display, r.Skip)).ToList(),
            method.Attributes.Exists(a => a.Is(FrameworkNames.NUnitTestCaseSource, "TestCaseSource")));
    }

    private static SourceTestMethod MSTest(MethodFacts method, SourceAttribute testMethod)
    {
        var rows = method.Attributes
            .Where(a => a.Is(FrameworkNames.MSTestDataRow, "DataRow"))
            .Select(a => (Attribute: a, Values: a.PositionalValues(), Display: a.Named("DisplayName") as string, Skip: a.Named("IgnoreMessage") as string))
            .ToList();
        var hasDynamicData = method.Attributes.Exists(a =>
            !a.Is(FrameworkNames.MSTestDataRow, "DataRow") &&
            (a.Implements(FrameworkNames.MSTestDataSource) || a.HasShortName("DynamicData")));
        var skip = IgnoreReason(method.Attributes, FrameworkNames.MSTestIgnore) ?? IgnoreReason(method.ClassAttributes, FrameworkNames.MSTestIgnore);
        var traits = method.ClassAttributes.Concat(method.Attributes)
            .Where(a => a.Is(FrameworkNames.MSTestTestCategory, "TestCategory"))
            .Select(a => a.FirstString())
            .OfType<string>()
            .Select(c => new TestTrait("Category", c))
            .ToList();

        return Create(method, TestFramework.MSTest, testMethod.Named("DisplayName") as string ?? testMethod.FirstString(), skip, isExplicit: false, traits,
            rows.Select(r => (r.Attribute, r.Values, r.Display, r.Skip)).ToList(),
            hasDynamicData);
    }

    private static SourceTestMethod Create(
        MethodFacts method,
        TestFramework framework,
        string? displayName,
        string? skipReason,
        bool isExplicit,
        List<TestTrait> traits,
        List<(SourceAttribute Attribute, List<object?> Values, string? DisplayName, string? SkipReason)> rows,
        bool hasDynamicData)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var baseName = string.IsNullOrWhiteSpace(displayName) ? method.Name : displayName;
        var dataRows = rows.Select(row =>
        {
            var arguments = TestNaming.Row(row.Values);
            var rowDisplay = string.IsNullOrWhiteSpace(row.DisplayName)
                ? TestNaming.RowDisplayName(framework, baseName, method.ParameterNames, row.Values)
                : row.DisplayName;
            return new SourceDataRow(
                TestNaming.Unique(method.FullyQualifiedName + arguments, seen),
                rowDisplay,
                arguments,
                row.Attribute.Location is { } location ? LocationOf(location) : LocationOf(method.Candidate.Declaration.Identifier),
                string.IsNullOrWhiteSpace(row.SkipReason) ? null : row.SkipReason);
        }).ToList();

        var identifier = method.Candidate.Declaration.Identifier;
        return new SourceTestMethod
        {
            FullyQualifiedName = method.FullyQualifiedName,
            TypeFullName = method.TypeFullName,
            MethodName = method.Name,
            DisplayName = baseName,
            Framework = framework,
            Location = LocationOf(identifier),
            SpanStart = identifier.SpanStart,
            SpanLength = identifier.Span.Length,
            SkipReason = string.IsNullOrWhiteSpace(skipReason) ? null : skipReason,
            IsExplicit = isExplicit,
            Traits = traits,
            IsParameterized = dataRows.Count > 0 || hasDynamicData || method.ParameterNames.Count > 0,
            HasDynamicData = hasDynamicData,
            DataRows = dataRows,
            IsInherited = method.Candidate.IsInherited,
        };
    }

    private static List<TestTrait> XunitTraits(List<SourceAttribute> attributes) =>
        attributes
            .Where(a => a.Is(FrameworkNames.XunitTrait, "Trait"))
            .Select(a => a.PositionalValues())
            .Where(values => values is [string, string])
            .Select(values => new TestTrait((string)values[0]!, (string)values[1]!))
            .ToList();

    private static string? IgnoreReason(List<SourceAttribute> attributes, string fullName)
    {
        var ignore = attributes.Find(a => a.Is(fullName, "Ignore"));
        if (ignore is null)
        {
            return null;
        }

        var reason = ignore.FirstString() ?? ignore.Named("IgnoreMessage") as string;
        return string.IsNullOrWhiteSpace(reason) ? "Ignored" : reason;
    }

    private static TestSourceLocation LocationOf(SyntaxToken token) => LocationOf(token.GetLocation());

    private static TestSourceLocation LocationOf(Location location)
    {
        var span = location.GetLineSpan();
        return new TestSourceLocation(span.Path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
    }
}
