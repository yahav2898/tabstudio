using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Link.Language;

// The bodies of the language service's messages. Paths are always the tablet's: the host maps them to its mirror
// and back. Results that are plain Compilation records (diagnostics, completion lists, rename edits...) travel as
// they are.

/// <summary>One file of the workspace, by its path relative to the workspace's root, with '/' as separator.</summary>
internal sealed record WorkspaceFileEntry(string Path, long Length, string Hash);

/// <summary>The workspace's files, so the host can tell which it lacks.</summary>
internal sealed record PrepareWorkspaceRequest(string Name, string ClientRoot, string Separator, IReadOnlyList<WorkspaceFileEntry> Files);

/// <summary>The positions, in the prepared file list, of the files the host needs.</summary>
internal sealed record PrepareWorkspaceResponse(IReadOnlyList<int> Missing);

/// <summary>A batch of files the host needs: their positions in the prepared list, in the order of the attachments.</summary>
internal sealed record UploadFilesRequest(IReadOnlyList<int> Files);

/// <summary>Loads the mirrored workspace, from its solution or folder, with the tablet's settings.</summary>
internal sealed record LoadWorkspaceRequest(string EntryPath, bool Optimize, IReadOnlyList<string> ActiveProjects);

/// <summary>The C# documents of the loaded workspace.</summary>
internal sealed record LoadWorkspaceResponse(IReadOnlyList<string> Documents);

/// <summary>A file's new text.</summary>
internal sealed record TextChange(string Path, string Text);

/// <summary>A request about a file.</summary>
internal sealed record FileRequest(string Path);

/// <summary>A request about a project.</summary>
internal sealed record ProjectRequest(string ProjectPath);

/// <summary>A request about a position in a file.</summary>
internal sealed record PositionRequest(string Path, int Position);

/// <summary>A completion list request.</summary>
internal sealed record CompletionListRequest(string Path, int Position, CompletionTrigger Trigger);

/// <summary>A completion commit request; the commit character as a one-character string, or null.</summary>
internal sealed record CompletionChangeRequest(string Path, CompletionEntry Entry, string? CommitCharacter);

/// <summary>A completion description request.</summary>
internal sealed record CompletionDescriptionRequest(CompletionEntry Entry);

/// <summary>A rename request.</summary>
internal sealed record RenameRequest(string Path, int Position, string NewName, RenameOptions? Options);

/// <summary>A code actions request.</summary>
internal sealed record CodeActionsRequest(string Path, int Start, int Length);

/// <summary>A code action edits request.</summary>
internal sealed record CodeActionEditsRequest(string Id);

/// <summary>Debug or Release.</summary>
internal sealed record OptimizationSetting(bool Optimize);

/// <summary>The projects being edited.</summary>
internal sealed record ActiveProjectsSetting(IReadOnlyList<string> ProjectPaths);

/// <summary>A result that may be absent, such as Quick Info where there is no symbol.</summary>
internal sealed record Optional<T>(T? Value)
    where T : class;

/// <summary>A compiled assembly: its image and PDB are the response's attachments at these positions.</summary>
internal sealed record AssemblyReference(string Name, int Image, int? Symbols);

/// <summary>A build result without its bytes, which are attachments.</summary>
internal sealed record BuildResponse(
    string ProjectName,
    IReadOnlyList<BuildDiagnostic> Diagnostics,
    AssemblyReference? EntryAssembly,
    IReadOnlyList<AssemblyReference> Dependencies,
    TimeSpan Duration);

/// <summary>A debug build: the build and the debugger's map of its statements and methods.</summary>
internal sealed record DebugBuildResponse(BuildResponse Result, IReadOnlyList<DebugStatement> Statements, IReadOnlyList<DebugMethod> Methods);
