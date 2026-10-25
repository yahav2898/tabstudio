namespace TabStudio.Swarm;

/// <summary>A layout problem the layout checks found on a device's screen, as it travels to the tablet.</summary>
/// <param name="Kind">The kind of problem, as the app's layout checks name it, such as <c>ClippedText</c>.</param>
/// <param name="Severity">How serious it is: <c>Warning</c> or <c>Error</c>.</param>
/// <param name="Message">What is wrong, in plain words.</param>
/// <param name="ElementPath">The element.</param>
/// <param name="X">The element's left edge on the screen, in device-independent pixels.</param>
/// <param name="Y">The element's top edge on the screen, in device-independent pixels.</param>
/// <param name="Width">The element's width.</param>
/// <param name="Height">The element's height.</param>
public sealed record SwarmLayoutIssue(string Kind, string Severity, string Message, string ElementPath, double X, double Y, double Width, double Height);
