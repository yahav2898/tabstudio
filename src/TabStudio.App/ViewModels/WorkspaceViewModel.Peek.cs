using CommunityToolkit.Mvvm.Input;
using TabStudio.Compilation;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>Peek Definition: the definition of the symbol at the caret, shown inside the editor.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>Peek Definition (Alt+F12): shows the definition under the caret line, or says where a compiled one is.</summary>
    [RelayCommand]
    private async Task PeekDefinitionAsync()
    {
        if (CaretContext() is not { } context || _language is not { } language)
        {
            return;
        }

        var (document, features, offset) = context;
        try
        {
            var definition = await features.FindDefinitionAsync(document.FilePath, offset, BackgroundToken);
            if (definition is null)
            {
                StatusText = "Cannot peek at the symbol under the caret.";
                return;
            }

            if (definition.IsInMetadata)
            {
                StatusText = definition.AssemblyName is { } assembly
                    ? $"{definition.SymbolDisplay} is defined in {assembly}"
                    : $"{definition.SymbolDisplay} is defined in a compiled assembly";
                return;
            }

            var peeked = new List<PeekedDefinition>();
            foreach (var location in definition.Locations)
            {
                var text = Documents.FirstOrDefault(d => string.Equals(d.FilePath, location.FilePath, StringComparison.Ordinal))?.Document.Text
                    ?? await File.ReadAllTextAsync(location.FilePath, BackgroundToken);
                IReadOnlyList<ClassifiedRange> colors = language.ContainsDocument(location.FilePath)
                    ? await language.GetClassificationsAsync(location.FilePath, BackgroundToken)
                    : [];
                peeked.Add(new PeekedDefinition(location, text, colors));
            }

            document.ShowPeek(new PeekDefinitionViewModel(definition.SymbolDisplay, peeked));
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (IOException ex)
        {
            StatusText = $"Cannot peek at the definition: {ex.Message}";
        }
    }

    /// <summary>Opens a peeked definition as a tab, at its place, as double-clicking the peek's title does.</summary>
    /// <param name="definition">The definition.</param>
    public Task OpenPeekedDefinitionAsync(PeekedDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return NavigateToAsync(definition.Location.FilePath, definition.Location.Line, definition.Location.Column);
    }
}
