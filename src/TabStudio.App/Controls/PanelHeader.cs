using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TabStudio.App.Controls;

/// <summary>
/// The header every tool window shares (docs/design/README.md, "Panel anatomy"): an optional icon, the window's title in
/// sentence case, a quieter subtitle for its context (a repository, a run, a count), and its actions on the right,
/// usually an <see cref="AdaptiveBar"/> so they fold into More on a narrow panel.
/// </summary>
public sealed class PanelHeader : TemplatedControl
{
    /// <summary>The window's title, such as "Git Changes".</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PanelHeader, string?>(nameof(Title));

    /// <summary>Its context, such as the repository "tester/hello".</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<PanelHeader, string?>(nameof(Subtitle));

    /// <summary>The window's icon, the same as in the activity bar; none in most headers.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<PanelHeader, Geometry?>(nameof(Icon));

    /// <summary>The window's actions, on the right.</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<PanelHeader, object?>(nameof(Actions));

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="ActionsProperty"/>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
