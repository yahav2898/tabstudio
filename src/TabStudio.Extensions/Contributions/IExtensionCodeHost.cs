using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.Contributions;

/// <summary>
/// Runs an extension's JavaScript (its <c>main</c> entry point) in an extension host on Node, which is what an
/// extension's commands, settings and language server need. TabStudio's declarative contributions work without one;
/// when a host is available, <see cref="ContributionSupport"/> reports the code-based contributions it runs as active.
/// </summary>
public interface IExtensionCodeHost
{
    /// <summary>Whether this host can run an extension's code on this device.</summary>
    /// <param name="manifest">The extension's manifest.</param>
    bool CanRun(ExtensionManifest manifest);
}
