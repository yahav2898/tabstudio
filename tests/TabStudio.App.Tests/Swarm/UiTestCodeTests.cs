using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.Recording;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// Recorded UI tests as code: the C# a recording becomes reads like a person wrote it and reads back into the same
/// steps; element paths find the same element as the generated driver does; and a test goes into the app's existing
/// xUnit project, or a new one beside it.
/// </summary>
public sealed class UiTestCodeTests
{
    private static readonly ScreenProfile Pixel = new("Pixel 7", FormFactor.Phone, 412, 915, 2.6, true, TextDirection.LeftToRight, 1);

    [Fact]
    public void A_recording_becomes_a_test_that_reads_back_into_the_same_steps()
    {
        UiTestStep[] steps =
        [
            new(UiTestStepKind.Tap, "MainView/StackPanel[0]/TextBox#NameBox"),
            new(UiTestStepKind.Type, string.Empty, "Ada \"Lovelace\""),
            new(UiTestStepKind.Press, string.Empty, Key: "Enter"),
            new(UiTestStepKind.Press, string.Empty, Key: "A", Modifiers: "Control, Shift"),
            new(UiTestStepKind.Scroll, "MainView/ScrollViewer[0]", ScrollY: 240),
            new(UiTestStepKind.LongPress, "MainView/StackPanel[0]/Button#Save"),
            UiTestStep.Check("MainView/StackPanel[0]/TextBlock[1]", "Saved Ada"),
        ];
        var code = UiTestWriter.Test("Shop.UITests.Recorded", new UiTestRecording("Saving a name", Pixel, steps));

        Assert.Contains("namespace Shop.UITests.Recorded;", code, StringComparison.Ordinal);
        Assert.Contains("public sealed class SavingANameTest", code, StringComparison.Ordinal);
        Assert.Contains("[AvaloniaFact]", code, StringComparison.Ordinal);
        Assert.Contains("[Trait(\"Category\", \"Recorded UI test\")]", code, StringComparison.Ordinal);
        Assert.Contains("public void Saving_a_name()", code, StringComparison.Ordinal);
        Assert.Contains("using var app = RecordedApp.Start(width: 412, height: 915);", code, StringComparison.Ordinal);
        Assert.Contains("        app.Tap(\"NameBox\");\n", code, StringComparison.Ordinal);
        Assert.Contains("app.Type(\"Ada \\\"Lovelace\\\"\");", code, StringComparison.Ordinal);
        Assert.Contains("app.Press(Key.Enter);", code, StringComparison.Ordinal);
        Assert.Contains("app.Press(Key.A, KeyModifiers.Control | KeyModifiers.Shift);", code, StringComparison.Ordinal);
        Assert.Contains("app.Scroll(\"MainView/ScrollViewer[0]\", deltaX: 0, deltaY: 240);", code, StringComparison.Ordinal);
        Assert.Contains("app.LongPress(\"Save\");", code, StringComparison.Ordinal);
        Assert.Contains("app.ShouldShow(\"MainView/StackPanel[0]/TextBlock[1]\", \"Saved Ada\");", code, StringComparison.Ordinal);

        var test = Assert.Single(UiTestReader.Read("/shop/SavingANameTest.cs", code));
        Assert.Equal("Shop.UITests.Recorded.SavingANameTest", test.TypeFullName);
        Assert.Equal("Shop.UITests.Recorded.SavingANameTest.Saving_a_name", test.MethodFullName);
        Assert.Equal("Saving a name", test.DisplayName);
        Assert.Equal((412d, 915d), (test.Width, test.Height));
        Assert.Empty(test.Unsupported);
        Assert.Equal(
            [
                new UiTestStep(UiTestStepKind.Tap, "NameBox"),
                new UiTestStep(UiTestStepKind.Type, string.Empty, "Ada \"Lovelace\""),
                new UiTestStep(UiTestStepKind.Press, string.Empty, Key: "Enter"),
                new UiTestStep(UiTestStepKind.Press, string.Empty, Key: "A", Modifiers: "Control, Shift"),
                new UiTestStep(UiTestStepKind.Scroll, "MainView/ScrollViewer[0]", ScrollY: 240),
                new UiTestStep(UiTestStepKind.LongPress, "Save"),
                UiTestStep.Check("MainView/StackPanel[0]/TextBlock[1]", "Saved Ada"),
            ],
            test.Steps);
    }

    [Fact]
    public void Names_become_a_class_and_a_sentence_and_code_added_by_hand_only_runs_with_dotnet_test()
    {
        Assert.Equal("CountingThreeTapsTest", UiTestWriter.ClassName("Counting three taps"));
        Assert.Equal("Counting_three_taps", UiTestWriter.MethodName("counting three taps"));
        Assert.Equal("Test3TapsTest", UiTestWriter.ClassName("3 taps!"));
        Assert.Equal("RecordedTest", UiTestWriter.ClassName("???"));

        var code = UiTestWriter.Test("A", new UiTestRecording("Taps", Pixel, [new UiTestStep(UiTestStepKind.Tap, "Go")]))
            .Replace("        app.Tap(\"Go\");\n", "        app.Tap(\"Go\");\n        Assert.True(DateTime.Now.Year > 2000);\n", StringComparison.Ordinal);
        var test = Assert.Single(UiTestReader.Read("Taps.cs", code));
        Assert.Single(test.Steps);
        Assert.StartsWith("line 16: Assert.True(", Assert.Single(test.Unsupported), StringComparison.Ordinal);
        Assert.Empty(UiTestReader.Read("Other.cs", "public class Plain { [Fact] public void Works() { } }"));
    }

    [AvaloniaFact]
    public void Element_paths_find_elements_by_name_by_index_and_after_a_panel_was_added_around_them()
    {
        var count = new TextBlock { Text = "Clicked 2 times" };
        var button = new Button { Name = "CountButton", Content = "Count" };
        var root = new UserControl { Content = new StackPanel { Children = { new TextBlock { Text = "Hello" }, button, count } } };

        Assert.Same(button, ElementPaths.Resolve(root, "CountButton"));
        Assert.Same(button, ElementPaths.Resolve(root, "UserControl/StackPanel[0]/Button#CountButton"));
        Assert.Same(count, ElementPaths.Resolve(root, "UserControl/StackPanel[0]/TextBlock[1]"));
        Assert.Same(button, ElementPaths.Resolve(root, "UserControl/Grid[0]/Button#CountButton"));
        Assert.Null(ElementPaths.Resolve(root, "UserControl/StackPanel[0]/Button#Missing"));
        var host = new Border { Child = root };
        Assert.Same(count, ElementPaths.Resolve(host, "UserControl/StackPanel[0]/TextBlock[1]"));
        Assert.Same(button, ElementPaths.Resolve(host, "UserControl/StackPanel[0]/Button#CountButton"));
        Assert.Equal("Clicked 2 times", ElementPaths.TextOf(count));
        Assert.Equal("Count", ElementPaths.TextOf(button));
        Assert.Equal("Hello Clicked 2 times", ElementPaths.TextOf((Control)root.Content!));
        Assert.Equal("CountButton", ElementPaths.ShortName("UserControl/StackPanel[0]/Button#CountButton"));
        Assert.Equal("UserControl/StackPanel[0]/TextBlock[1]", ElementPaths.ShortName("UserControl/StackPanel[0]/TextBlock[1]"));
    }

    [Fact]
    public void A_test_goes_into_the_apps_xunit_project_which_gets_the_driver_and_packages_once()
    {
        using var temp = new TempDirectory();
        var app = WriteProject(temp.Path, "Shop", "<OutputType>WinExe</OutputType>", string.Empty);
        File.WriteAllText(Path.Combine(temp.Path, "Shop", "App.axaml"), "<Application xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" x:Class=\"Shop.App\" />");
        var tests = WriteProject(temp.Path, "Shop.Tests", "<IsTestProject>true</IsTestProject>", "<PackageReference Include=\"xunit\" Version=\"2.9.3\" />\n<ProjectReference Include=\"..\\Shop\\Shop.csproj\" />");
        var workspace = new Workspace
        {
            Name = "Shop",
            RootDirectory = temp.Path,
            Tree = new WorkspaceNode(WorkspaceNodeKind.Folder, "Shop", temp.Path),
            Projects =
            [
                new ProjectInfo { Name = "Shop", FilePath = app },
                new ProjectInfo
                {
                    Name = "Shop.Tests",
                    FilePath = tests,
                    IsTestProject = true,
                    Language = ProjectLanguage.CSharp,
                    PackageReferences = [new PackageReferenceInfo("xunit", "2.9.3")],
                    ProjectReferences = [app],
                },
            ],
        };
        var recording = new UiTestRecording("Buying a cake", Pixel, [new UiTestStep(UiTestStepKind.Tap, "Buy")]);

        var first = UiTestProjects.Save(workspace, workspace.Projects[0], recording);
        var second = UiTestProjects.Save(workspace, workspace.Projects[0], recording);

        Assert.False(first.CreatedProject);
        Assert.Equal(tests, first.ProjectFilePath);
        Assert.Equal(Path.Combine(temp.Path, "Shop.Tests", "Recorded", "BuyingACakeTest.cs"), first.TestFilePath);
        Assert.Equal(Path.Combine(temp.Path, "Shop.Tests", "Recorded", "BuyingACake2Test.cs"), second.TestFilePath);
        Assert.Contains("namespace Shop.Tests.Recorded;", File.ReadAllText(first.TestFilePath), StringComparison.Ordinal);
        Assert.Contains("public void Buying_a_cake_2()", File.ReadAllText(second.TestFilePath), StringComparison.Ordinal);
        var driver = File.ReadAllText(Path.Combine(temp.Path, "Shop.Tests", "RecordedApp.cs"));
        Assert.Contains("var app = new global::Shop.App { ApplicationLifetime = lifetime };", driver, StringComparison.Ordinal);
        Assert.Contains("AppBuilder.Configure<global::Shop.App>()", File.ReadAllText(Path.Combine(temp.Path, "Shop.Tests", "TestAppBuilder.cs")), StringComparison.Ordinal);
        var project = File.ReadAllText(tests);
        Assert.Contains("Avalonia.Headless.XUnit", project, StringComparison.Ordinal);
        Assert.Single(project.Split("Avalonia.Headless.XUnit")[1..]);
    }

    [Fact]
    public void An_app_without_a_test_project_gets_one_beside_it_in_the_solution()
    {
        using var temp = new TempDirectory();
        var app = WriteProject(temp.Path, "Shop", "<OutputType>WinExe</OutputType>", string.Empty);
        var solution = Path.Combine(temp.Path, "Shop.slnx");
        File.WriteAllText(solution, "<Solution>\n  <Project Path=\"Shop/Shop.csproj\" />\n</Solution>\n");
        var workspace = new Workspace
        {
            Name = "Shop",
            RootDirectory = temp.Path,
            Solution = SolutionParser.Load(solution),
            Tree = new WorkspaceNode(WorkspaceNodeKind.Folder, "Shop", temp.Path),
            Projects = [new ProjectInfo { Name = "Shop", FilePath = app }],
        };

        var saved = UiTestProjects.Save(workspace, workspace.Projects[0], new UiTestRecording("Buying", Pixel, [new UiTestStep(UiTestStepKind.Tap, "Buy")]));

        Assert.True(saved.CreatedProject);
        Assert.Equal(Path.Combine(temp.Path, "Shop.UITests", "Shop.UITests.csproj"), saved.ProjectFilePath);
        var project = File.ReadAllText(saved.ProjectFilePath);
        Assert.Contains("<ProjectReference Include=\"..\\Shop\\Shop.csproj\" />", project, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Avalonia.Headless.XUnit\" Version=\"" + TabStudio.App.Services.ProjectTemplates.AvaloniaVersion + "\" />", project, StringComparison.Ordinal);
        Assert.Contains("xunit.runner.visualstudio", project, StringComparison.Ordinal);
        Assert.Contains("Shop.UITests/Shop.UITests.csproj", File.ReadAllText(solution), StringComparison.Ordinal);
        Assert.Contains("global::Shop.App", File.ReadAllText(Path.Combine(temp.Path, "Shop.UITests", "RecordedApp.cs")), StringComparison.Ordinal);
    }

    private static string WriteProject(string root, string name, string property, string items)
    {
        var folder = Path.Combine(root, name);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".csproj");
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                {property}
              </PropertyGroup>
              <ItemGroup>
                {items}
              </ItemGroup>
            </Project>
            """);
        return path;
    }
}
