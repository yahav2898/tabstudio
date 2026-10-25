using Avalonia;
using Avalonia.Controls.Primitives;

namespace TabStudio.App.Controls;

/// <summary>
/// What a panel shows while it loads: a turning arc and what is happening, such as "Loading pull requests…", in the
/// middle of the panel, or as one row under a list that is still growing.
/// </summary>
public sealed class LoadingState : TemplatedControl
{
    /// <summary>What is happening, ending in an ellipsis.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<LoadingState, string?>(nameof(Message));

    /// <summary>A quieter second line, such as how far it got.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<LoadingState, string?>(nameof(Detail));

    /// <summary>Whether it is one row in a list rather than the whole panel.</summary>
    public static readonly StyledProperty<bool> IsInlineProperty =
        AvaloniaProperty.Register<LoadingState, bool>(nameof(IsInline));

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <inheritdoc cref="IsInlineProperty"/>
    public bool IsInline
    {
        get => GetValue(IsInlineProperty);
        set => SetValue(IsInlineProperty, value);
    }
}
