using System.Globalization;
using Avalonia;
using TabStudio.Swarm;

namespace TabStudio.App.Swarm;

/// <summary>
/// Turns the screens' types (<see cref="AppInput"/>, <see cref="LayoutIssue"/>, <see cref="ScreenProfile"/>) into the
/// swarm's wire types and back, and describes inputs in words.
/// </summary>
public static class SwarmScreens
{
    /// <summary>The input as it travels to a device.</summary>
    /// <param name="input">The input.</param>
    public static SwarmInput ToSwarm(AppInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new SwarmInput(
            (SwarmInputKind)(int)input.Kind,
            input.ElementPath,
            input.Position.X,
            input.Position.Y,
            input.Text,
            input.Key,
            input.Modifiers,
            input.ScrollDelta.X,
            input.ScrollDelta.Y);
    }

    /// <summary>An input that arrived from a device.</summary>
    /// <param name="input">The input.</param>
    public static AppInput ToApp(SwarmInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new AppInput(
            (AppInputKind)(int)input.Kind,
            input.ElementPath,
            new Point(input.X, input.Y),
            input.Text,
            input.Key,
            input.Modifiers,
            new Vector(input.ScrollX, input.ScrollY));
    }

    /// <summary>A layout issue as it travels from a device.</summary>
    /// <param name="issue">The issue.</param>
    public static SwarmLayoutIssue ToSwarm(LayoutIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new SwarmLayoutIssue(issue.Kind.ToString(), issue.Severity.ToString(), issue.Message, issue.ElementPath, issue.Bounds.X, issue.Bounds.Y, issue.Bounds.Width, issue.Bounds.Height);
    }

    /// <summary>A layout issue that arrived from a device; <see langword="null"/> for a kind this version does not know.</summary>
    /// <param name="issue">The issue.</param>
    public static LayoutIssue? ToApp(SwarmLayoutIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return Enum.TryParse<LayoutIssueKind>(issue.Kind, out var kind)
            ? new LayoutIssue(
                kind,
                Enum.TryParse<LayoutIssueSeverity>(issue.Severity, out var severity) ? severity : LayoutIssueSeverity.Warning,
                issue.Message,
                issue.ElementPath,
                new Rect(issue.X, issue.Y, Math.Max(0, issue.Width), Math.Max(0, issue.Height)))
            : null;
    }

    /// <summary>The profile of a device's screen.</summary>
    /// <param name="name">The device's name.</param>
    /// <param name="screen">Its screen.</param>
    public static ScreenProfile Profile(string name, SwarmScreenInfo screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        return new ScreenProfile(
            name,
            FormFactorOf(screen.Width, screen.Height),
            screen.Width,
            screen.Height,
            screen.Scale,
            screen.IsDark,
            screen.IsRightToLeft ? TextDirection.RightToLeft : TextDirection.LeftToRight,
            screen.FontScale);
    }

    /// <summary>The kind of device a screen of a size belongs to, by its shorter side, as Android's size classes go.</summary>
    /// <param name="width">The width, in device-independent pixels.</param>
    /// <param name="height">The height, in device-independent pixels.</param>
    public static FormFactor FormFactorOf(double width, double height)
    {
        var shorter = Math.Min(width, height);
        return shorter switch
        {
            < 400 => FormFactor.Phone,
            < 600 => FormFactor.Phablet,
            _ => FormFactor.Tablet,
        };
    }

    /// <summary>A form factor in words, as the Swarm window shows it: Phone, Large phone, Tablet or Desktop.</summary>
    /// <param name="formFactor">The form factor.</param>
    public static string Describe(FormFactor formFactor) => formFactor switch
    {
        FormFactor.Phone => "Phone",
        FormFactor.Phablet => "Large phone",
        FormFactor.Tablet => "Tablet",
        _ => "Desktop",
    };

    /// <summary>A screen's size in words, such as <c>412 × 915</c>.</summary>
    /// <param name="width">The width, in device-independent pixels.</param>
    /// <param name="height">The height, in device-independent pixels.</param>
    public static string DescribeSize(double width, double height) =>
        width <= 0 || height <= 0
            ? "Size unknown"
            : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(width):0} × {Math.Round(height):0}");

    /// <summary>An input in words, such as <c>Tap on CountButton</c> or <c>Typing "Ada"</c>.</summary>
    /// <param name="input">The input.</param>
    public static string Describe(AppInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var element = ElementPaths.ShortName(input.ElementPath);
        return input.Kind switch
        {
            AppInputKind.Tap => $"Tap on {element}",
            AppInputKind.LongPress => $"Long press on {element}",
            AppInputKind.Scroll => $"Scroll on {element}",
            AppInputKind.Text => $"Typing \"{input.Text}\"",
            _ => input.Modifiers is { Length: > 0 } modifiers ? $"{modifiers.Replace(", ", "+", StringComparison.Ordinal)}+{input.Key}" : $"Key {input.Key}",
        };
    }
}
