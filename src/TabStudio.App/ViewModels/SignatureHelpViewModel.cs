using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Compilation.Features;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Parameter Info: the signature of the method being called while its arguments are typed, with the current
/// parameter in bold and its documentation, and arrows to go through the overloads, as in Visual Studio.
/// </summary>
public sealed partial class SignatureHelpViewModel : ObservableObject
{
    private SignatureHelpResult? _result;
    private bool _overloadChosen;

    /// <summary>Whether Parameter Info is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The overload shown, zero-based.</summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; private set; }

    /// <summary>The signature of the overload, with the current parameter in bold.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StyledRun> Signature { get; private set; } = [];

    /// <summary>"2 of 3" when there are overloads.</summary>
    [ObservableProperty]
    public partial string Counter { get; private set; } = string.Empty;

    /// <summary>Whether the method has more than one overload.</summary>
    [ObservableProperty]
    public partial bool HasOverloads { get; private set; }

    /// <summary>The summary of the method.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocumentation))]
    public partial string Documentation { get; private set; } = string.Empty;

    /// <summary>The name of the current parameter.</summary>
    [ObservableProperty]
    public partial string ParameterName { get; private set; } = string.Empty;

    /// <summary>The documentation of the current parameter.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParameterDocumentation))]
    public partial string ParameterDocumentation { get; private set; } = string.Empty;

    /// <summary>Whether the method has a summary.</summary>
    public bool HasDocumentation => Documentation.Length > 0;

    /// <summary>Whether the current parameter has documentation.</summary>
    public bool HasParameterDocumentation => ParameterDocumentation.Length > 0;

    /// <summary>Where the argument list starts, just after its opening parenthesis. The popup is placed there.</summary>
    public int ApplicableSpanStart => _result?.ApplicableSpanStart ?? 0;

    /// <summary>The overloads and argument from the language service, while open.</summary>
    public SignatureHelpResult? Result => _result;

    /// <summary>Shows new results. The overload the user picked with the arrows stays selected while it still applies.</summary>
    /// <param name="result">Signature help from the language service.</param>
    public void Show(SignatureHelpResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var sameCall = _result is { } previous
            && previous.ApplicableSpanStart == result.ApplicableSpanStart
            && previous.Signatures.Count == result.Signatures.Count;
        _result = result;
        if (!sameCall || !_overloadChosen)
        {
            _overloadChosen = false;
            SelectedIndex = Math.Clamp(result.ActiveSignature, 0, result.Signatures.Count - 1);
        }

        Update();
        IsOpen = true;
    }

    /// <summary>Hides Parameter Info.</summary>
    public void Close()
    {
        _result = null;
        _overloadChosen = false;
        IsOpen = false;
    }

    /// <summary>Shows the next overload (Down arrow), wrapping around.</summary>
    [RelayCommand]
    public void NextOverload() => MoveOverload(1);

    /// <summary>Shows the previous overload (Up arrow), wrapping around.</summary>
    [RelayCommand]
    public void PreviousOverload() => MoveOverload(-1);

    private void MoveOverload(int step)
    {
        if (_result is not { Signatures.Count: > 1 } result)
        {
            return;
        }

        _overloadChosen = true;
        SelectedIndex = (SelectedIndex + step + result.Signatures.Count) % result.Signatures.Count;
        Update();
    }

    private void Update()
    {
        if (_result is not { } result || result.Signatures.Count == 0)
        {
            return;
        }

        var signature = result.Signatures[SelectedIndex];
        var active = signature.ActiveParameter;
        var runs = new List<StyledRun>(StyledRun.From(signature.Prefix));
        for (var i = 0; i < signature.Parameters.Count; i++)
        {
            if (i > 0)
            {
                runs.Add(new StyledRun(", ", "punctuation"));
            }

            runs.AddRange(StyledRun.From(signature.Parameters[i].Display, bold: i == active));
        }

        runs.AddRange(StyledRun.From(signature.Suffix));
        Signature = runs;
        HasOverloads = result.Signatures.Count > 1;
        Counter = string.Create(CultureInfo.InvariantCulture, $"{SelectedIndex + 1} of {result.Signatures.Count}");
        Documentation = signature.Documentation.Trim();
        var parameter = active >= 0 && active < signature.Parameters.Count ? signature.Parameters[active] : null;
        ParameterName = parameter?.Name ?? string.Empty;
        ParameterDocumentation = parameter?.Documentation.Trim() ?? string.Empty;
    }
}
