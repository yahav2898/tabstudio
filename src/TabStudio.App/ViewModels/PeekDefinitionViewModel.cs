using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.Compilation;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>One definition shown by Peek Definition: its file's text and where the definition is in it.</summary>
/// <param name="Location">Where the definition is.</param>
/// <param name="Text">The file's text, with unsaved edits when it is open.</param>
/// <param name="Classifications">Roslyn's colors for the text.</param>
public sealed record PeekedDefinition(SourceLocation Location, string Text, IReadOnlyList<ClassifiedRange> Classifications)
{
    /// <summary>The file name, such as <c>Greeter.cs</c>.</summary>
    public string FileName => Path.GetFileName(Location.FilePath);
}

/// <summary>
/// Peek Definition (Alt+F12): the definition of the symbol at the caret, shown in a small editor under the line
/// without leaving the file. Several definitions, such as the parts of a partial class, are stepped through.
/// </summary>
public sealed partial class PeekDefinitionViewModel : ObservableObject
{
    /// <summary>Creates the peek.</summary>
    /// <param name="symbolDisplay">The symbol, as the header names it.</param>
    /// <param name="definitions">The definitions, at least one.</param>
    public PeekDefinitionViewModel(string symbolDisplay, IReadOnlyList<PeekedDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(symbolDisplay);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentOutOfRangeException.ThrowIfZero(definitions.Count);
        SymbolDisplay = symbolDisplay;
        Definitions = definitions;
    }

    /// <summary>The symbol.</summary>
    public string SymbolDisplay { get; }

    /// <summary>Every definition.</summary>
    public IReadOnlyList<PeekedDefinition> Definitions { get; }

    /// <summary>The definition shown.</summary>
    public PeekedDefinition Current => Definitions[Index];

    /// <summary>Which definition is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Current), nameof(Position), nameof(Title))]
    public partial int Index { get; set; }

    /// <summary>Whether there is more than one definition to step through.</summary>
    public bool HasSeveral => Definitions.Count > 1;

    /// <summary>"2 of 3", when there are several.</summary>
    public string Position => string.Create(CultureInfo.CurrentCulture, $"{Index + 1} of {Definitions.Count}");

    /// <summary>The header: the file and the line.</summary>
    public string Title => string.Create(CultureInfo.CurrentCulture, $"{Current.FileName}, line {Current.Location.Line}");

    /// <summary>Shows the next definition, after the last the first.</summary>
    public void Next() => Index = (Index + 1) % Definitions.Count;

    /// <summary>Shows the previous definition, before the first the last.</summary>
    public void Previous() => Index = (Index + Definitions.Count - 1) % Definitions.Count;
}
