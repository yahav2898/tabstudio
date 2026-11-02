using TabStudio.Live.Files;
using TabStudio.Testing;

namespace TabStudio.Live.Tests;

/// <summary>The rules every guest request is checked against: safe paths inside the shared folder, and roles.</summary>
public sealed class PathAndPermissionTests
{
    [Theory]
    [InlineData("Program.cs", "Program.cs")]
    [InlineData("src/App/MainWindow.axaml", "src/App/MainWindow.axaml")]
    [InlineData(".github/workflows/ci.yml", ".github/workflows/ci.yml")]
    [InlineData(".editorconfig", ".editorconfig")]
    [InlineData("", null)]
    [InlineData("/etc/passwd", null)]
    [InlineData("../secret.txt", null)]
    [InlineData("src/../../secret.txt", null)]
    [InlineData("src/./Program.cs", null)]
    [InlineData("src//Program.cs", null)]
    [InlineData("src\\Program.cs", null)]
    [InlineData("C:/Windows/win.ini", null)]
    [InlineData(".git/config", null)]
    [InlineData("bin/Debug/App.dll", null)]
    [InlineData("src/obj/project.assets.json", null)]
    [InlineData("node_modules/left-pad/index.js", null)]
    [InlineData("a\u0000b.cs", null)]
    public void Only_safe_paths_inside_a_workspace_are_accepted(string path, string? expected)
    {
        Assert.Equal(expected, LiveWorkspacePaths.Normalize(path));
    }

    [Fact]
    public void A_path_through_a_link_out_of_the_workspace_is_refused()
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "workspace");
        var outside = Path.Combine(temp.Path, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "secret");
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class P { }");
        Directory.CreateSymbolicLink(Path.Combine(root, "escape"), outside);

        Assert.Equal(Path.Combine(root, "Program.cs"), LiveWorkspacePaths.ToFullPath(root, "Program.cs"));
        Assert.Null(LiveWorkspacePaths.ToFullPath(root, "escape/secret.txt"));
        Assert.Null(LiveWorkspacePaths.ToRelativePath(root, Path.Combine(outside, "secret.txt")));
        Assert.Equal("Program.cs", LiveWorkspacePaths.ToRelativePath(root, Path.Combine(root, "Program.cs")));

        var files = new LiveWorkspaceFiles(root).Collect(CancellationToken.None);
        Assert.Equal(["Program.cs"], files.Select(f => f.Path));
        Assert.Null(new LiveWorkspaceFiles(root).Read("escape/secret.txt"));
    }

    [Fact]
    public void A_folder_moved_into_the_workspace_shares_its_files()
    {
        using var temp = new TempDirectory();
        temp.Write("App/Renamed/Model.cs", "class Model {}");
        temp.Write("App/Renamed/Deep/View.cs", "class View {}");
        temp.Write("App/Renamed/bin/App.dll", "binary");
        temp.Write("App/obj/Moved/x.cs", "generated");
        var files = new LiveWorkspaceFiles(temp.Path);

        Assert.Null(files.Find("App/Renamed"));
        Assert.Equal(["App/Renamed/Deep/View.cs", "App/Renamed/Model.cs"], files.FindFolder("App/Renamed").Select(f => f.Path));
        Assert.Empty(files.FindFolder("App/obj/Moved"));
        Assert.Empty(files.FindFolder("App/Renamed/Model.cs"));
    }

    [Fact]
    public void The_shared_files_leave_out_build_output_and_git()
    {
        using var temp = new TempDirectory();
        temp.Write("App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        temp.Write("App/Program.cs", "Console.WriteLine();");
        temp.Write("App/bin/Debug/App.dll", "binary");
        temp.Write("App/obj/project.assets.json", "{}");
        temp.Write(".git/HEAD", "ref: refs/heads/main");
        temp.Write(".github/workflows/ci.yml", "on: push");

        var files = new LiveWorkspaceFiles(temp.Path).Collect(CancellationToken.None);

        Assert.Equal([".github/workflows/ci.yml", "App/App.csproj", "App/Program.cs"], files.Select(f => f.Path));
        var program = files.Single(f => f.Path == "App/Program.cs");
        Assert.Equal(LiveWorkspaceFiles.HashOf("Console.WriteLine();"u8), program.Hash);
    }

    [Fact]
    public void A_guests_copy_writes_only_inside_its_folder_and_only_bytes_that_match()
    {
        using var temp = new TempDirectory();
        var mirror = new LiveMirror(Path.Combine(temp.Path, "copy"));
        var content = "class P { }"u8.ToArray();
        var entry = new LiveFileEntry("src/P.cs", content.Length, LiveWorkspaceFiles.HashOf(content));

        Assert.Equal([entry], mirror.Missing([entry]));
        Assert.True(mirror.Write(entry, content));
        Assert.Empty(mirror.Missing([entry]));
        Assert.False(mirror.Write(entry with { Path = "../escape.cs" }, content));
        Assert.False(mirror.Write(entry, "tampered"u8.ToArray()));
        Assert.False(File.Exists(Path.Combine(temp.Path, "escape.cs")));

        mirror.Delete();
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "copy")));
    }

    [Fact]
    public void Roles_decide_who_edits_steps_types_in_the_Terminal_and_manages()
    {
        var host = new LiveParticipant(1, "Teacher", "tablet", 0, LiveRole.Host);
        var editor = new LiveParticipant(2, "Dana", "tablet", 1, LiveRole.Editor);
        var viewer = new LiveParticipant(3, "Noa", "phone", 2, LiveRole.Viewer);
        var stepper = viewer with { CanStep = true, CanUseTerminal = true };

        Assert.True(LivePermissions.CanEdit(host));
        Assert.True(LivePermissions.CanEdit(editor));
        Assert.False(LivePermissions.CanEdit(viewer));
        Assert.False(LivePermissions.CanSave(viewer));
        Assert.False(LivePermissions.CanSetBreakpoints(viewer));

        Assert.True(LivePermissions.CanControlDebugger(host));
        Assert.False(LivePermissions.CanControlDebugger(editor));
        Assert.True(LivePermissions.CanControlDebugger(stepper));

        Assert.False(LivePermissions.CanUseTerminal(editor));
        Assert.True(LivePermissions.CanUseTerminal(stepper));

        Assert.True(LivePermissions.CanRun(host));
        Assert.False(LivePermissions.CanRun(editor));
        Assert.True(LivePermissions.CanManage(host));
        Assert.False(LivePermissions.CanManage(stepper));

        static bool Shared(string path) => path == "Program.cs";
        Assert.True(LivePermissions.CanRead(viewer, "Program.cs", Shared));
        Assert.False(LivePermissions.CanRead(viewer, "Secret.cs", Shared));
        Assert.False(LivePermissions.CanRead(viewer, "../Program.cs", Shared));
        Assert.False(LivePermissions.CanRead(viewer with { IsConnected = false }, "Program.cs", Shared));
    }

    [Theory]
    [InlineData("Dana's tablet", "DT")]
    [InlineData("pixel", "P")]
    [InlineData("  ", "?")]
    [InlineData("Galaxy-Tab S9", "GT")]
    public void Avatars_show_initials(string name, string initials)
    {
        Assert.Equal(initials, LiveNames.Initials(name));
    }

    [Fact]
    public void Names_are_cleaned_and_shortened()
    {
        Assert.Equal("Guest", LiveNames.Clean("\u0007\n"));
        Assert.Equal(LiveProtocol.MaxNameLength, LiveNames.Clean(new string('x', 100)).Length);
    }
}
