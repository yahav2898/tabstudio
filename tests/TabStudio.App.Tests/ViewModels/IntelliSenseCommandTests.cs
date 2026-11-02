using Avalonia.Headless.XUnit;
using TabStudio.App.ViewModels;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>The IntelliSense commands of the workspace: navigation, references, rename, code actions, breadcrumb.</summary>
public sealed class IntelliSenseCommandTests
{
    [AvaloniaFact]
    public async Task Go_to_definition_opens_the_declaration_in_another_project()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var program = fixture.Workspace.ActiveDocument!;
        program.CaretOffset = OffsetOf(program, "greeter.Greet()", "greeter.".Length + 1);

        await fixture.Workspace.GoToDefinitionCommand.ExecuteAsync(null);

        var greeter = fixture.Workspace.ActiveDocument!;
        Assert.Equal("Greeter.cs", greeter.FileName);
        var line = greeter.Document.GetLineByNumber(greeter.PendingNavigation!.Value.Line);
        Assert.Contains("public string Greet()", greeter.Document.GetText(line), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Go_to_definition_with_no_file_open_does_not_name_one_language()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        await fixture.Workspace.CloseDocumentCommand.ExecuteAsync(fixture.Workspace.ActiveDocument);

        await fixture.Workspace.GoToDefinitionCommand.ExecuteAsync(null);

        Assert.Equal("Open a code file first.", fixture.Workspace.StatusText);
    }

    [AvaloniaFact]
    public async Task Go_to_definition_of_a_framework_method_names_its_assembly()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var program = fixture.Workspace.ActiveDocument!;
        program.CaretOffset = OffsetOf(program, "Console.ReadLine()", "Console.".Length + 2);

        await fixture.Workspace.GoToDefinitionCommand.ExecuteAsync(null);

        Assert.Same(program, fixture.Workspace.ActiveDocument);
        Assert.Contains("ReadLine", fixture.Workspace.StatusText, StringComparison.Ordinal);
        Assert.Contains("is defined in System.Console", fixture.Workspace.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Go_to_implementation_opens_the_class_that_implements_an_interface()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greetings = fixture.Workspace.Workspace!.Projects.Single(p => p.Name == "Greetings").DirectoryPath;
        var greeterPath = Path.Combine(greetings, "Greeter.cs");
        var interfacePath = Path.Combine(greetings, "IGreeter.cs");
        await File.WriteAllTextAsync(interfacePath, "namespace Greetings;\n\npublic interface IGreeter\n{\n    string Greet();\n}\n");
        var greeterText = await File.ReadAllTextAsync(greeterPath);
        await File.WriteAllTextAsync(greeterPath, greeterText.Replace("public sealed class Greeter", "public sealed class Greeter : IGreeter", StringComparison.Ordinal));
        await fixture.Workspace.ReloadWorkspaceAsync();
        var contract = (await fixture.Workspace.OpenDocumentAsync(interfacePath))!;
        contract.CaretOffset = OffsetOf(contract, "IGreeter", 2);

        await fixture.Workspace.GoToImplementationCommand.ExecuteAsync(null);

        Assert.Equal("Greeter.cs", fixture.Workspace.ActiveDocument?.FileName);
    }

    [AvaloniaFact]
    public async Task Find_all_references_lists_them_by_file_and_opens_the_one_tapped()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var program = fixture.Workspace.ActiveDocument!;
        program.CaretOffset = OffsetOf(program, "greeter.Greet()", "greeter.".Length + 1);

        await fixture.Workspace.FindAllReferencesCommand.ExecuteAsync(null);

        var references = fixture.Workspace.References;
        Assert.Equal(BottomPanel.References, fixture.Workspace.SelectedPanel);
        Assert.Equal("'Greet' references", references.Title);
        Assert.Equal(2, references.Items.Count);
        Assert.Contains(references.Rows, r => r is ReferenceFileRow { FileName: "Program.cs" });
        Assert.Contains(references.Rows, r => r is ReferenceFileRow { FileName: "Greeter.cs" });
        var definition = Assert.Single(references.Rows.OfType<ReferenceLineRow>(), r => r.IsDefinition);
        Assert.Contains(definition.Text, run => run.IsBold && run.Text == "Greet");
        Assert.Equal("2 references in 2 files", references.Summary);

        await references.NavigateCommand.ExecuteAsync(definition);

        Assert.Equal("Greeter.cs", fixture.Workspace.ActiveDocument?.FileName);
    }

    [AvaloniaFact]
    public async Task Rename_changes_open_documents_undoably_and_closed_files_on_disk()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var programPath = workspace.ActiveDocument!.FilePath;
        var greeterPath = Path.Combine(workspace.Workspace!.Projects.Single(p => p.Name == "Greetings").DirectoryPath, "Greeter.cs");
        var greeter = (await workspace.OpenDocumentAsync(greeterPath))!;
        await workspace.CloseDocumentCommand.ExecuteAsync(workspace.Documents.Single(d => d.FilePath == programPath));
        greeter.CaretOffset = OffsetOf(greeter, "string Greet()", "string Gr".Length);

        var rename = workspace.RenameSymbolCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the rename prompt");
        Assert.Equal("Greet", workspace.Prompt.Text);
        workspace.Prompt.Text = "SayHello";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await rename;

        Assert.Contains("public string SayHello()", greeter.Document.Text, StringComparison.Ordinal);
        Assert.Contains("greeter.SayHello()", await File.ReadAllTextAsync(programPath), StringComparison.Ordinal);
        Assert.Contains("SayHello", workspace.StatusText, StringComparison.Ordinal);

        greeter.Document.UndoStack.Undo();
        Assert.Contains("public string Greet()", greeter.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Rename_explains_why_a_keyword_cannot_be_renamed()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var program = fixture.Workspace.ActiveDocument!;
        program.CaretOffset = OffsetOf(program, "return 0;", 2);

        await fixture.Workspace.RenameSymbolCommand.ExecuteAsync(null);

        Assert.False(fixture.Workspace.Prompt.IsOpen);
        Assert.False(string.IsNullOrEmpty(fixture.Workspace.StatusText));
        Assert.NotEqual("Ready", fixture.Workspace.StatusText);
    }

    [AvaloniaFact]
    public async Task A_quick_fix_adds_the_missing_using_directive()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var program = fixture.Workspace.ActiveDocument!;
        var insertAt = OffsetOf(program, "Console.WriteLine();", 0);
        program.Document.Insert(insertAt, "var builder = new StringBuilder();\n");
        program.SyncText();
        var features = program.Features!;

        var actions = await features.GetCodeActionsAsync(program.FilePath, OffsetOf(program, "StringBuilder()", 3), 0, CancellationToken.None);
        var addUsing = Assert.Single(actions, a => a.Title == "using System.Text;");
        Assert.Equal(CodeActionKind.QuickFix, addUsing.Kind);
        var edits = await features.GetCodeActionEditsAsync(addUsing.Id, CancellationToken.None);
        var changed = await fixture.Workspace.ApplyDocumentEditsAsync(edits);

        Assert.Equal(1, changed);
        Assert.Contains("using System.Text;", program.Document.Text, StringComparison.Ordinal);
        await program.AnalyzeNowAsync();
        Assert.DoesNotContain(program.Diagnostics, d => d.Id == "CS0246");
    }

    [AvaloniaFact]
    public async Task The_breadcrumb_follows_the_caret_into_members()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greeterPath = Path.Combine(fixture.Workspace.Workspace!.Projects.Single(p => p.Name == "Greetings").DirectoryPath, "Greeter.cs");
        var greeter = (await fixture.Workspace.OpenDocumentAsync(greeterPath))!;

        await greeter.UpdateSymbolPathAsync(OffsetOf(greeter, "var partOfDay", 0));

        var names = greeter.SymbolPath.Select(c => c.Name).ToList();
        Assert.Contains("Greeter", names);
        Assert.StartsWith("Greet(", names[^1], StringComparison.Ordinal);
        var member = greeter.SymbolPath[^1];
        Assert.Equal(SymbolGlyph.Method, member.Kind);
        Assert.Contains(member.Siblings, s => s.Name.StartsWith("Greeter(", StringComparison.Ordinal));

        member.Siblings.First(s => s.Kind == SymbolGlyph.Field).NavigateCommand.Execute(null);
        var line = greeter.Document.GetLineByNumber(greeter.PendingNavigation!.Value.Line);
        Assert.Contains("private readonly string _name;", greeter.Document.GetText(line), StringComparison.Ordinal);
    }

    private static int OffsetOf(DocumentViewModel document, string text, int within)
    {
        var index = document.Document.Text.IndexOf(text, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{text}' is in {document.FileName}");
        return index + within;
    }
}
