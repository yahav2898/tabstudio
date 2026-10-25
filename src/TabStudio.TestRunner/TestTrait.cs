namespace TabStudio.TestRunner;

/// <summary>
/// A name and value attached to a test, used to group and filter tests in Test Explorer.
/// </summary>
/// <remarks>
/// xUnit's <c>[Trait("Name", "Value")]</c> maps directly. NUnit's <c>[Category("x")]</c> and MSTest's
/// <c>[TestCategory("x")]</c> become a trait named <c>Category</c>; MSTest's <c>[TestProperty]</c>,
/// <c>[Owner]</c> and <c>[Priority]</c> become traits of the same names.
/// </remarks>
/// <param name="Name">The trait name, for example <c>Category</c>.</param>
/// <param name="Value">The trait value, for example <c>Integration</c>.</param>
public sealed record TestTrait(string Name, string Value);
