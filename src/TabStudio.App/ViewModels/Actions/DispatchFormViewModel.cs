using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>One input of the "Run workflow" form, with the control its type calls for.</summary>
public sealed partial class DispatchInputViewModel : ObservableObject
{
    /// <summary>Creates the input with its default value.</summary>
    /// <param name="input">The input as the workflow file declares it.</param>
    public DispatchInputViewModel(WorkflowDispatchInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Input = input;
        Text = input.Type is WorkflowInputType.Boolean or WorkflowInputType.Choice ? string.Empty : input.Default ?? string.Empty;
        IsOn = input.Type == WorkflowInputType.Boolean && string.Equals(input.Default, "true", StringComparison.OrdinalIgnoreCase);
        SelectedOption = input.Type != WorkflowInputType.Choice ? null
            : input.Default is { } preferred && input.Options.Contains(preferred) ? preferred
            : input.Options.Count > 0 ? input.Options[0]
            : null;
    }

    /// <summary>The input as the workflow file declares it.</summary>
    public WorkflowDispatchInput Input { get; }

    /// <summary>Its name, as the workflow reads it.</summary>
    public string Name => Input.Name;

    /// <summary>
    /// Its name as a label, as GitHub's own form does not but a person reads it: <c>dry-run</c> is "Dry run",
    /// <c>node_version</c> "Node version" and <c>releaseChannel</c> "Release channel". The name is its tooltip.
    /// </summary>
    public string Label => ToLabel(Input.Name);

    /// <summary>What it is for, if the file says.</summary>
    public string? Description => Input.Description;

    /// <summary>A workflow input's name as words: separators become spaces, camel case splits, the first letter is capital.</summary>
    /// <param name="name">The name, such as <c>dry-run</c>.</param>
    public static string ToLabel(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var words = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c is '-' or '_' or '.')
            {
                if (words.Length > 0 && words[^1] != ' ')
                {
                    words.Append(' ');
                }

                continue;
            }

            if (char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1]) && words.Length > 0 && words[^1] != ' ')
            {
                words.Append(' ');
            }

            words.Append(words.Length == 0 ? char.ToUpperInvariant(c) : char.IsUpper(c) && i + 1 < name.Length && char.IsLower(name[i + 1]) ? char.ToLowerInvariant(c) : c);
        }

        var label = words.ToString().Trim();
        return label.Length == 0 ? name : label;
    }

    /// <summary>Whether it has a description to show.</summary>
    public bool HasDescription => Input.Description is not null;

    /// <summary>Whether a value must be given.</summary>
    public bool IsRequired => Input.IsRequired;

    /// <summary>The mark after a required input's name.</summary>
    public string RequiredMark => IsRequired ? " *" : string.Empty;

    /// <summary>Whether it is typed in: text, a number or an environment's name.</summary>
    public bool IsText => Input.Type is WorkflowInputType.Text or WorkflowInputType.Number or WorkflowInputType.Environment;

    /// <summary>Whether it is a toggle.</summary>
    public bool IsBoolean => Input.Type == WorkflowInputType.Boolean;

    /// <summary>Whether it is a list of choices.</summary>
    public bool IsChoice => Input.Type == WorkflowInputType.Choice;

    /// <summary>The choices of a choice input.</summary>
    public IReadOnlyList<string> Options => Input.Options;

    /// <summary>The hint in an empty text box.</summary>
    public string Watermark => Input.Type switch
    {
        WorkflowInputType.Number => "A number",
        WorkflowInputType.Environment => "The name of a deployment environment",
        _ => IsRequired ? "Required" : "Optional",
    };

    /// <summary>The value of a typed-in input.</summary>
    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>The value of a toggle.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    /// <summary>The chosen option of a choice input.</summary>
    [ObservableProperty]
    public partial string? SelectedOption { get; set; }

    /// <summary>What is wrong with the value, after <see cref="Validate"/>.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>The value to send, or <see langword="null"/> to leave the input to its default.</summary>
    public string? Value => Input.Type switch
    {
        WorkflowInputType.Boolean => IsOn ? "true" : "false",
        WorkflowInputType.Choice => SelectedOption,
        _ => string.IsNullOrWhiteSpace(Text) ? null : Text.Trim(),
    };

    /// <summary>Checks the value: present when required, a number for a number input.</summary>
    /// <returns>Whether the value can be sent.</returns>
    public bool Validate()
    {
        ErrorText = IsRequired && Value is null ? $"{Name} is required."
            : Input.Type == WorkflowInputType.Number && Value is { } number && !double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? $"{Name} must be a number, such as 3 or 2.5."
            : null;
        return ErrorText is null;
    }

    partial void OnTextChanged(string value) => ErrorText = null;

    partial void OnSelectedOptionChanged(string? value) => ErrorText = null;
}

/// <summary>
/// The "Run workflow" form: the branch to run on, and a control for each of the workflow's dispatch inputs, as
/// on GitHub.
/// </summary>
public sealed partial class DispatchFormViewModel : ObservableObject
{
    private readonly Func<DispatchFormViewModel, Task> _run;
    private string? _definitionBranch;
    private bool _listingBranches;
    private bool _canDispatch = true;

    /// <summary>Creates the form.</summary>
    /// <param name="owner">The repository's owner.</param>
    /// <param name="repository">The repository's name.</param>
    /// <param name="workflow">The workflow.</param>
    /// <param name="definition">What its file says about running it by hand.</param>
    /// <param name="branch">The branch it starts on: the workspace's, or the default branch.</param>
    /// <param name="run">Sends the dispatch; the form shows what goes wrong.</param>
    public DispatchFormViewModel(string owner, string repository, GitHubWorkflow workflow, WorkflowDefinition definition, string branch, Func<DispatchFormViewModel, Task> run)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(definition);
        Owner = owner;
        Repository = repository;
        Workflow = workflow;
        Inputs = [.. definition.Inputs.Select(i => new DispatchInputViewModel(i))];
        _definitionBranch = branch;
        Branches.Add(branch);
        Branch = branch;
        _run = run;
    }

    /// <summary>Raised when the user picks another branch, whose version of the workflow may have other inputs.</summary>
    public event EventHandler<string>? BranchChanged;

    /// <summary>The repository's owner.</summary>
    public string Owner { get; }

    /// <summary>The repository's name.</summary>
    public string Repository { get; }

    /// <summary>The workflow.</summary>
    public GitHubWorkflow Workflow { get; }

    /// <summary>"Run CI".</summary>
    public string Title => $"Run {Workflow.Name}";

    /// <summary>"octocat/hello · .github/workflows/ci.yml".</summary>
    public string Subtitle => $"{Owner}/{Repository} · {Workflow.Path}";

    /// <summary>The branches the workflow can run on.</summary>
    public ObservableCollection<string> Branches { get; } = [];

    /// <summary>The branch to run on.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial string? Branch { get; set; }

    /// <summary>The workflow's inputs, as the chosen branch's version of its file declares them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInputs))]
    public partial IReadOnlyList<DispatchInputViewModel> Inputs { get; private set; }

    /// <summary>Whether the workflow has inputs.</summary>
    public bool HasInputs => Inputs.Count > 0;

    /// <summary>Whether the dispatch is being sent.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>Why the run could not be started.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    /// <summary>The values to send, by input name; inputs left empty take their defaults.</summary>
    public IReadOnlyDictionary<string, string> Values =>
        Inputs.Where(i => i.Value is not null).ToDictionary(i => i.Name, i => i.Value!, StringComparer.Ordinal);

    /// <summary>Shows the repository's branches, keeping the chosen one first.</summary>
    /// <param name="branches">The branch names.</param>
    public void ShowBranches(IEnumerable<string> branches)
    {
        ArgumentNullException.ThrowIfNull(branches);
        var chosen = Branch;
        _listingBranches = true;
        foreach (var name in branches.Where(b => !Branches.Contains(b)).Order(StringComparer.OrdinalIgnoreCase))
        {
            Branches.Add(name);
        }

        Branch = chosen;
        _listingBranches = false;
    }

    /// <summary>Shows the inputs of a branch's version of the workflow, keeping the values of inputs both versions have.</summary>
    /// <param name="definition">What that version's file says.</param>
    /// <param name="branch">The branch it was read from.</param>
    public void ShowDefinition(WorkflowDefinition definition, string branch)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (Branch != branch)
        {
            return;
        }

        _definitionBranch = branch;
        var previous = Inputs.ToDictionary(i => i.Name, StringComparer.Ordinal);
        Inputs = [.. definition.Inputs.Select(i => previous.TryGetValue(i.Name, out var kept) && kept.Input.Type == i.Type && kept.Options.SequenceEqual(i.Options) ? kept : new DispatchInputViewModel(i))];
        _canDispatch = definition.CanDispatch;
        ErrorText = definition.CanDispatch ? null : $"{branch}'s version of {Workflow.Name} has no workflow_dispatch trigger, so it cannot be run there by hand.";
        RunCommand.NotifyCanExecuteChanged();
    }

    partial void OnBranchChanged(string? value)
    {
        if (!_listingBranches && value is not null && value != _definitionBranch)
        {
            BranchChanged?.Invoke(this, value);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        ErrorText = null;
        var valid = true;
        foreach (var input in Inputs)
        {
            valid &= input.Validate();
        }

        if (valid)
        {
            await _run(this);
        }
    }

    private bool CanRun() => !IsBusy && _canDispatch && !string.IsNullOrWhiteSpace(Branch);
}
