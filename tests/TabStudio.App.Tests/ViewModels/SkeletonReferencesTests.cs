using Avalonia.Headless.XUnit;
using TabStudio.App.Platform;
using TabStudio.App.Services;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>On a compact tablet, only the projects of the open documents keep a full compiler model.</summary>
public sealed class SkeletonReferencesTests
{
    private static readonly DeviceInfo CompactTablet = new(3L * 1024 * 1024 * 1024, 4, IsLowRamDevice: false);

    [AvaloniaFact]
    public async Task A_compact_tablet_references_projects_without_open_documents_through_skeletons()
    {
        using var fixture = new WorkspaceFixture(CompactTablet);
        Assert.True(fixture.Profile.UseSkeletonReferences);
        var solution = await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var host = Assert.IsType<LocalLanguageService>(workspace.LanguageService).Host;
        var root = Path.GetDirectoryName(solution)!;
        var app = Path.Combine(root, "src", "HelloTablet", "HelloTablet.csproj");
        var library = Path.Combine(root, "src", "Greetings", "Greetings.csproj");

        // The sample opens with Program.cs, so its project is the one being edited.
        await UiTestHelpers.WaitUntilAsync(() => host.IsSkeleton(library), because: "the library to become a skeleton");
        Assert.True(host.UsesSkeletonReferences);
        Assert.Equal(new[] { app }, workspace.ActiveProjects);
        Assert.False(host.IsSkeleton(app));

        // Opening a file of the library brings it back in full.
        await workspace.OpenDocumentAsync(Path.Combine(root, "src", "Greetings", "Greeter.cs"));
        await UiTestHelpers.WaitUntilAsync(() => !host.IsSkeleton(library), because: "the library to come back in full");

        // Builds always compile every project in full, so F5 works as on any device.
        fixture.AnswerInput("Tablet");
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains(", Tablet!", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Larger_tablets_keep_every_project_in_full()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var host = Assert.IsType<LocalLanguageService>(fixture.Workspace.LanguageService).Host;

        Assert.False(fixture.Profile.UseSkeletonReferences);
        Assert.False(host.UsesSkeletonReferences);
        Assert.Empty(fixture.Workspace.ActiveProjects);
    }
}
