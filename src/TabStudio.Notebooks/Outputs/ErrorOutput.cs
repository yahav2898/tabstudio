namespace TabStudio.Notebooks.Outputs;

/// <summary>
/// An error a cell ran into: a compile error or an exception. This is a Jupyter <c>error</c> output.
/// </summary>
/// <param name="Name">The kind of error, such as <c>System.InvalidOperationException</c> or <c>CompilationError</c>.</param>
/// <param name="Message">The error message.</param>
/// <param name="Traceback">The lines to show, such as the exception and its stack trace.</param>
public sealed record ErrorOutput(string Name, string Message, IReadOnlyList<string> Traceback) : NotebookOutput;
