namespace TabStudio.Web.Serving;

/// <summary>A message a page posted to the server that serves it, such as the console output of an app in the web preview.</summary>
/// <param name="Route">The route it was posted to, without the leading <c>/</c>, such as <c>_tabstudio/console</c>.</param>
/// <param name="Body">Its body, as UTF-8 text.</param>
public sealed record PostedMessage(string Route, string Body);
