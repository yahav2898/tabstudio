using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl.GitHub;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Generates an iOS cloud workflow and its setup guide in the current repository.</summary>
    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildForIosAsync()
    {
        if (Workspace?.RootDirectory is not { } workspaceRoot)
        {
            return;
        }

        var root = workspaceRoot;
        for (var parent = new DirectoryInfo(root); parent is not null; parent = parent.Parent)
        {
            if (Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git")))
            {
                root = parent.FullName;
                break;
            }
        }

        var projectRoot = MobileRoot() ?? workspaceRoot;
        var capacitor = GradleAndroidProject.IsCapacitor(projectRoot);
        var project = capacitor ? "ios/App/App.xcworkspace" : FindXcodeProject(projectRoot);
        if (project is null)
        {
            StatusText = "Build for iOS needs a Capacitor app or an Xcode project";
            return;
        }

        var options = new IosWorkflowOptions(capacitor, Path.GetRelativePath(root, projectRoot).Replace('\\', '/'), project,
            capacitor ? "App" : Path.GetFileNameWithoutExtension(project));
        var workflow = Path.Combine(root, IosWorkflowGenerator.WorkflowPath);
        if (File.Exists(workflow) && !await Prompt.ConfirmAsync("Replace iOS workflow?", "Replaces the generated workflow. Commit any edits you want to keep first.", "Replace", false))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(workflow)!);
        await File.WriteAllTextAsync(workflow, IosWorkflowGenerator.Generate(options));
        var guide = Path.Combine(root, "docs", "IOS-BUILD.md");
        Directory.CreateDirectory(Path.GetDirectoryName(guide)!);
        if (!File.Exists(guide))
        {
            await File.WriteAllTextAsync(guide, IosWorkflowGenerator.SetupGuide());
        }

        await OpenDocumentAsync(guide);
        await OpenDocumentAsync(workflow);
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        Output.WriteLine("Build for iOS: no local Mac needed. An Apple Developer account is required.", OutputKind.Info);
        foreach (var secret in IosWorkflowGenerator.Secrets)
        {
            Output.WriteLine($"{secret.Name}: {secret.Explanation}", OutputKind.Info);
        }

        Output.WriteLine("Review, commit and push the workflow. Then choose Build > Run iOS Workflow; Actions shows progress, logs and the IPA artifact.", OutputKind.Info);
        StatusText = "iOS workflow generated — see docs/IOS-BUILD.md for signing setup";
    }

    /// <summary>Opens the existing Actions dispatch form for the generated workflow.</summary>
    [RelayCommand]
    private Task RunIosWorkflowAsync() => Actions.OpenWorkflowAsync(IosWorkflowGenerator.WorkflowPath);

    private static string? FindXcodeProject(string root)
    {
        var queue = new Queue<(string Directory, int Depth)>();
        queue.Enqueue((root, 0));
        var found = new List<string>();
        while (queue.Count > 0 && found.Count < 20)
        {
            var (folder, depth) = queue.Dequeue();
            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                var name = Path.GetFileName(child);
                if (name.EndsWith(".xcworkspace", StringComparison.Ordinal) || name.EndsWith(".xcodeproj", StringComparison.Ordinal))
                {
                    found.Add(child);
                }
                else if (depth < 3 && !name.StartsWith('.') && name is not ("node_modules" or "Pods" or "build" or "dist"))
                {
                    queue.Enqueue((child, depth + 1));
                }
            }
        }

        var selected = found.OrderBy(p => p.EndsWith(".xcworkspace", StringComparison.Ordinal) ? 0 : 1).ThenBy(p => p.Length).FirstOrDefault();
        return selected is null ? null : Path.GetRelativePath(root, selected).Replace('\\', '/');
    }
}
