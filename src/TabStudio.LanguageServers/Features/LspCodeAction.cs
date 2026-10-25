using System.Text.Json;

namespace TabStudio.LanguageServers.Features;

/// <summary>A quick fix or refactoring the server offers for a range.</summary>
/// <remarks>
/// An action can carry its edit, a command for the server to run, or both; servers may also leave the edit out
/// until the action is resolved. The document's <c>ApplyCodeActionAsync</c> takes care of all three.
/// </remarks>
public sealed class LspCodeAction
{
    internal LspCodeAction(JsonElement raw)
    {
        Raw = raw;
    }

    /// <summary>The menu text.</summary>
    public required string Title { get; init; }

    /// <summary>The kind, such as <c>quickfix</c>, <c>refactor.extract</c> or <c>source.organizeImports</c>.</summary>
    public string? Kind { get; init; }

    /// <summary>Whether it fixes a problem, rather than changing working code.</summary>
    public bool IsQuickFix => Kind is null ? DiagnosticCodes.Count > 0 : Kind.StartsWith("quickfix", StringComparison.Ordinal);

    /// <summary>Whether the server recommends it, such as the obvious fix for an error.</summary>
    public bool IsPreferred { get; init; }

    /// <summary>The codes of the diagnostics it fixes.</summary>
    public IReadOnlyList<string> DiagnosticCodes { get; init; } = [];

    /// <summary>Why it cannot be applied now, when the server says so.</summary>
    public string? DisabledReason { get; init; }

    /// <summary>The edit, when the server sent it with the action.</summary>
    public LspWorkspaceEdit? Edit { get; init; }

    /// <summary>The command, when the action is or ends with one.</summary>
    internal LspCommand? Command { get; init; }

    /// <summary>Whether the action is an old-style bare command, without a title of its own.</summary>
    internal bool IsBareCommand { get; init; }

    /// <summary>The JSON the server sent, for <c>codeAction/resolve</c>.</summary>
    internal JsonElement Raw { get; }

    /// <summary>The command <c>codeAction/resolve</c> added, if any.</summary>
    internal LspCommand? ResolvedCommand { get; set; }

    /// <summary>The edit, once resolved; <see langword="null"/> for an action that is only a command.</summary>
    internal LspWorkspaceEdit? ResolvedEdit { get; set; }

    /// <summary>Whether the action's command has run.</summary>
    internal bool CommandRan { get; set; }

    /// <summary>The command to run: the resolved one, or the one it was listed with.</summary>
    internal LspCommand? EffectiveCommand => ResolvedCommand ?? Command;

    /// <summary>Whether the action's command still has to run after its edit was applied.</summary>
    public bool HasCommandAfterEdit => !CommandRan && ResolvedEdit is not null && EffectiveCommand is not null;
}

/// <summary>A command the server runs when asked with <c>workspace/executeCommand</c>.</summary>
/// <param name="Title">Its title.</param>
/// <param name="Name">The command's identifier.</param>
/// <param name="Arguments">Its arguments, as the server sent them.</param>
internal sealed record LspCommand(string Title, string Name, JsonElement? Arguments);
