namespace TabStudio.App.ViewModels;

/// <summary>
/// A value of a program paused by a debug adapter, for the Locals and Watch panels and DataTips: what the adapter
/// shows of it, its children, read the first time they are shown, and a way to change it.
/// </summary>
public interface IDebugVariable
{
    /// <summary>The value, as the adapter formats it.</summary>
    string Value { get; }

    /// <summary>The type, or empty when the adapter does not say.</summary>
    string Type { get; }

    /// <summary>Whether it has members or items to show.</summary>
    bool HasChildren { get; }

    /// <summary>An expression that finds it again, such as <c>items[0]</c>, for Add Watch; <see langword="null"/> when unknown.</summary>
    string? EvaluateName { get; }

    /// <summary>Whether <see cref="SetValueAsync"/> can change it.</summary>
    bool CanSetValue { get; }

    /// <summary>Reads its members or items from the adapter. Called off the UI thread; it waits for the adapter.</summary>
    /// <returns>The children with their names, in the adapter's order.</returns>
    IReadOnlyList<(string Name, IDebugVariable Value)> ReadChildren();

    /// <summary>Changes the value in the paused program.</summary>
    /// <param name="expression">The new value, as an expression in the program's language.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The value as the adapter now shows it.</returns>
    Task<string> SetValueAsync(string expression, CancellationToken cancellationToken);
}
