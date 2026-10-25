using TabStudio.Core.Workspaces;

namespace TabStudio.Web.RazorApps;

/// <summary>
/// The runtime switches a Razor project sets through its properties, which the SDK writes to the app's
/// <c>runtimeconfig.json</c>. A web app runs in TabStudio's process, so they are set on its <see cref="AppContext"/>
/// before it starts.
/// </summary>
public static class RazorAppSwitches
{
    /// <summary>
    /// The switch of <c>BlazorDisableThrowNavigationException</c>: <c>NavigationManager.NavigateTo</c> during static
    /// rendering redirects without throwing, as the .NET 10 templates ask.
    /// </summary>
    public const string DisableThrowNavigationException = "Microsoft.AspNetCore.Components.Endpoints.NavigationManager.DisableThrowNavigationException";

    /// <summary>Sets the switches of an app for the app about to start.</summary>
    /// <param name="project">The web app.</param>
    public static void Apply(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var disable = string.Equals(project.GetProperty("BlazorDisableThrowNavigationException")?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        AppContext.SetSwitch(DisableThrowNavigationException, disable);
    }
}
