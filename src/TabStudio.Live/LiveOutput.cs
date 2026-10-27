namespace TabStudio.Live;

/// <summary>A piece of the host's Output panel: build messages and the running program's console.</summary>
/// <param name="Text">The text, which may hold several lines.</param>
/// <param name="Kind">What it is.</param>
public sealed record LiveOutput(string Text, LiveOutputKind Kind);
