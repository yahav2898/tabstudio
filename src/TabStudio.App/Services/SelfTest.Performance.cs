using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.App.ViewModels;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    private const int PerformanceWarmTrials = 5;

    /// <summary>Measures real editor operations without running the functional shards or installing toolchains.</summary>
    private static async Task<SelfTestResult> RunPerformanceAsync(MainViewModel main, SampleLibrary samples, string? outputDirectory, CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        var report = new SelfTestPerformanceReport
        {
            Framework = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            InformationalVersion = typeof(SelfTest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            InterpreterOptions = Environment.GetEnvironmentVariable("MONO_INTERPRETER_OPTIONS"),
            ModifiableAssemblies = Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES"),
            MetadataUpdatesSupported = System.Reflection.Metadata.MetadataUpdater.IsSupported,
            DynamicCodeSupported = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported,
            DynamicCodeCompiled = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled,
            ProcessorCount = Environment.ProcessorCount,
        };

        void Save()
        {
            if (outputDirectory is null) return;
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, "runtime-performance.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(report, SelfTestPerformanceJson.Default.SelfTestPerformanceReport));
            File.Move(path + ".tmp", path, overwrite: true);
        }

        void Phase(string phase)
        {
            report.Phase = phase;
            Save();
        }

        try
        {
            Phase("extract_sample");
            var solution = await samples.EnsureExtractedAsync(cancellationToken);
            Phase("open_workspace");
            var loading = Stopwatch.StartNew();
            await main.OpenWorkspaceAsync(solution).WaitAsync(cancellationToken);
            report.WorkspaceOpenMilliseconds = loading.Elapsed.TotalMilliseconds;
            var workspace = main.Workspace ?? throw new InvalidOperationException("The sample workspace did not open.");
            var program = workspace.Documents.FirstOrDefault(d => d.FileName == "Program.cs")
                ?? throw new InvalidOperationException("Program.cs did not open.");

            // The first trial includes the first Debug build; the following five use the same warmed workspace.
            for (var trial = 0; trial <= PerformanceWarmTrials; trial++)
            {
                Phase($"f5_{trial}");
                var prompt = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
                var run = Stopwatch.StartNew();
                void Answer(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
                {
                    if (args.PropertyName != nameof(OutputViewModel.IsWaitingForInput) || !workspace.Output.IsWaitingForInput) return;
                    prompt.TrySetResult(run.Elapsed.TotalMilliseconds);
                    workspace.Output.InputText = Name;
                    workspace.Output.SubmitInputCommand.Execute(null);
                }

                workspace.Output.PropertyChanged += Answer;
                try
                {
                    if (!workspace.StartDebuggingCommand.CanExecute(null))
                        throw new InvalidOperationException($"F5 is unavailable: {workspace.StatusText}.");
                    await workspace.StartDebuggingCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
                    var promptMilliseconds = await prompt.Task.WaitAsync(cancellationToken);
                    await WaitAsync(() => !workspace.IsDebugging, cancellationToken);
                    if (!workspace.Output.Text.Contains("What's your name?", StringComparison.Ordinal)
                        || !workspace.Output.Text.Contains($", {Name}!", StringComparison.Ordinal)
                        || !workspace.Output.Text.Contains("has exited with code 0", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("F5 did not print the expected greeting and exit successfully: " + LastLines(workspace));
                    }

                    report.F5PromptMilliseconds.Add(promptMilliseconds);
                    report.F5ExitMilliseconds.Add(run.Elapsed.TotalMilliseconds);
                    Save();
                }
                finally
                {
                    workspace.Output.PropertyChanged -= Answer;
                    if (workspace.IsDebugging) workspace.StopDebuggingCommand.Execute(null);
                }
            }

            Phase("find_editor");
            var offset = program.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal);
            if (offset < 0) throw new InvalidOperationException("The sample's greeter.Greet() completion location was not found.");
            offset += "greeter.".Length;
            workspace.ActiveDocument = program;
            await WaitAsync(() => FindEditor(program)?.Completion is not null, cancellationToken);
            var editor = FindEditor(program)!;
            editor.Editor.CaretOffset = offset;
            program.SyncText();

            for (var trial = 0; trial <= PerformanceWarmTrials; trial++)
            {
                Phase($"completion_{trial}");
                editor.Completion!.Close();
                editor.Editor.CaretOffset = offset;
                var completion = Stopwatch.StartNew();
                var opened = await editor.Completion.RequestAsync(CompletionTrigger.Invoke).WaitAsync(cancellationToken);
                if (!opened || !editor.Completion.Items.Any(item => item.Entry.DisplayText == "Greet"))
                    throw new InvalidOperationException("C# completion did not offer Greet.");
                await WaitAsync(() => editor.IsCompletionShown, cancellationToken);
                report.CompletionPopupMilliseconds.Add(completion.Elapsed.TotalMilliseconds);
                Save();
            }

            editor.Completion!.Close();
            report.Success = true;
            Phase("complete");
            return new SelfTestResult(true, "Performance: six real F5 runs and six C# completion popups verified; runtime-performance.json contains first and five warm samples.", total.Elapsed);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            report.Error = $"{exception.GetType().Name}: {exception.Message}{DescribeActivity(main)}".ReplaceLineEndings(" ");
            Save();
            return new SelfTestResult(false, $"Performance failed during {report.Phase}: {report.Error}", total.Elapsed);
        }
    }
}

/// <summary>Raw measurements; first samples precede the five warm samples and are never included in their median.</summary>
internal sealed class SelfTestPerformanceReport
{
    public bool Success { get; set; }
    public string Phase { get; set; } = "starting";
    public string? Error { get; set; }
    public string Framework { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string InformationalVersion { get; set; } = "";
    public string? InterpreterOptions { get; set; }
    public string? ModifiableAssemblies { get; set; }
    public bool MetadataUpdatesSupported { get; set; }
    public bool DynamicCodeSupported { get; set; }
    public bool DynamicCodeCompiled { get; set; }
    public int ProcessorCount { get; set; }
    public double? WorkspaceOpenMilliseconds { get; set; }
    public List<double> F5PromptMilliseconds { get; set; } = [];
    public List<double> F5ExitMilliseconds { get; set; } = [];
    public List<double> CompletionPopupMilliseconds { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SelfTestPerformanceReport))]
internal sealed partial class SelfTestPerformanceJson : JsonSerializerContext
{
}
