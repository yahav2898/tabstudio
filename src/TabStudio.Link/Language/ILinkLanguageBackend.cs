using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;

namespace TabStudio.Link.Language;

/// <summary>
/// The language service a host runs for a tablet, over the host's mirror of the tablet's workspace: the same Roslyn
/// language service the IDE runs on its own. Paths are the host's; <see cref="LinkLanguageServer"/> maps them.
/// </summary>
/// <remarks>Every method may be called from any thread.</remarks>
public interface ILinkLanguageBackend : IDisposable
{
    /// <summary>Loads a workspace from the mirror.</summary>
    /// <param name="workspace">The workspace, loaded from the mirror's files.</param>
    /// <param name="pathMap">The compiler's path map from the mirror to the tablet's paths, for PDBs and stack traces.</param>
    void Load(Workspace workspace, IReadOnlyList<KeyValuePair<string, string>> pathMap);

    /// <summary>Whether a file is a C# document of a loaded project.</summary>
    /// <param name="filePath">Full path of the file.</param>
    bool ContainsDocument(string filePath);

    /// <summary>Replaces a file's text.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="text">The text.</param>
    void UpdateText(string filePath, string text);

    /// <summary>Adds a C# file to the project whose folder contains it.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="text">Its text.</param>
    bool AddDocument(string filePath, string text);

    /// <summary>Switches between Debug and Release.</summary>
    /// <param name="optimize">Whether to optimize: Release.</param>
    void SetOptimization(bool optimize);

    /// <summary>Tells which projects are being edited.</summary>
    /// <param name="projectFilePaths">The project files.</param>
    void SetActiveProjects(IEnumerable<string> projectFilePaths);

    /// <summary>Errors and warnings in one file.</summary>
    Task<IReadOnlyList<BuildDiagnostic>> GetDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Errors and warnings in a whole project.</summary>
    Task<IReadOnlyList<BuildDiagnostic>> GetProjectDiagnosticsAsync(string projectFilePath, CancellationToken cancellationToken);

    /// <summary>Semantic classification of a whole file.</summary>
    Task<IReadOnlyList<ClassifiedRange>> GetClassificationsAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>The edits that format a file.</summary>
    Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Builds a project and the projects it references.</summary>
    Task<BuildResult> BuildAsync(ProjectInfo project, CancellationToken cancellationToken);

    /// <summary>Builds a project and the projects it references for the debugger.</summary>
    Task<DebugBuild> BuildForDebuggingAsync(ProjectInfo project, CancellationToken cancellationToken);

    /// <summary>Restores a project's NuGet packages for compiling.</summary>
    Task<LinkRestoreOutcome> RestorePackagesAsync(ProjectInfo project, CancellationToken cancellationToken);

    /// <summary>Gives every project .NET's own source generators.</summary>
    Task<IReadOnlyList<BuildDiagnostic>> AddFrameworkGeneratorsAsync(CancellationToken cancellationToken);

    /// <summary>The completion list at a position.</summary>
    Task<CompletionResult> GetCompletionsAsync(string filePath, int position, CompletionTrigger trigger, CancellationToken cancellationToken);

    /// <summary>The edits that commit a completion item.</summary>
    Task<CompletionCommit> GetCompletionChangeAsync(string filePath, CompletionEntry entry, char? commitCharacter, CancellationToken cancellationToken);

    /// <summary>The description of a completion item.</summary>
    Task<IReadOnlyList<TaggedRun>> GetCompletionDescriptionAsync(CompletionEntry entry, CancellationToken cancellationToken);

    /// <summary>Quick Info at a position.</summary>
    Task<QuickInfoResult?> GetQuickInfoAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>The overloads of the call at a position.</summary>
    Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>Where the symbol at a position is defined.</summary>
    Task<DefinitionResult?> FindDefinitionAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>The implementations of the symbol at a position.</summary>
    Task<IReadOnlyList<DefinitionResult>> FindImplementationsAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>Every reference to the symbol at a position.</summary>
    Task<IReadOnlyList<ReferenceItem>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>The occurrences of the symbol at a position in its file.</summary>
    Task<IReadOnlyList<DocumentHighlight>> GetDocumentHighlightsAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>Whether the symbol at a position can be renamed.</summary>
    Task<RenameTarget> CanRenameAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>The edits that rename the symbol at a position.</summary>
    Task<RenameResult> GetRenameEditsAsync(string filePath, int position, string newName, RenameOptions? options, CancellationToken cancellationToken);

    /// <summary>The quick fixes and refactorings for a selection.</summary>
    Task<IReadOnlyList<CodeActionItem>> GetCodeActionsAsync(string filePath, int start, int length, CancellationToken cancellationToken);

    /// <summary>What a code action changes.</summary>
    Task<IReadOnlyList<DocumentEdit>> GetCodeActionEditsAsync(string id, CancellationToken cancellationToken);

    /// <summary>The declarations of a file.</summary>
    Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>The declarations that contain a position.</summary>
    Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken);

    /// <summary>Prepares IntelliSense in the background.</summary>
    Task WarmUpAsync(string filePath, CancellationToken cancellationToken);
}
