using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Workspaces;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Embedded.Testing;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Runs configured host-side Unity, TAP, GoogleTest or Cargo tests and shows every result in Test Explorer.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunEmbeddedTestsAsync()
    {
        try
        {
            if (await SelectEmbeddedProjectAsync() is not { } project || !await SaveAllCoreAsync())
            {
                return;
            }
            var path = Path.Combine(project.Directory, EmbeddedHostTestPlanner.OptionsFile);
            EmbeddedHostTestOptions options;
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 65536)
                {
                    throw new InvalidDataException("Embedded test configuration exceeds 64 KB.");
                }
                options = JsonSerializer.Deserialize<EmbeddedHostTestOptions>(await File.ReadAllTextAsync(path), EmbeddedJson)
                    ?? throw new InvalidDataException("Embedded test configuration is empty.");
            }
            else if (project.Kind == EmbeddedProjectKind.Rust)
            {
                options = new EmbeddedHostTestOptions { Format = EmbeddedTestFormat.Rust };
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new EmbeddedHostTestOptions(), EmbeddedJson) + "\n");
                await OpenDocumentAsync(path);
                Output.WriteLine("Set Format to Unity, Tap or GoogleTest and Executable to the already built HOST test binary. Arguments is an array, never a shell command. Use Import Embedded Test Results for actual on-target logs.", OutputKind.Info);
                SelectedPanel = BottomPanel.Output;
                IsBottomPanelVisible = true;
                return;
            }
            if (options.Format == EmbeddedTestFormat.Rust && _toolchains.Catalog.Find("rust") is { } toolchain
                && !await EnsureToolchainReadyAsync(toolchain, "Run the project's native host-side tests."))
            {
                return;
            }
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            _testRun = cancellation;
            State = IdeState.Running;
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            StatusText = "Running embedded host tests";
            try
            {
                await _toolchains.Manager.PrepareRuntimeAsync(cancellationToken: cancellation.Token);
                string? hostTarget = null;
                if (options.Format == EmbeddedTestFormat.Rust && options.HostTarget is null)
                {
                    var version = await OutputProcess.RunAsync(_toolchains.Runtime,
                        new ProcessStartSpec { Program = "rustc", Arguments = ["-vV"], WorkingDirectory = project.Directory },
                        Output, null, cancellation.Token);
                    if (!version.Succeeded)
                    {
                        throw new IOException("Could not query the installed Rust compiler's host target.");
                    }
                    hostTarget = EmbeddedHostTestPlanner.RustHostTarget(string.Join('\n', version.Lines));
                }
                var reportDirectory = Path.Combine(project.Directory, ".tabstudio", "embedded", "test-results");
                Directory.CreateDirectory(reportDirectory);
                var reportPath = Path.Combine(reportDirectory, Guid.NewGuid().ToString("N") + ".xml");
                var spec = EmbeddedHostTestPlanner.Plan(project.Directory, options, hostTarget, reportPath);
                Output.WriteLine("> " + spec.CommandLine, OutputKind.Info);
                var parsed = await CaptureEmbeddedTestsAsync(spec, options, reportPath, cancellation.Token);
                ShowEmbeddedTestResults(project, parsed);
                State = parsed.Any(r => r.Outcome is EmbeddedTestOutcome.Failed or EmbeddedTestOutcome.Errored) ? IdeState.Failed : IdeState.Ready;
                StatusText = $"Embedded tests: {parsed.Count(r => r.Outcome == EmbeddedTestOutcome.Passed)} passed, {parsed.Count(r => r.Outcome is EmbeddedTestOutcome.Failed or EmbeddedTestOutcome.Errored)} failed";
            }
            finally
            {
                _run = null;
                _testRun = null;
                if (State == IdeState.Running)
                {
                    State = IdeState.Ready;
                }
            }
        }
        catch (OperationCanceledException)
        {
            State = IdeState.Ready;
            StatusText = "Embedded tests stopped";
        }
        catch (Exception error) when (IsEmbeddedError(error))
        {
            EmbeddedFailure(error);
        }
    }

    /// <summary>Imports an actual host or decoded board test log, retaining the explicit board identity.</summary>
    [RelayCommand]
    private async Task ImportEmbeddedTestResultsAsync()
    {
        try
        {
            if (await SelectEmbeddedProjectAsync() is not { } project)
            {
                return;
            }
            var path = await Prompt.AskAsync("Import embedded test results", "Path to an actual decoded Unity, TAP, defmt-test, Rust log or GoogleTest XML report:", "Read", ActiveDocument?.FilePath ?? "");
            if (path is null)
            {
                return;
            }
            path = Path.GetFullPath(path, project.Directory);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 16 * 1024 * 1024 || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Choose a regular test report smaller than 16 MB.");
            }
            var formats = Enum.GetValues<EmbeddedTestFormat>();
            var selected = await Prompt.ChooseAsync("Test result format", "Choose the harness that produced this report.", "Next", formats.Select(f => f.ToString()).ToArray());
            if (selected is null)
            {
                return;
            }
            var board = await Prompt.AskAsync("Test board identity", "Enter the actual board serial/identity, or host for host-side tests:", "Import");
            if (string.IsNullOrWhiteSpace(board))
            {
                return;
            }
            var text = await File.ReadAllTextAsync(path);
            var format = formats[selected.Value];
            IReadOnlyList<EmbeddedTestResult> parsed;
            if (format == EmbeddedTestFormat.GoogleTest)
            {
                parsed = EmbeddedTestXml.ReadGoogleTest(text, board);
            }
            else
            {
                var parser = new EmbeddedTestOutputParser(board, format);
                using var input = new StringReader(text);
                while (input.ReadLine() is { } line)
                {
                    parser.Feed(line);
                }
                parsed = parser.Complete();
            }
            ShowEmbeddedTestResults(project, parsed);
            StatusText = $"Imported {parsed.Count} test results for {board}";
        }
        catch (Exception error) when (IsEmbeddedError(error))
        {
            EmbeddedFailure(error);
        }
    }

    private void ShowEmbeddedTestResults(EmbeddedProject project, IReadOnlyList<EmbeddedTestResult> results)
    {
        Tests.ReportEmbeddedResults(new ProjectInfo
        {
            Name = project.Name, FilePath = project.MarkerPath, Kind = ProjectKind.EmbeddedFirmware,
        }, results);
        SelectSideBarPanel(SideBarPanel.TestExplorer);
    }

    private async Task<IReadOnlyList<EmbeddedTestResult>> CaptureEmbeddedTestsAsync(ProcessStartSpec spec,
        EmbeddedHostTestOptions options, string reportPath, CancellationToken cancellationToken)
    {
        var parser = options.Format == EmbeddedTestFormat.GoogleTest ? null : new EmbeddedTestOutputParser(options.HostId, options.Format);
        using var process = _toolchains.Runtime.Start(spec);
        process.CloseInput();
        using var stopped = cancellationToken.Register(process.Stop);
        using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var gate = new Lock();
        var total = 0;
        async Task PumpAsync(Stream source, OutputKind outputKind)
        {
            using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var buffer = new char[4096];
            var partial = new StringBuilder();
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), drain.Token)) > 0)
            {
                var text = new string(buffer, 0, read);
                Output.Write(text, outputKind);
                lock (gate)
                {
                    total += read;
                    if (total > 16 * 1024 * 1024)
                    {
                        process.Stop();
                        throw new InvalidDataException("Embedded test output exceeded 16 MB.");
                    }
                    foreach (var character in text)
                    {
                        if (character == '\n')
                        {
                            parser?.Feed(partial.ToString().TrimEnd('\r'));
                            partial.Clear();
                        }
                        else if (partial.Length < 65536)
                        {
                            partial.Append(character);
                        }
                        else
                        {
                            process.Stop();
                            throw new InvalidDataException("Embedded test output line exceeded 64 KB.");
                        }
                    }
                }
            }
            lock (gate)
            {
                if (partial.Length > 0)
                {
                    parser?.Feed(partial.ToString());
                }
            }
        }
        var pumps = Task.WhenAll(PumpAsync(process.Output, OutputKind.Program),
            process.Error is { } error ? PumpAsync(error, OutputKind.Error) : Task.CompletedTask);
        _ = pumps.ContinueWith(_ => process.Stop(), CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            var exit = await process.Completion;
            var drained = await Task.WhenAny(pumps, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken)) == pumps;
            if (!drained)
            {
                drain.Cancel();
            }
            try
            {
                await pumps;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A child retained its output handle after the test command ended; the run cannot be green.
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (parser is not null)
            {
                return parser.Complete(exit.Succeeded && drained);
            }
            if (!File.Exists(reportPath) || new FileInfo(reportPath).Length > 16 * 1024 * 1024)
            {
                throw new IOException("GoogleTest did not write a valid fresh XML report.");
            }
            var results = EmbeddedTestXml.ReadGoogleTest(await File.ReadAllTextAsync(reportPath, cancellationToken), options.HostId).ToList();
            if ((!exit.Succeeded || !drained) && !results.Any(r => r.Outcome is EmbeddedTestOutcome.Failed or EmbeddedTestOutcome.Errored))
            {
                results.Add(new(options.HostId, options.Format, "[transport]", EmbeddedTestOutcome.Errored, "The GoogleTest host process did not complete successfully."));
            }
            return results;
        }
        finally
        {
            drain.Cancel();
            process.Stop();
        }
    }
}
