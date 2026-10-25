using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace TabStudio.App.Controls;

/// <summary>
/// What a panel shows when it could not do its work: what went wrong, plainly and without apology, how to fix it, the
/// technical details for those who want them, and the action that tries again or fixes it.
/// </summary>
public sealed class ErrorState : TemplatedControl
{
    /// <summary>What went wrong, such as "GitHub did not answer".</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ErrorState, string?>(nameof(Title));

    /// <summary>How to fix it, in a sentence.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ErrorState, string?>(nameof(Message));

    /// <summary>The technical details, such as the exception's message, shown in the code font and selectable.</summary>
    public static readonly StyledProperty<string?> DetailsProperty =
        AvaloniaProperty.Register<ErrorState, string?>(nameof(Details));

    /// <summary>The retry button's label; "Try Again" when there is a command and no label.</summary>
    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<ErrorState, string?>(nameof(RetryText), "Try Again");

    /// <summary>Tries again; no button when it is <see langword="null"/>.</summary>
    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<ErrorState, ICommand?>(nameof(RetryCommand));

    /// <summary>A second action that fixes the cause, such as "Sign In"; no button when it is empty.</summary>
    public static readonly StyledProperty<string?> FixTextProperty =
        AvaloniaProperty.Register<ErrorState, string?>(nameof(FixText));

    /// <summary>The second action.</summary>
    public static readonly StyledProperty<ICommand?> FixCommandProperty =
        AvaloniaProperty.Register<ErrorState, ICommand?>(nameof(FixCommand));

    /// <summary>Whether the state is drawn small, for a short panel.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ErrorState, bool>(nameof(IsCompact));

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <inheritdoc cref="DetailsProperty"/>
    public string? Details
    {
        get => GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    /// <inheritdoc cref="RetryTextProperty"/>
    public string? RetryText
    {
        get => GetValue(RetryTextProperty);
        set => SetValue(RetryTextProperty, value);
    }

    /// <inheritdoc cref="RetryCommandProperty"/>
    public ICommand? RetryCommand
    {
        get => GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    /// <inheritdoc cref="FixTextProperty"/>
    public string? FixText
    {
        get => GetValue(FixTextProperty);
        set => SetValue(FixTextProperty, value);
    }

    /// <inheritdoc cref="FixCommandProperty"/>
    public ICommand? FixCommand
    {
        get => GetValue(FixCommandProperty);
        set => SetValue(FixCommandProperty, value);
    }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }
}
