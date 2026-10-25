using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The look of grid cells: colors, font, alignment and format. A cell's style inherits what it leaves unset from its
/// row's, its column's and the grid's default styles.
/// </summary>
[TypeConverter(typeof(TypeConverter))]
public class DataGridViewCellStyle : ICloneable
{
    private string? _format;

    /// <summary>Creates a style with nothing set.</summary>
    public DataGridViewCellStyle()
    {
    }

    /// <summary>Creates a copy of a style.</summary>
    /// <param name="dataGridViewCellStyle">The style.</param>
    public DataGridViewCellStyle(DataGridViewCellStyle dataGridViewCellStyle)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCellStyle);
        ApplyStyle(dataGridViewCellStyle);
        Tag = dataGridViewCellStyle.Tag;
    }

    /// <summary>Where the content sits.</summary>
    public DataGridViewContentAlignment Alignment { get; set; }

    /// <summary>The background color.</summary>
    public Color BackColor { get; set; }

    /// <summary>The text color.</summary>
    public Color ForeColor { get; set; }

    /// <summary>The background color of selected cells.</summary>
    public Color SelectionBackColor { get; set; }

    /// <summary>The text color of selected cells.</summary>
    public Color SelectionForeColor { get; set; }

    /// <summary>The font.</summary>
    public Font? Font { get; set; }

    /// <summary>The format of values, such as "C2" or "d".</summary>
    [AllowNull]
    public string Format
    {
        get => _format ?? string.Empty;
        set => _format = value;
    }

    /// <summary>The culture or format provider of values.</summary>
    public IFormatProvider? FormatProvider { get; set; }

    /// <summary>Whether a format provider was set.</summary>
    public bool IsFormatProviderDefault => FormatProvider is null;

    /// <summary>What a null value shows as.</summary>
    public object? NullValue { get; set; } = string.Empty;

    /// <summary>The value a null shown value is stored as.</summary>
    public object? DataSourceNullValue { get; set; } = DBNull.Value;

    /// <summary>Whether a null value was set.</summary>
    public bool IsNullValueDefault => NullValue is string { Length: 0 };

    /// <summary>Whether the data source null value was set.</summary>
    public bool IsDataSourceNullValueDefault => DataSourceNullValue == DBNull.Value;

    /// <summary>The space inside a cell around its content.</summary>
    public Padding Padding { get; set; }

    /// <summary>Whether text wraps.</summary>
    public DataGridViewTriState WrapMode { get; set; }

    /// <summary>Data about the style.</summary>
    public object? Tag { get; set; }

    /// <summary>Takes every property another style sets.</summary>
    /// <param name="dataGridViewCellStyle">The style.</param>
    public virtual void ApplyStyle(DataGridViewCellStyle dataGridViewCellStyle)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCellStyle);
        var other = dataGridViewCellStyle;
        if (other.Alignment != DataGridViewContentAlignment.NotSet)
        {
            Alignment = other.Alignment;
        }

        if (!other.BackColor.IsEmpty)
        {
            BackColor = other.BackColor;
        }

        if (!other.ForeColor.IsEmpty)
        {
            ForeColor = other.ForeColor;
        }

        if (!other.SelectionBackColor.IsEmpty)
        {
            SelectionBackColor = other.SelectionBackColor;
        }

        if (!other.SelectionForeColor.IsEmpty)
        {
            SelectionForeColor = other.SelectionForeColor;
        }

        if (other.Font is not null)
        {
            Font = other.Font;
        }

        if (other._format is not null)
        {
            _format = other._format;
        }

        if (other.FormatProvider is not null)
        {
            FormatProvider = other.FormatProvider;
        }

        if (!other.IsNullValueDefault)
        {
            NullValue = other.NullValue;
        }

        if (!other.IsDataSourceNullValueDefault)
        {
            DataSourceNullValue = other.DataSourceNullValue;
        }

        if (other.Padding != Padding.Empty)
        {
            Padding = other.Padding;
        }

        if (other.WrapMode != DataGridViewTriState.NotSet)
        {
            WrapMode = other.WrapMode;
        }
    }

    /// <summary>Copies the style.</summary>
    public virtual DataGridViewCellStyle Clone() => new(this);

    /// <inheritdoc />
    object ICloneable.Clone() => Clone();

    /// <inheritdoc />
    public override string ToString() => $"DataGridViewCellStyle {{ BackColor={BackColor}, ForeColor={ForeColor}, Format={Format}, Alignment={Alignment} }}";
}
