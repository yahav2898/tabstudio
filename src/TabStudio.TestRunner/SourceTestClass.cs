namespace TabStudio.TestRunner;

/// <summary>A test class found in source, with its test methods.</summary>
public sealed record SourceTestClass
{
    /// <summary>The full name, with <c>+</c> between nested types, the same as <see cref="TestCase.TypeFullName"/>.</summary>
    public required string FullName { get; init; }

    /// <summary>The class name without its namespace.</summary>
    public required string Name { get; init; }

    /// <summary>The namespace, or an empty string for the global namespace.</summary>
    public required string NamespaceName { get; init; }

    /// <summary>The framework of the class's tests.</summary>
    public required TestFramework Framework { get; init; }

    /// <summary>Where the class's name is, for a "Run tests in class" marker.</summary>
    public required TestSourceLocation Location { get; init; }

    /// <summary>The zero-based offset of the class's name in its file.</summary>
    public int SpanStart { get; init; }

    /// <summary>The length of the class's name.</summary>
    public int SpanLength { get; init; }

    /// <summary>Why every test of the class is skipped, or <see langword="null"/>.</summary>
    public string? SkipReason { get; init; }

    /// <summary>The test methods, in declaration order; inherited ones last.</summary>
    public required IReadOnlyList<SourceTestMethod> Methods { get; init; }
}
