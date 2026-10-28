using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;

namespace TabStudio.App.Services.Link;

/// <summary>
/// Decides on this device whether a typed character opens the completion list, as Roslyn's C# or Visual Basic
/// completion does. The editor asks on every keystroke, too often for a round trip to the host; the answer needs
/// only the text.
/// </summary>
internal static class CompletionTriggers
{
    /// <summary>
    /// A workspace with one empty C# document and one empty Visual Basic document, for Roslyn's completion services;
    /// kept for the app's lifetime.
    /// </summary>
    private static readonly Lazy<AdhocWorkspace> Scratch = new(CreateScratchWorkspace);

    /// <summary>Whether typing a character opens the completion list.</summary>
    /// <param name="text">The file's text with the character typed, or <see langword="null"/> when unknown.</param>
    /// <param name="position">The caret, after the character.</param>
    /// <param name="typedCharacter">The character.</param>
    /// <param name="filePath">
    /// The file, whose extension picks the language: Visual Basic for <c>.vb</c>, C# for anything else or
    /// <see langword="null"/>.
    /// </param>
    public static bool ShouldTrigger(string? text, int position, char typedCharacter, string? filePath = null)
    {
        var language = filePath is null ? LanguageNames.CSharp : SyntaxClassifier.LanguageOf(filePath);
        var document = Scratch.Value.CurrentSolution.Projects
            .Where(p => p.Language == language)
            .SelectMany(p => p.Documents)
            .FirstOrDefault();
        if (text is not null && document is not null && CompletionService.GetService(document) is { } service)
        {
            var source = SourceText.From(text);
            return service.ShouldTriggerCompletion(source, Math.Clamp(position, 0, source.Length), CompletionTrigger.CreateInsertionTrigger(typedCharacter));
        }

        return char.IsLetter(typedCharacter) || typedCharacter is '.' or '_';
    }

    private static AdhocWorkspace CreateScratchWorkspace()
    {
        var workspace = new AdhocWorkspace(RoslynWorkspaceHost.CreateHostServices());
        var csharp = workspace.AddProject("CompletionTriggers", LanguageNames.CSharp);
        workspace.AddDocument(csharp.Id, "Triggers.cs", SourceText.From(string.Empty));
        var basic = workspace.AddProject("CompletionTriggersVisualBasic", LanguageNames.VisualBasic);
        workspace.AddDocument(basic.Id, "Triggers.vb", SourceText.From(string.Empty));
        return workspace;
    }
}
