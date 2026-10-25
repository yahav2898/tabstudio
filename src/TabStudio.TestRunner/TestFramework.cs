namespace TabStudio.TestRunner;

/// <summary>A unit test framework whose tests TabStudio can find and run.</summary>
/// <remarks>
/// Frameworks are recognized by the names of their attributes, so any version that keeps those names works and
/// none of the frameworks' own runners are needed.
/// </remarks>
public enum TestFramework
{
    /// <summary>xUnit.net v2: <c>[Fact]</c> and <c>[Theory]</c> methods.</summary>
    XUnit,

    /// <summary>NUnit 3 and 4: <c>[Test]</c>, <c>[TestCase]</c> and <c>[TestCaseSource]</c> methods.</summary>
    NUnit,

    /// <summary>MSTest: <c>[TestMethod]</c> and <c>[DataTestMethod]</c> methods in a <c>[TestClass]</c>.</summary>
    MSTest,
}
