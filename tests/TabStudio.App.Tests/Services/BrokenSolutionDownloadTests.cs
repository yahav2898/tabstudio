using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

/// <summary>
/// A downloaded repository whose <c>.slnx</c> cannot be read: the start screen stayed as it was, with no preview, no
/// error and no way to open what was downloaded.
/// </summary>
public sealed class BrokenSolutionDownloadTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeGitHub _github = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public BrokenSolutionDownloadTests()
    {
        var paths = new AppPaths(_temp.Path);
        _services = AppServices.Create(paths, services => services.AddSingleton(_github.CreateAccount(paths)));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task A_repository_with_a_broken_solution_is_previewed_kept_in_Recent_and_opens_with_the_error_listed()
    {
        _github.AddRepository("shop", new Dictionary<string, string>
        {
            ["Shop.slnx"] = "<Solution>\n  <Project Path=\"src/App/App.csproj\" />\n<<<<<<< HEAD\n",
            ["src/App/App.csproj"] = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                </Project>
                """,
            ["src/App/Program.cs"] = "System.Console.WriteLine(\"shop\");\n",
        });
        var start = _main.Start;
        start.SelectCommand.Execute(StartAction.GitHub);
        start.RepositoryText = $"{FakeGitHub.Login}/shop";

        await start.DownloadCommand.ExecuteAsync(null);

        Assert.Null(start.ErrorText);
        var preview = Assert.IsType<WorkspacePreviewViewModel>(start.Preview);
        Assert.Contains(start.RecentGroups.SelectMany(g => g.Items), i => i.Entry.Path == preview.Path);

        start.OpenPreviewCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => _main.Workspace?.Workspace is not null, because: "the folder to open");
        var workspace = _main.Workspace!;
        Assert.Equal("App", workspace.StartupProject?.Name);
        Assert.Contains(workspace.ErrorList.Items, i => i.Diagnostic.Id == "TS1101" && i.Diagnostic.Message.Contains("Shop.slnx", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _github.Dispose();
        _temp.Dispose();
    }
}
