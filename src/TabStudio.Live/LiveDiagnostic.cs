namespace TabStudio.Live;

/// <summary>An Error List row of the host's, on the workspace's relative paths, as every participant sees it.</summary>
/// <param name="Id">The code, such as <c>CS0103</c>.</param>
/// <param name="Severity">0 hidden, 1 message, 2 warning, 3 error, as <c>DiagnosticSeverityLevel</c> numbers them.</param>
/// <param name="Message">The description.</param>
/// <param name="Path">The file, relative to the workspace's folder, or <see langword="null"/> for the whole project.</param>
/// <param name="Start">The zero-based offset of its span.</param>
/// <param name="Length">The length of its span.</param>
/// <param name="Line">The one-based line.</param>
/// <param name="Column">The one-based column.</param>
/// <param name="Project">The project it belongs to, when known.</param>
public sealed record LiveDiagnostic(string Id, int Severity, string Message, string? Path, int Start, int Length, int Line, int Column, string? Project);
