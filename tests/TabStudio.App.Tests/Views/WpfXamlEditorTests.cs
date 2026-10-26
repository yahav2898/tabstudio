using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Compilation.Wpf;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>WPF XAML through the real tablet editor, with completion and handler-action screenshots.</summary>
public sealed class WpfXamlEditorTests
{
    [AvaloniaFact]
    public async Task Typing_in_WPF_XAML_opens_completion_and_Tab_commits_an_attribute_with_quotes()
    {
        using var fixture = new WorkspaceFixture();
        var document = await OpenAsync(fixture);
        var window = Show(fixture);
        try
        {
            var view = ActiveEditor(window);
            Assert.True(document.HasXamlFeatures);
            Assert.NotNull(view.Completion);
            view.Editor.CaretOffset = OffsetOf(view.Editor, "<Button") + "<Button".Length;
            Type(view.Editor, " Horiz");
            await UiTestHelpers.WaitUntilAsync(() => view.Completion!.IsOpen && view.Completion.SelectedItem?.Entry.DisplayText == "HorizontalAlignment", because: "WPF attribute completion");

            UiTestHelpers.SaveScreenshot(window, "wpf-01-xaml-completion");
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

            await UiTestHelpers.WaitUntilAsync(() => view.Editor.Text.Contains("HorizontalAlignment=\"\"", StringComparison.Ordinal), because: "the committed XAML attribute");
            Assert.Equal('"', view.Editor.Document.GetCharAt(view.Editor.CaretOffset));
            Assert.True(document.IsDirty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_light_bulb_adds_an_event_handler_to_unsaved_code_behind_and_F12_finds_it()
    {
        using var fixture = new WorkspaceFixture();
        var document = await OpenAsync(fixture);
        var workspace = fixture.Workspace;
        var codePath = document.FilePath + ".cs";
        var code = (await workspace.OpenDocumentAsync(codePath))!;
        var close = code.Document.Text.LastIndexOf("    }", StringComparison.Ordinal);
        code.Document.Insert(close, "        private string UnsavedText => \"Keep me\";\n");
        await workspace.OpenDocumentAsync(document.FilePath);
        var window = Show(fixture);
        try
        {
            var view = ActiveEditor(window);
            view.Editor.CaretOffset = OffsetOf(view.Editor, "Greet_Click") + 3;
            await document.AnalyzeAgainAsync();
            Assert.Contains(document.Diagnostics, d => d.Id == WpfDiagnosticIds.MissingEventHandler);
            await UiTestHelpers.WaitUntilAsync(() => view.IsLightBulbVisible && view.CodeActions.Any(a => a.Title.StartsWith("Add Greet_Click", StringComparison.Ordinal)), because: "the missing handler's light bulb");
            workspace.QuickActionsCommand.Execute(null);
            await UiTestHelpers.WaitUntilAsync(() => window.GetVisualDescendants().OfType<MenuItem>().Any(i => i.Header is TextBlock { Text: { } title } && title.StartsWith("Add Greet_Click", StringComparison.Ordinal)), because: "the add-handler menu");
            UiTestHelpers.SaveScreenshot(window, "wpf-02-add-event-handler");

            await view.ApplyCodeActionAsync(Assert.Single(view.CodeActions));
            Assert.Contains("private void Greet_Click(object sender,", code.Document.Text, StringComparison.Ordinal);
            Assert.Contains("UnsavedText", code.Document.Text, StringComparison.Ordinal);
            Assert.True(code.IsDirty);
            Assert.DoesNotContain("Greet_Click", await File.ReadAllTextAsync(codePath), StringComparison.Ordinal);
            await code.AnalyzeAgainAsync();
            await document.AnalyzeAgainAsync();
            Assert.DoesNotContain(document.Diagnostics, d => d.Id == WpfDiagnosticIds.MissingEventHandler);
            Assert.DoesNotContain(code.Diagnostics, d => d.Severity == Core.Diagnostics.DiagnosticSeverityLevel.Error);

            await workspace.GoToDefinitionCommand.ExecuteAsync(null);
            Assert.Same(code, workspace.ActiveDocument);
            Assert.NotNull(code.PendingNavigation);
            code.Document.UndoStack.Undo();
            Assert.DoesNotContain("private void Greet_Click", code.Document.Text, StringComparison.Ordinal);
            Assert.Contains("UnsavedText", code.Document.Text, StringComparison.Ordinal);
            await code.AnalyzeAgainAsync();
            await document.AnalyzeAgainAsync();
            Assert.Contains(document.Diagnostics, d => d.Id == WpfDiagnosticIds.MissingEventHandler);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Adding_a_handler_to_closed_code_behind_writes_the_file_and_clears_the_XAML_error()
    {
        using var fixture = new WorkspaceFixture();
        var document = await OpenAsync(fixture);
        var window = Show(fixture);
        try
        {
            Assert.DoesNotContain(fixture.Workspace.Documents, d => d.FilePath == document.FilePath + ".cs");
            var view = ActiveEditor(window);
            view.Editor.CaretOffset = OffsetOf(view.Editor, "Greet_Click") + 3;
            var actions = await view.ListCodeActionsAsync(CancellationToken.None);

            await view.ApplyCodeActionAsync(Assert.Single(actions!));

            Assert.Contains("private void Greet_Click(object sender,", await File.ReadAllTextAsync(document.FilePath + ".cs"), StringComparison.Ordinal);
            await UiTestHelpers.WaitUntilAsync(() => !document.Diagnostics.Any(d => d.Id == WpfDiagnosticIds.MissingEventHandler), because: "the closed code-behind fix to clear the XAML error");
            Assert.DoesNotContain(fixture.Workspace.ErrorList.Items, i => i.Code == WpfDiagnosticIds.MissingEventHandler);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Live_XAML_errors_reach_the_Error_List_and_quick_info_uses_the_current_text()
    {
        using var fixture = new WorkspaceFixture();
        var document = await OpenAsync(fixture);
        var window = Show(fixture);
        try
        {
            var view = ActiveEditor(window);
            var button = OffsetOf(view.Editor, "Button");
            view.Editor.CaretOffset = button + 2;
            fixture.Workspace.QuickInfoCommand.Execute(null);
            await UiTestHelpers.WaitUntilAsync(() => view.QuickInfo is not null, because: "XAML Quick Info");
            Assert.Contains("Button", string.Concat(view.QuickInfo!.Signature.Select(r => r.Text)), StringComparison.Ordinal);

            document.Document.Replace(button, "Button".Length, "Buton");
            await UiTestHelpers.WaitUntilAsync(() => document.Diagnostics.Any(d => d.Message.Contains("Buton", StringComparison.Ordinal)), because: "live XAML analysis after typing");

            Assert.Contains(document.Diagnostics, d => d.Message.Contains("Buton", StringComparison.Ordinal));
            Assert.Contains(fixture.Workspace.ErrorList.Items, r => r.Description.Contains("Buton", StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Switching_away_from_and_back_to_local_compilation_keeps_the_open_editors_features_valid()
    {
        using var fixture = new WorkspaceFixture();
        var document = await OpenAsync(fixture);
        var host = Assert.IsType<LocalLanguageService>(fixture.Workspace.LanguageService).Host;
        var features = document.Features!;
        var position = document.Document.Text.IndexOf("Button", StringComparison.Ordinal) + 2;
        void Synchronize()
        {
            foreach (var open in fixture.Workspace.Documents)
            {
                open.SyncText();
            }
        }

        document.AttachWpfXaml(null, Synchronize);
        Assert.Same(features, document.Features);
        Assert.Null(await features.GetQuickInfoAsync(document.FilePath, position, CancellationToken.None));
        document.AttachWpfXaml(host, Synchronize);
        Assert.Same(features, document.Features);
        Assert.NotNull(await features.GetQuickInfoAsync(document.FilePath, position, CancellationToken.None));
    }

    [AvaloniaFact]
    public async Task Closing_XAML_cancels_pending_analysis_and_other_XML_files_keep_their_editor()
    {
        using var fixture = new WorkspaceFixture();
        var document = await OpenAsync(fixture);
        var xmlPath = Path.Combine(Path.GetDirectoryName(document.FilePath)!, "notes.xml");
        await File.WriteAllTextAsync(xmlPath, "<notes />");
        var xml = (await fixture.Workspace.OpenDocumentAsync(xmlPath))!;
        Assert.False(xml.HasXamlFeatures);
        Assert.Null(xml.Features);

        document.Document.Insert(document.Document.TextLength, "<");
        var analysis = document.AnalyzeAgainAsync();
        document.Dispose();
        await analysis;
        Assert.False(document.HasXamlFeatures);
    }

    private static async Task<DocumentViewModel> OpenAsync(WorkspaceFixture fixture)
    {
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WpfId, "HelloWpf");
        var xaml = Path.Combine(Path.GetDirectoryName(solution)!, "HelloWpf", "MainWindow.xaml");
        var source = await File.ReadAllTextAsync(xaml);
        await File.WriteAllTextAsync(xaml, source.Replace("<Grid>", "<Grid>\n        <Button Content=\"Greet\" Click=\"Greet_Click\" />", StringComparison.Ordinal));
        await fixture.Workspace.LoadAsync(solution);
        var document = (await fixture.Workspace.OpenDocumentAsync(xaml))!;
        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.PackagesText is null, because: "OpenSilver to restore");
        await document.AnalyzeAgainAsync();
        Assert.True(document.HasXamlFeatures);
        return document;
    }

    private static Window Show(WorkspaceFixture fixture)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        return window;
    }
}
