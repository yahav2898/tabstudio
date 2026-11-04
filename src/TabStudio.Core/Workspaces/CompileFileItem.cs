namespace TabStudio.Core.Workspaces;

/// <summary>
/// A source file that a target writes before the compiler runs and adds to a project's <c>Compile</c> items (see
/// <see cref="TargetItems.CompileFiles"/>). TabStudio does not write it: it keeps the text the target would write in memory.
/// </summary>
/// <param name="FullPath">The file's full path, where the build writes it.</param>
/// <param name="Text">The text the target writes into it.</param>
public sealed record CompileFileItem(string FullPath, string Text);
