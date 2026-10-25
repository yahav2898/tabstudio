namespace TabStudio.Link.Language;

/// <summary>The methods of the language service over TabStudio Link. Notifications are marked as such.</summary>
internal static class LinkLanguageMethods
{
    /// <summary>Sends the workspace's file list; answered with the files the host lacks.</summary>
    public const string PrepareWorkspace = "workspace/prepare";

    /// <summary>Sends a batch of the files the host lacks, as attachments.</summary>
    public const string UploadFiles = "workspace/upload";

    /// <summary>Loads the mirrored workspace into the host's language service; answered with its C# documents.</summary>
    public const string LoadWorkspace = "workspace/load";

    /// <summary>Notification: a file's text changed in the editor.</summary>
    public const string UpdateText = "document/update";

    /// <summary>Notification: a new C# file was added.</summary>
    public const string AddDocument = "document/add";

    /// <summary>Notification: Debug or Release.</summary>
    public const string SetOptimization = "settings/optimization";

    /// <summary>Notification: the projects being edited.</summary>
    public const string SetActiveProjects = "settings/activeProjects";

    /// <summary>Errors and warnings of a file.</summary>
    public const string DocumentDiagnostics = "diagnostics/document";

    /// <summary>Errors and warnings of a project.</summary>
    public const string ProjectDiagnostics = "diagnostics/project";

    /// <summary>Semantic classification of a file.</summary>
    public const string Classifications = "document/classify";

    /// <summary>The edits that format a file.</summary>
    public const string Format = "document/format";

    /// <summary>Builds a project; the assemblies come back as attachments.</summary>
    public const string Build = "build/project";

    /// <summary>Builds a test project for Live Unit Testing, the projects it tests instrumented for the tablet's coverage recorder.</summary>
    public const string BuildForCoverage = "build/coverage";

    /// <summary>Builds a project for the debugger; the assemblies come back as attachments.</summary>
    public const string BuildForDebugging = "build/debug";

    /// <summary>Restores a project's packages on the host.</summary>
    public const string RestorePackages = "packages/restore";

    /// <summary>Gives the projects .NET's own source generators.</summary>
    public const string AddFrameworkGenerators = "generators/framework";

    /// <summary>The completion list.</summary>
    public const string Completions = "completion/list";

    /// <summary>The edits that commit a completion item.</summary>
    public const string CompletionChange = "completion/commit";

    /// <summary>A completion item's description.</summary>
    public const string CompletionDescription = "completion/describe";

    /// <summary>Quick Info.</summary>
    public const string QuickInfo = "hover/quickInfo";

    /// <summary>Signature help.</summary>
    public const string SignatureHelp = "hover/signatureHelp";

    /// <summary>Go to Definition.</summary>
    public const string Definition = "navigate/definition";

    /// <summary>Go to Implementation.</summary>
    public const string Implementations = "navigate/implementations";

    /// <summary>Find All References.</summary>
    public const string References = "navigate/references";

    /// <summary>The occurrences of a symbol in its file.</summary>
    public const string Highlights = "navigate/highlights";

    /// <summary>Whether a symbol can be renamed.</summary>
    public const string CanRename = "rename/check";

    /// <summary>The edits of a rename.</summary>
    public const string RenameEdits = "rename/edits";

    /// <summary>Quick fixes and refactorings.</summary>
    public const string CodeActions = "codeActions/list";

    /// <summary>What a code action changes.</summary>
    public const string CodeActionEdits = "codeActions/edits";

    /// <summary>The declarations of a file.</summary>
    public const string DocumentSymbols = "symbols/document";

    /// <summary>The declarations around a position.</summary>
    public const string SymbolPath = "symbols/path";

    /// <summary>Warms IntelliSense up.</summary>
    public const string WarmUp = "workspace/warmUp";

    /// <summary>The errors of the workspace with changes not applied yet, for the agent.</summary>
    public const string SnapshotDiagnostics = "snapshot/diagnostics";

    /// <summary>Builds the test projects of the workspace with changes not applied yet, for the agent.</summary>
    public const string SnapshotTestBuilds = "snapshot/buildTests";
}
