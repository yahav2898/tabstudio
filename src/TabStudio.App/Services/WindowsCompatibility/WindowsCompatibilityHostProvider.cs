using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.Toolchains.Linux;
using TabStudio.WindowsCompatibility.Building;

namespace TabStudio.App.Services.WindowsCompatibility;

/// <summary>
/// Windows Compatibility as a run host for Windows Forms and WPF apps: ready once installed, installable from the
/// Toolchain Store where the device can run it, and otherwise with the reason it cannot.
/// </summary>
/// <param name="service">Windows Compatibility on this device.</param>
public sealed class WindowsCompatibilityHostProvider(WindowsCompatibilityService service) : IRunHostProvider
{
    /// <summary>The host's name, for the Run button and the Compatibility Doctor.</summary>
    public const string Name = "Windows Compatibility";

    /// <inheritdoc />
    public IEnumerable<RunHostOption> OptionsFor(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Language != ProjectLanguage.CSharp || WindowsDesktopProject.From(project) is not { IsApp: true } desktop)
        {
            yield break;
        }

        var app = desktop.UsesWpf ? "WPF" : "Windows Forms";
        if (service.Problem is { } problem)
        {
            yield return Option(CompatibilityStatus.Unsupported, problem);
        }
        else if (service.IsInstalled)
        {
            yield return Option(
                CompatibilityStatus.Ready,
                $"Runs the real {app} app on this tablet: .NET's Windows Desktop runtime under Wine, shown in the app preview, with touch as the mouse. Saving a file restarts it.");
        }
        else if (service.CanInstall)
        {
            var size = service.Status.DownloadSize > 0 ? $" (a {DiskSpace.Format(service.Status.DownloadSize)} download)" : string.Empty;
            yield return Option(
                CompatibilityStatus.NeedsToolchain,
                $"Runs the real {app} app on this tablet under Wine, once Windows Compatibility is installed from the Toolchain Store{size}.");
        }
        else
        {
            yield return Option(CompatibilityStatus.Unsupported, service.Status.Note ?? "Windows Compatibility cannot be installed on this device.");
        }
    }

    private static RunHostOption Option(CompatibilityStatus status, string explanation) =>
        new(RunHost.WindowsCompatibility, Name, status, explanation, RunHostOption.WindowsCompatibilityPreference);
}
