using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TabStudio.App.Controls;

/// <summary>
/// What a panel shows when it has nothing to show (docs/design/README.md, "Panel states"): an icon, a title that says
/// what is missing, a sentence that says how to get it, and the action that does it. An empty panel is an invitation
/// to act, so the action is there whenever there is one.
/// </summary>
public sealed class EmptyState : TemplatedControl
{
    /// <summary>The icon: the panel's own, drawn large and quiet.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(Icon));

    /// <summary>What is missing, such as "No tests yet".</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    /// <summary>How to get it, in a sentence.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Message));

    /// <summary>The action's label, such as "Open a folder"; no button when it is empty.</summary>
    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(ActionText));

    /// <summary>The action.</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<EmptyState, ICommand?>(nameof(ActionCommand));

    /// <summary>The action's parameter.</summary>
    public static readonly StyledProperty<object?> ActionCommandParameterProperty =
        AvaloniaProperty.Register<EmptyState, object?>(nameof(ActionCommandParameter));

    /// <summary>Whether the action is the screen's main one, drawn in the accent color.</summary>
    public static readonly StyledProperty<bool> IsActionPrimaryProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(IsActionPrimary));

    /// <summary>Whether the state is drawn small, for a short panel such as the bottom dock.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(IsCompact));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

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

    /// <inheritdoc cref="ActionTextProperty"/>
    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    /// <inheritdoc cref="ActionCommandProperty"/>
    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <inheritdoc cref="ActionCommandParameterProperty"/>
    public object? ActionCommandParameter
    {
        get => GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }

    /// <inheritdoc cref="IsActionPrimaryProperty"/>
    public bool IsActionPrimary
    {
        get => GetValue(IsActionPrimaryProperty);
        set => SetValue(IsActionPrimaryProperty, value);
    }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }
}
