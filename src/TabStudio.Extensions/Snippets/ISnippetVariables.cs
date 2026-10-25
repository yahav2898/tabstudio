namespace TabStudio.Extensions.Snippets;

/// <summary>Resolves the variables a snippet uses, such as <c>$TM_FILENAME</c>.</summary>
public interface ISnippetVariables
{
    /// <summary>Resolves a variable.</summary>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">Its value, or <see langword="null"/> when it is known but has none now, such as an empty selection.</param>
    /// <returns>Whether the variable is known. An unknown variable becomes a placeholder with its name, as in VS Code.</returns>
    bool TryResolve(string name, out string? value);
}
