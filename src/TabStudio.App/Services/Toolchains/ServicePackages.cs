using TabStudio.Services;
using TabStudio.Services.Compose;
using TabStudio.Services.Native;
using TabStudio.Toolchains.Alpine;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Services.Toolchains;

/// <summary>Services use the Toolchain Store's signed, resumable Alpine package downloads.</summary>
public sealed class ServicePackages(ToolchainServices toolchains) : IServicePackages
{
    public async Task<MappingResult> EnsureAsync(ComposeService service, Action<string> log, CancellationToken cancellationToken)
    {
        await toolchains.Manager.PrepareRuntimeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var packages = toolchains.ServicePackageManager;
        var mapping = ServiceCatalog.Map(service, packages is null ? null : await packages.LoadIndexAsync(refresh: false, cancellationToken).ConfigureAwait(false));
        if (!mapping.Supported) return mapping;
        if (packages is null)
        {
            // Desktop development uses installed host tools. The tablet always uses its private Alpine runtime.
            if (toolchains.Runtime.Kind != RuntimeKind.Linux)
                return new(null, "Native Services need TabStudio's Alpine environment. On desktop use the Alpine CI harness or a remote host.");
            return mapping;
        }
        await toolchains.WithPackageOperationAsync(async () =>
        {
            var plan = ApkResolver.PlanInstall(await packages.LoadIndexAsync(refresh: false, cancellationToken).ConfigureAwait(false), packages.Installed.Packages, mapping.Mapping!.Packages);
            toolchains.Environment!.EnsureSpace(plan.DownloadSize + plan.InstalledSize, service.Name);
            await packages.DownloadAsync(plan, (bytes, package) => log($"Downloading {package.Name} ({bytes / 1024} KB)"), cancellationToken).ConfigureAwait(false);
            await packages.InstallAsync(mapping.Mapping.Packages, upgrade: false, log, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return mapping;
    }
}
