using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation;

namespace TabStudio.App.Services.Link;

/// <summary>
/// Decides on this device whether a typed character opens the completion list, as Roslyn's C# completion does.
/// The editor asks on every keystroke, too often for a round trip to the host; the answer needs only the text.
/// </summary>
internal static class CompletionTriggers
{
    /// <summary>A workspace with one empty C# document, for Roslyn's completion service; kept for the app's lifetime.</summary>
    private static readonly Lazy<AdhocWorkspace> Scratch = new(CreateScratchWorkspace);

    /// <summary>Whether typing a character opens the completion list.</summary>
    /// <param name="text">The file's text with the character typed, or <see langword="null"/> when unknown.</param>
    /// <param name="position">The caret, after the character.</param>
    /// <param name="typedCharacter">The character.</param>
    public static bool ShouldTrigger(string? text, int position, char typedCharacter)
    {
        var document = Scratch.Value.CurrentSolution.Projects.SelectMany(p => p.Documents).FirstOrDefault();
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
        var project = workspace.AddProject("CompletionTriggers", LanguageNames.CSharp);
        workspace.AddDocument(project.Id, "Triggers.cs", SourceText.From(string.Empty));
        return workspace;
    }
}
