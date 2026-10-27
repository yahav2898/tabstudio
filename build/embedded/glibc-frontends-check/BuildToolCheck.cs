using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

internal static class BuildToolCheck
{
    public static async Task VerifyAsync(IToolchainRuntime runtime, string projectDirectory, string outputDirectory,
        CancellationToken cancellationToken, string? nativeSearchPath = null)
    {
        var project = Path.Combine(projectDirectory, "native-build-tool-check");
        var build = Path.Combine(project, "build");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(project, "CMakeLists.txt"), """
            cmake_minimum_required(VERSION 3.20)
            project(tabstudio_build_tool_check C)
            add_executable(tabstudio_build_tool_check main.c)
            """ + "\n", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(project, "main.c"), "#include <stdio.h>\nint main(void) { puts(\"NATIVE_BUILD_TOOL_PROGRAM_PASSED\"); return 0; }\n", cancellationToken);
        var environment = new Dictionary<string, string?>
        {
            [EmbeddedSdkBootstrap.JobsVariable] = "2",
            ["PATH"] = nativeSearchPath ?? EmbeddedSdkBootstrap.BuildToolsDirectory + ":/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
        };
        if (nativeSearchPath is null)
        {
            environment[EmbeddedGlibcRuntime.RoutingMarker] = "1";
        }

        async Task<CommandResult> RunAsync(string program, IReadOnlyList<string> arguments, string log)
        {
            var command = new ProcessStartSpec { Program = program, Arguments = arguments, WorkingDirectory = project, Environment = environment };
            Console.WriteLine("+ " + command.CommandLine);
            var result = await CommandRunner.RunAsync(runtime, command, Console.WriteLine, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, log), result.Output, cancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("The real native build-tool regression failed: " + command.CommandLine + "\n" + result.Tail(15));
            }
            return result;
        }

        _ = await RunAsync("cmake", ["-S", project, "-B", build, "-G", "Ninja"], "native-tools-configure.log");
        // CMake invokes this tool mode during regeneration. Ninja global flags must precede -t;
        // appending -j2 makes restat interpret -j as an invalid tool-specific option.
        _ = await RunAsync("ninja", ["-C", build, "-t", "restat", "build.ninja"], "native-tools-restat.log");
        _ = await RunAsync("cmake", ["--build", build, "--parallel", "2"], "native-tools-build.log");
        var result = await RunAsync(Path.Combine(build, "tabstudio_build_tool_check"), [], "native-tools-execute.log");
        if (!result.Output.Contains("NATIVE_BUILD_TOOL_PROGRAM_PASSED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The native build tool fixture did not execute its compiled output.");
        }
        Console.WriteLine("EMBEDDED_NATIVE_BUILD_TOOLS_PASSED");
    }
}
