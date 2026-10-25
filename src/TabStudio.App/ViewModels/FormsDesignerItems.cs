using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Forms.Design.Catalog;
using DrawingColor = System.Drawing.Color;

namespace TabStudio.App.ViewModels;

/// <summary>A tab of the Toolbox, such as Common Controls.</summary>
/// <param name="name">The tab's name.</param>
/// <param name="items">Its tools.</param>
public sealed class ToolboxCategoryViewModel(string name, IReadOnlyList<ToolboxItemViewModel> items)
{
    /// <summary>The tab's name.</summary>
    public string Name { get; } = name;

    /// <summary>Its tools.</summary>
    public IReadOnlyList<ToolboxItemViewModel> Items { get; } = items;
}

/// <summary>A tool of the Toolbox: tap it, then tap the form; or drag it onto the form.</summary>
public sealed partial class ToolboxItemViewModel : ObservableObject
{
    private readonly Action<ToolboxItemViewModel> _arm;

    /// <summary>Creates a tool.</summary>
    /// <param name="type">The control's or component's type.</param>
    /// <param name="name">The name shown.</param>
    /// <param name="icon">The icon's stroke path data.</param>
    /// <param name="arm">Arms or disarms the tool.</param>
    internal ToolboxItemViewModel(Type type, string name, string icon, Action<ToolboxItemViewModel> arm)
    {
        Type = type;
        Name = name;
        Icon = StreamGeometry.Parse(icon);
        _arm = arm;
    }

    /// <summary>The type the tool adds.</summary>
    public Type Type { get; }

    /// <summary>The name shown.</summary>
    public string Name { get; }

    /// <summary>The icon.</summary>
    public Geometry Icon { get; }

    /// <summary>Whether the next tap on the form adds this tool.</summary>
    [ObservableProperty]
    public partial bool IsArmed { get; set; }

    /// <summary>Arms the tool, or puts it away when it is armed.</summary>
    [RelayCommand]
    private void Arm() => _arm(this);
}

/// <summary>
/// An event in the Properties panel's events: its handler, which a double-tap creates. A name typed there gets its
/// method in the code; clearing it takes the handler off the event.
/// </summary>
public sealed partial class FormsEventViewModel : ObservableObject
{
    private readonly FormsDesignerViewModel _designer;
    private readonly string _component;
    private bool _loading;

    internal FormsEventViewModel(FormsDesignerViewModel designer, string component, string eventName, string? handler)
    {
        _designer = designer;
        _component = component;
        Name = eventName;
        _loading = true;
        Handler = handler ?? string.Empty;
        _loading = false;
    }

    /// <summary>The event.</summary>
    public string Name { get; }

    /// <summary>The handler's name, or empty.</summary>
    [ObservableProperty]
    public partial string Handler { get; set; }

    partial void OnHandlerChanged(string value)
    {
        if (!_loading)
        {
            _designer.SetHandler(_component, Name, value);
        }
    }

    /// <summary>Creates the handler, or goes to it, in the form's code.</summary>
    [RelayCommand]
    private void Open()
    {
        if (_designer.Find(_component) is { } component)
        {
            _designer.CreateHandler(component, Name);
        }
    }
}

/// <summary>A property in the Properties panel, edited as its kind suggests.</summary>
public abstract partial class FormsPropertyViewModel : ObservableObject
{
    private bool _loading;

    private protected FormsPropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
    {
        Designer = designer;
        ComponentName = component;
        Property = property;
    }

    /// <summary>The property's path.</summary>
    public string Path => Property.Path;

    /// <summary>The name shown.</summary>
    public string Label => Property.Label;

    /// <summary>The group it is in.</summary>
    public string Category => Property.Category;

    /// <summary>Whether it is the first of its group, under which the group's name shows.</summary>
    public bool StartsCategory { get; internal set; }

    /// <summary>The component whose property it is.</summary>
    internal string ComponentName { get; }

    private protected FormsDesignerViewModel Designer { get; }

    private protected DesignProperty Property { get; }

    /// <summary>Whether a value is being shown rather than changed by the user.</summary>
    private protected bool IsLoading => _loading;

    /// <summary>Shows a value without setting it.</summary>
    /// <param name="value">The value.</param>
    internal void Load(object? value)
    {
        _loading = true;
        try
        {
            Show(value);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>The row for a property, with its value.</summary>
    internal static FormsPropertyViewModel Create(FormsDesignerViewModel designer, string component, DesignProperty property, object? value)
    {
        FormsPropertyViewModel row = property.Editor switch
        {
            PropertyEditor.Boolean => new BooleanPropertyViewModel(designer, component, property),
            PropertyEditor.Integer or PropertyEditor.Decimal or PropertyEditor.FontSize => new NumberPropertyViewModel(designer, component, property),
            PropertyEditor.Choice => new ChoicePropertyViewModel(designer, component, property),
            PropertyEditor.Point or PropertyEditor.Size => new PairPropertyViewModel(designer, component, property),
            PropertyEditor.Anchor => new AnchorPropertyViewModel(designer, component, property),
            PropertyEditor.Color => new ColorPropertyViewModel(designer, component, property),
            PropertyEditor.FontFamily => new FontFamilyPropertyViewModel(designer, component, property),
            _ => new TextPropertyViewModel(designer, component, property),
        };
        row.Load(value);
        return row;
    }

    /// <summary>Shows a value.</summary>
    private protected abstract void Show(object? value);

    /// <summary>Sets the property, unless a value is only being shown.</summary>
    private protected void Commit(object? value)
    {
        if (!_loading)
        {
            Designer.SetProperty(ComponentName, Property.Path, value);
        }
    }
}

/// <summary>A property edited as text: a name, or the text of a control.</summary>
public sealed partial class TextPropertyViewModel : FormsPropertyViewModel
{
    internal TextPropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
        : base(designer, component, property)
    {
    }

    /// <summary>Whether the text can have several lines.</summary>
    public bool IsMultiline => Property.Editor == PropertyEditor.MultilineText;

    /// <summary>The text.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    partial void OnTextChanged(string value) => Commit(value);

    private protected override void Show(object? value) => Text = value as string ?? string.Empty;
}

/// <summary>A property that is on or off.</summary>
public sealed partial class BooleanPropertyViewModel : FormsPropertyViewModel
{
    internal BooleanPropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
        : base(designer, component, property)
    {
    }

    /// <summary>The value.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    partial void OnIsCheckedChanged(bool value) => Commit(value);

    private protected override void Show(object? value) => IsChecked = value is true;
}

/// <summary>A number: a whole number, a decimal, or a font's size in points.</summary>
public sealed partial class NumberPropertyViewModel : FormsPropertyViewModel
{
    internal NumberPropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
        : base(designer, component, property)
    {
    }

    /// <summary>The value.</summary>
    [ObservableProperty]
    public partial decimal? Value { get; set; }

    /// <summary>The least value.</summary>
    public decimal Minimum => Property.Editor == PropertyEditor.FontSize ? 6 : Property.ValueType == typeof(int) && Property.Path == "TabIndex" ? 0 : -1_000_000;

    /// <summary>The greatest value.</summary>
    public decimal Maximum => Property.Editor == PropertyEditor.FontSize ? 96 : 1_000_000;

    /// <summary>The number of decimals shown.</summary>
    public string Format => Property.ValueType == typeof(int) ? "0" : "0.##";

    /// <summary>The step of the arrows.</summary>
    public decimal Increment => Property.Editor == PropertyEditor.FontSize ? 1 : Property.ValueType == typeof(int) ? 1 : 0.1m;

    partial void OnValueChanged(decimal? value)
    {
        if (IsLoading || value is not { } number)
        {
            return;
        }

        if (Property.Editor == PropertyEditor.FontSize)
        {
            if (Designer.Find(ComponentName)?.Instance is System.Windows.Forms.Control { Font: { } font })
            {
                // As Visual Studio's font editor writes it: in points, with the default character set.
                Commit(new System.Drawing.Font(font.FontFamily.Name, (float)number, font.Style, System.Drawing.GraphicsUnit.Point, 0));
            }

            return;
        }

        Commit(Property.ValueType == typeof(int) ? (object)(int)number : number);
    }

    private protected override void Show(object? value) => Value = value switch
    {
        int i => i,
        decimal d => d,
        float f => (decimal)Math.Round(f, 2),
        double d => (decimal)Math.Round(d, 2),
        _ => null,
    };
}

/// <summary>One value of an enumeration, such as Dock or FormBorderStyle.</summary>
public sealed partial class ChoicePropertyViewModel : FormsPropertyViewModel
{
    internal ChoicePropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
        : base(designer, component, property)
    {
        Options = Enum.GetNames(property.ValueType);
    }

    /// <summary>The values to choose from.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>The value.</summary>
    [ObservableProperty]
    public partial string? Selected { get; set; }

    partial void OnSelectedChanged(string? value)
    {
        if (value is not null)
        {
            Commit(Enum.Parse(Property.ValueType, value));
        }
    }

    private protected override void Show(object? value) => Selected = value?.ToString();
}

/// <summary>A location or a size: two numbers.</summary>
public sealed partial class PairPropertyViewModel : FormsPropertyViewModel
{
    internal PairPropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
        : base(designer, component, property)
    {
    }

    /// <summary>Whether it is a size rather than a location.</summary>
    public bool IsSize => Property.Editor == PropertyEditor.Size;

    /// <summary>The first number's label: X, or width.</summary>
    public string FirstLabel => IsSize ? "W" : "X";

    /// <summary>The second number's label: Y, or height.</summary>
    public string SecondLabel => IsSize ? "H" : "Y";

    /// <summary>The least value.</summary>
    public decimal Minimum => IsSize ? 0 : -10_000;

    /// <summary>The first number.</summary>
    [ObservableProperty]
    public partial decimal? First { get; set; }

    /// <summary>The second number.</summary>
    [ObservableProperty]
    public partial decimal? Second { get; set; }

    partial void OnFirstChanged(decimal? value) => CommitPair();

    partial void OnSecondChanged(decimal? value) => CommitPair();

    private protected override void Show(object? value)
    {
        switch (value)
        {
            case System.Drawing.Point point:
                First = point.X;
                Second = point.Y;
                break;
            case System.Drawing.Size size:
                First = size.Width;
                Second = size.Height;
                break;
        }
    }

    private void CommitPair()
    {
        if (IsLoading || First is not { } first || Second is not { } second)
        {
            return;
        }

        Commit(IsSize ? new System.Drawing.Size((int)first, (int)second) : new System.Drawing.Point((int)first, (int)second));
    }
}

/// <summary>The edges a control is anchored to: four toggles around a box, as Visual Studio's editor shows them.</summary>
public sealed partial class AnchorPropertyViewModel : FormsPropertyViewModel
{
    internal AnchorPropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
        : base(designer, component, property)
    {
    }

    /// <summary>Anchored to the top.</summary>
    [ObservableProperty]
    public partial bool Top { get; set; }

    /// <summary>Anchored to the bottom.</summary>
    [ObservableProperty]
    public partial bool Bottom { get; set; }

    /// <summary>Anchored to the left.</summary>
    [ObservableProperty]
    public partial bool Left { get; set; }

    /// <summary>Anchored to the right.</summary>
    [ObservableProperty]
    public partial bool Right { get; set; }

    /// <summary>The anchor as Visual Studio shows it, such as <c>Top, Left</c>.</summary>
    public string Summary => Value().ToString();

    partial void OnTopChanged(bool value) => CommitAnchor();

    partial void OnBottomChanged(bool value) => CommitAnchor();

    partial void OnLeftChanged(bool value) => CommitAnchor();

    partial void OnRightChanged(bool value) => CommitAnchor();

    private protected override void Show(object? value)
    {
        var anchor = value is System.Windows.Forms.AnchorStyles styles ? styles : System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        Top = anchor.HasFlag(System.Windows.Forms.AnchorStyles.Top);
        Bottom = anchor.HasFlag(System.Windows.Forms.AnchorStyles.Bottom);
        Left = anchor.HasFlag(System.Windows.Forms.AnchorStyles.Left);
        Right = anchor.HasFlag(System.Windows.Forms.AnchorStyles.Right);
        OnPropertyChanged(nameof(Summary));
    }

    private System.Windows.Forms.AnchorStyles Value() =>
        (Top ? System.Windows.Forms.AnchorStyles.Top : 0) | (Bottom ? System.Windows.Forms.AnchorStyles.Bottom : 0)
        | (Left ? System.Windows.Forms.AnchorStyles.Left : 0) | (Right ? System.Windows.Forms.AnchorStyles.Right : 0);

    private void CommitAnchor()
    {
        OnPropertyChanged(nameof(Summary));
        Commit(Value());
    }
}

/// <summary>A color: a swatch, and a palette of Windows' system colors and named colors to pick from.</summary>
public sealed partial class ColorPropertyViewModel : FormsPropertyViewModel
{
    /// <summary>The colors the palette offers: the system colors controls use, then named colors.</summary>
    private static readonly DrawingColor[] PaletteColors =
    [
        System.Drawing.SystemColors.Control, System.Drawing.SystemColors.ControlText, System.Drawing.SystemColors.Window, System.Drawing.SystemColors.WindowText,
        System.Drawing.SystemColors.Highlight, System.Drawing.SystemColors.HighlightText, System.Drawing.SystemColors.GrayText, System.Drawing.SystemColors.Info,
        DrawingColor.White, DrawingColor.Black, DrawingColor.Gray, DrawingColor.Silver, DrawingColor.Red, DrawingColor.DarkRed, DrawingColor.Orange, DrawingColor.Gold,
        DrawingColor.Yellow, DrawingColor.LightYellow, DrawingColor.Green, DrawingColor.LimeGreen, DrawingColor.Teal, DrawingColor.LightBlue, DrawingColor.SteelBlue, DrawingColor.Blue,
        DrawingColor.Navy, DrawingColor.Purple, DrawingColor.Magenta, DrawingColor.Pink, DrawingColor.Brown, DrawingColor.Tan, DrawingColor.Transparent, DrawingColor.FromArgb(0, 120, 215),
    ];

    internal ColorPropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
        : base(designer, component, property)
    {
        Palette = [.. PaletteColors.Select(c => new ColorChoiceViewModel(this, c))];
    }

    /// <summary>The colors to pick from.</summary>
    public IReadOnlyList<ColorChoiceViewModel> Palette { get; }

    /// <summary>The color's name, such as <c>Control</c> or <c>255, 128, 0</c>.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    /// <summary>The color's swatch.</summary>
    [ObservableProperty]
    public partial IBrush Swatch { get; private set; } = Brushes.Transparent;

    /// <summary>Picks a color.</summary>
    /// <param name="color">The color.</param>
    internal void Pick(DrawingColor color)
    {
        Show(color);
        Commit(color);
    }

    private protected override void Show(object? value)
    {
        var color = value is DrawingColor c ? c : DrawingColor.Empty;
        Name = color.IsEmpty ? string.Empty : color.IsNamedColor ? color.Name : string.Create(CultureInfo.InvariantCulture, $"{color.R}, {color.G}, {color.B}");
        Swatch = ColorChoiceViewModel.BrushOf(color);
    }
}

/// <summary>A color in a color property's palette.</summary>
public sealed partial class ColorChoiceViewModel(ColorPropertyViewModel owner, DrawingColor color)
{
    /// <summary>The color's name.</summary>
    public string Name { get; } = color.Name;

    /// <summary>The color's swatch.</summary>
    public IBrush Swatch { get; } = BrushOf(color);

    /// <summary>A brush of a color, as Windows draws it.</summary>
    internal static IBrush BrushOf(DrawingColor color)
    {
        var drawn = TabStudio.Forms.Rendering.SystemColorValues.Resolve(color);
        return new SolidColorBrush(Color.FromArgb(drawn.A, drawn.R, drawn.G, drawn.B));
    }

    /// <summary>Picks this color.</summary>
    [RelayCommand]
    private void Pick() => owner.Pick(color);
}

/// <summary>A font's family, from the common Windows families.</summary>
public sealed partial class FontFamilyPropertyViewModel : FormsPropertyViewModel
{
    internal FontFamilyPropertyViewModel(FormsDesignerViewModel designer, string component, DesignProperty property)
        : base(designer, component, property)
    {
    }

    /// <summary>The families to choose from.</summary>
    public IReadOnlyList<string> Options { get; } = System.Windows.Forms.FontDialog.Families;

    /// <summary>The family.</summary>
    [ObservableProperty]
    public partial string? Selected { get; set; }

    partial void OnSelectedChanged(string? value)
    {
        if (IsLoading || value is null || Designer.Find(ComponentName)?.Instance is not System.Windows.Forms.Control { Font: { } font })
        {
            return;
        }

        Commit(new System.Drawing.Font(value, font.Size, font.Style, font.Unit, 0));
    }

    private protected override void Show(object? value) => Selected = (value as System.Drawing.Font)?.Name;
}
