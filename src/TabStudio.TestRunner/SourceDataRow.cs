namespace TabStudio.TestRunner;

/// <summary>
/// One data row written in source, such as <c>[InlineData(1, 2)]</c>, <c>[TestCase(1, 2)]</c> or <c>[DataRow(1, 2)]</c>.
/// </summary>
/// <remarks>
/// <see cref="Id"/> is computed the same way as <see cref="TestCase.Id"/>, so after a build the row's result can
/// be found with it and an editor marker on the attribute can run just that row.
/// </remarks>
/// <param name="Id">The id the runner gives this row's test.</param>
/// <param name="DisplayName">The row's display name in the style of its framework, for example <c>Adds(a: 1, b: 2)</c>.</param>
/// <param name="Arguments">The row's arguments as written, for example <c>(1, 2)</c>.</param>
/// <param name="Location">Where the attribute is, for a marker beside it.</param>
/// <param name="SkipReason">Why the row is skipped, or <see langword="null"/>.</param>
public sealed record SourceDataRow(string Id, string DisplayName, string Arguments, TestSourceLocation Location, string? SkipReason);
