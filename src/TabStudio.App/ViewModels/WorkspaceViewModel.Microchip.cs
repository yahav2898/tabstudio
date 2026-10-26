using TabStudio.Core.Diagnostics;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private bool _lastPicBuildSucceeded;

    private void ReportPicBuild(ToolchainProject project, IReadOnlyList<string> lines, bool succeeded)
    {
        _lastPicBuildSucceeded = succeeded;
        var text = string.Join('\n', lines);
        var diagnostics = PicCompilerDiagnostic.Parse(text).Select(d =>
        {
            var path = _toolchains.Runtime.ToDevicePath(d.File, project.Directory) ?? ResolveOutputPath(d.File);
            return new BuildDiagnostic(project.Toolchain.Name, d.Severity, d.Message,
                path is null ? null : new DiagnosticLocation(path, OffsetOf(path, d.Line), 0, d.Line, d.Column), project.Name);
        }).ToList();
        ErrorList.SetRestoreDiagnostics("pic:" + project.Directory, diagnostics);
        if (!succeeded)
        {
            return;
        }

        var memory = PicMemoryUsage.Parse(text);
        static string Region(PicMemoryRegion? region) => region is null ? "not reported"
            : region.CapacityBytes is { } total ? $"{region.UsedBytes:N0} / {total:N0} bytes" : $"{region.UsedBytes:N0} bytes";
        var summary = $"Program: {Region(memory.Program)} · Data: {Region(memory.Data)}";
        Output.WriteLine(summary, OutputKind.Info);
        StatusText = summary;
    }
}
