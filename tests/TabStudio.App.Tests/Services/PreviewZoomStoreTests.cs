using TabStudio.App.Services;
using TabStudio.Core.Previews;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

public sealed class PreviewZoomStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_project_without_a_choice_shows_Fit()
    {
        var store = new PreviewZoomStore(new AppPaths(_temp.Path));

        Assert.Equal(PreviewZoomMode.Fit, store.Load(Path.Combine(_temp.Path, "Calculator", "Calculator.csproj")));
    }

    [Fact]
    public void Each_project_keeps_its_own_mode_across_sessions()
    {
        var calculator = Path.Combine(_temp.Path, "Calculator", "Calculator.csproj");
        var editor = Path.Combine(_temp.Path, "Editor", "Editor.csproj");
        var store = new PreviewZoomStore(new AppPaths(_temp.Path));
        store.Save(calculator, PreviewZoomMode.ActualSize);
        store.Save(editor, PreviewZoomMode.Fill);

        // A new store, as after TabStudio restarts, reads the same file.
        var reopened = new PreviewZoomStore(new AppPaths(_temp.Path));
        Assert.Equal(PreviewZoomMode.ActualSize, reopened.Load(calculator));
        Assert.Equal(PreviewZoomMode.Fill, reopened.Load(editor));

        reopened.Save(calculator, PreviewZoomMode.Fit);
        Assert.Equal(PreviewZoomMode.Fit, new PreviewZoomStore(new AppPaths(_temp.Path)).Load(calculator));
        Assert.Equal(PreviewZoomMode.Fill, new PreviewZoomStore(new AppPaths(_temp.Path)).Load(editor));
    }

    [Fact]
    public void The_modes_are_saved_by_name_and_a_damaged_file_starts_over()
    {
        var paths = new AppPaths(_temp.Path);
        var project = Path.Combine(_temp.Path, "Calculator.csproj");
        new PreviewZoomStore(paths).Save(project, PreviewZoomMode.ActualSize);
        var file = Path.Combine(paths.State, "preview-zoom.json");
        Assert.Contains("\"ActualSize\"", File.ReadAllText(file), StringComparison.Ordinal);

        File.WriteAllText(file, "{ not json");
        var store = new PreviewZoomStore(paths);
        Assert.Equal(PreviewZoomMode.Fit, store.Load(project));
        store.Save(project, PreviewZoomMode.Fill);
        Assert.Equal(PreviewZoomMode.Fill, new PreviewZoomStore(paths).Load(project));
    }

    public void Dispose() => _temp.Dispose();
}
