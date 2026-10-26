using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Workspaces;
using TabStudio.Porting;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>The real IDE path: conversion, summary, links, a single proposed agent task, F5 and undo.</summary>
public sealed class PortingTests
{
    [AvaloniaTheory]
    [InlineData("WpfTemplate")]
    [InlineData("WinFormsTemplate")]
    [InlineData("WpfContacts")]
    [InlineData("WinFormsCustomers")]
    public async Task Every_sample_ports_into_the_solution_and_runs_with_F5(string sample)
    {
        using var fixture = new WorkspaceFixture();
        var input = Path.Combine(AppContext.BaseDirectory, "PortingSamples", sample, "Input");
        var sourceFolder = Directory.GetDirectories(input).Single();
        var sourceName = Path.GetFileName(sourceFolder);
        var root = Path.Combine(fixture.Paths.Projects, "Porting");
        var original = Path.Combine(root, sourceName);
        CopyTree(sourceFolder, original);
        var originals = Directory.GetFiles(original, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        var solution = Path.Combine(root, "Porting.slnx");
        File.WriteAllText(solution, $"<Solution><Project Path=\"{sourceName}/{sourceName}.csproj\" /></Solution>");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var source = workspace.Workspace!.Projects.Single();
        var summary = await workspace.PortProjectAsync(source, sourceName + ".Avalonia");
        Assert.Equal(2, workspace.Workspace!.Projects.Count);
        Assert.Equal(ProjectKind.Avalonia, workspace.StartupProject!.Kind);
        Assert.Same(summary, workspace.ActiveDocument!.PortSummary);
        Assert.Contains(workspace.Compatibility, r => r.Project == summary.Result.ProjectName && r.IsReady);
        Assert.True(originals.All(f => f.Value.SequenceEqual(File.ReadAllBytes(f.Key))));
        if (summary.Notes.Count > 0)
        {
            Assert.Contains("Port notes (file:line: what. why)", workspace.Agent.InputText, StringComparison.Ordinal);
            foreach (var note in summary.Notes)
            {
                Assert.Contains(note.What, workspace.Agent.InputText, StringComparison.Ordinal);
            }

            await summary.OpenNoteCommand.ExecuteAsync(summary.Notes[0]);
            Assert.Equal(Path.Combine(summary.Result.ProjectDirectory, summary.Notes[0].File), workspace.ActiveDocument!.FilePath);
        }

        var window = new Window { Width = 1440, Height = 960, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            workspace.ActiveDocument = workspace.Documents.Single(d => d.PortSummary == summary);
            UiTestHelpers.SaveScreenshot(window, "port-" + sample + "-summary");
            workspace.AppPreview.Frame = AppPreviewFrame.Tablet;
            var run = workspace.StartDebuggingCommand.ExecuteAsync(null);
            try
            {
                await UiTestHelpers.WaitUntilAsync(() => workspace.AppPreview.IsRunning || run.IsCompleted, TimeSpan.FromMinutes(4), "the converted app");
                Assert.True(workspace.AppPreview.IsRunning, workspace.Output.Text);
                if (sample == "WinFormsCustomers")
                {
                    var controls = workspace.AppPreview.View!.GetLogicalDescendants().OfType<Control>().ToList();
                    ((TextBox)controls.Single(c => c.Name == "nameTextBox")).Text = "Yahav";
                    ((TextBox)controls.Single(c => c.Name == "emailTextBox")).Text = "test@example.com";
                    var button = (Button)controls.Single(c => c.Name == "addButton");
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Contains(workspace.AppPreview.View!.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "1 customer");
                }

                UiTestHelpers.SaveScreenshot(window, "port-" + sample + "-running");
            }
            finally
            {
                workspace.StopCommand.Execute(null);
                await run.WaitAsync(TimeSpan.FromSeconds(30));
            }

            var converted = summary.Result.ProjectDirectory;
            File.WriteAllText(Path.Combine(converted, "user-change.txt"), "Preserve this when undoing.");
            await summary.UndoCommand.ExecuteAsync(null);
            Assert.True(summary.IsUndone);
            Assert.Single(workspace.Workspace!.Projects);
            Assert.False(Directory.Exists(converted));
            Assert.Contains(Directory.EnumerateFiles(fixture.Paths.Trash, "user-change.txt", SearchOption.AllDirectories), File.Exists);
            Assert.True(originals.All(f => f.Value.SequenceEqual(File.ReadAllBytes(f.Key))));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Doctor_and_explorer_open_the_dialog_and_cancel_leaves_the_original_untouched()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WindowsFormsId, "Original");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var doctor = workspace.Compatibility.Single();
        Assert.True(doctor.CanPortToAvalonia);
        var request = workspace.PortToAvaloniaCommand.ExecuteAsync(doctor);
        Assert.True(workspace.Prompt.IsOpen);
        Assert.Equal("Original.Avalonia", workspace.Prompt.Text);
        workspace.Prompt.CancelCommand.Execute(null);
        await request;
        var explorer = workspace.Explorer.Rows.Single(r => r.IsProject);
        Assert.True(explorer.CanPortToAvalonia);
        request = explorer.PortToAvaloniaCommand.ExecuteAsync(null);
        Assert.True(workspace.Prompt.IsOpen);
        workspace.Prompt.CancelCommand.Execute(null);
        await request;
        Assert.Single(workspace.Workspace!.Projects);
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
