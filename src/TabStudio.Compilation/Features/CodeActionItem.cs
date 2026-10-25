namespace TabStudio.Compilation.Features;

/// <summary>An entry of the Ctrl+. (light bulb) menu.</summary>
/// <param name="Id">Identifies the action for <see cref="CodeActionFeature.GetCodeActionEditsAsync"/>.</param>
/// <param name="Title">The text of the menu entry, such as <c>using System.Text;</c>.</param>
/// <param name="Group">
/// The title of the submenu the entry belongs to, such as <c>Implement interface</c>, or <see langword="null"/>
/// for a top-level entry.
/// </param>
/// <param name="Kind">Whether it is a fix or a refactoring.</param>
/// <param name="DiagnosticId">The id of the diagnostic a fix is for, such as <c>CS0246</c>, or <see langword="null"/>.</param>
public sealed record CodeActionItem(string Id, string Title, string? Group, CodeActionKind Kind, string? DiagnosticId);
