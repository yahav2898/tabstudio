using System.Diagnostics;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>The outcome of <see cref="SelfTest"/>.</summary>
/// <param name="Success">Whether the sample built and ran to the end.</param>
/// <param name="Message">A one-line summary: what the program printed, or what went wrong.</param>
/// <param name="Duration">How long it took, from opening the sample to the program's exit.</param>
public sealed record SelfTestResult(bool Success, string Message, TimeSpan Duration);

/// <summary>
/// Does what a person does first: opens the sample solution, presses F5, types a name when the program asks,
/// and reads the Output panel; then debugs it, serves a new web app and shows a new Avalonia app. CI runs it on an
/// Android emulator to prove the whole IDE works on a device.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The name typed when the sample asks for one.</summary>
    public const string Name = "Tablet";

    /// <summary>Runs the self test in the running app.</summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="cancellationToken">Gives up, for example after a timeout.</param>
    public static Task<SelfTestResult> RunAsync(MainViewModel main, SampleLibrary samples, CancellationToken cancellationToken = default) =>
        RunAsync(main, samples, templates: null, cancellationToken);

    /// <summary>Runs the self test in the running app, then a web app and an Avalonia app when <paramref name="templates"/> is given.</summary>
    /// <param name="main">The app's root view model.</param>
    /// <param name="samples">The sample library.</param>
    /// <param name="templates">Creates the web and Avalonia apps; without it, those parts are skipped.</param>
    /// <param name="cancellationToken">Gives up, for example after a timeout.</param>
    public static async Task<SelfTestResult> RunAsync(MainViewModel main, SampleLibrary samples, ProjectTemplates? templates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(samples);
        var clock = Stopwatch.StartNew();
        try
        {
            var solution = await samples.EnsureExtractedAsync(cancellationToken);
            await main.OpenWorkspaceAsync(solution);
            if (main.Workspace is not { } workspace)
            {
                return new SelfTestResult(false, "The sample did not open.", clock.Elapsed);
            }

            if (!workspace.RunCommand.CanExecute(null))
            {
                return new SelfTestResult(false, $"F5 is not available: {workspace.StatusText}.", clock.Elapsed);
            }

            void Answer(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
                {
                    workspace.Output.InputText = Name;
                    workspace.Output.SubmitInputCommand.Execute(null);
                }
            }

            workspace.Output.PropertyChanged += Answer;
            try
            {
                await workspace.RunCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            }
            finally
            {
                workspace.Output.PropertyChanged -= Answer;
            }

            var output = workspace.Output.Text;
            var exited = output.Contains("has exited with code 0", StringComparison.Ordinal);
            var greeted = output.Contains($", {Name}!", StringComparison.Ordinal);
            var summary = string.Join(" | ", output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(6));
            if (!exited || !greeted)
            {
                return new SelfTestResult(false, summary, clock.Elapsed);
            }

            var debugged = await DebugSampleAsync(workspace, cancellationToken);
            if (!debugged.Success || templates is null)
            {
                return new SelfTestResult(debugged.Success, $"{summary} | {debugged.Message}", clock.Elapsed);
            }

            var web = await ServeWebAppAsync(main, templates, cancellationToken);
            if (!web.Success)
            {
                return new SelfTestResult(false, $"{summary} | {debugged.Message} | {web.Message}", clock.Elapsed);
            }

            var avalonia = await ShowAvaloniaAppAsync(main, templates, cancellationToken);
            if (!avalonia.Success || main.Workspace is not { } last)
            {
                return new SelfTestResult(false, $"{summary} | {debugged.Message} | {web.Message} | {avalonia.Message}", clock.Elapsed);
            }

            var extensions = await InstallGrammarExtensionAsync(last, FindEditor, cancellationToken);
            return new SelfTestResult(extensions.Success, $"{summary} | {debugged.Message} | {web.Message} | {avalonia.Message} | {extensions.Message}", clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SelfTestResult(false, $"{ex.GetType().Name}: {ex.Message}".ReplaceLineEndings(" "), clock.Elapsed);
        }
    }
}
