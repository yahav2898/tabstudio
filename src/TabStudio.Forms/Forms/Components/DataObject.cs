namespace System.Windows.Forms;

/// <summary>Data in several formats, as the clipboard and drag and drop carry it.</summary>
public interface IDataObject
{
    /// <summary>The data in a format, or <see langword="null"/>.</summary>
    /// <param name="format">The format, such as <see cref="DataFormats.Text"/>.</param>
    object? GetData(string format);

    /// <summary>The data of a type, or <see langword="null"/>.</summary>
    /// <param name="format">The type.</param>
    object? GetData(Type format);

    /// <summary>Whether the data is in a format.</summary>
    /// <param name="format">The format.</param>
    bool GetDataPresent(string format);

    /// <summary>Whether the data is of a type.</summary>
    /// <param name="format">The type.</param>
    bool GetDataPresent(Type format);

    /// <summary>The formats the data is in.</summary>
    string[] GetFormats();

    /// <summary>Stores data in a format.</summary>
    /// <param name="format">The format.</param>
    /// <param name="data">The data.</param>
    void SetData(string format, object? data);

    /// <summary>Stores data in the format of its type.</summary>
    /// <param name="data">The data.</param>
    void SetData(object? data);
}

/// <summary>Data in several formats, such as the text a grid copies.</summary>
public class DataObject : IDataObject
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates an empty data object.</summary>
    public DataObject()
    {
    }

    /// <summary>Creates a data object with data in the format of its type.</summary>
    /// <param name="data">The data.</param>
    public DataObject(object data) => SetData(data);

    /// <summary>Creates a data object with data in a format.</summary>
    /// <param name="format">The format.</param>
    /// <param name="data">The data.</param>
    public DataObject(string format, object data) => SetData(format, data);

    /// <inheritdoc />
    public virtual object? GetData(string format) => _data.TryGetValue(Normalize(format), out var value) ? value : null;

    /// <inheritdoc />
    public virtual object? GetData(Type format) => GetData(format.FullName ?? format.Name);

    /// <inheritdoc />
    public virtual bool GetDataPresent(string format) => _data.ContainsKey(Normalize(format));

    /// <inheritdoc />
    public virtual bool GetDataPresent(Type format) => GetDataPresent(format.FullName ?? format.Name);

    /// <inheritdoc />
    public virtual string[] GetFormats() => [.. _data.Keys];

    /// <inheritdoc />
    public virtual void SetData(string format, object? data) => _data[Normalize(format)] = data;

    /// <inheritdoc />
    public virtual void SetData(object? data)
    {
        ArgumentNullException.ThrowIfNull(data);
        SetData(data is string ? DataFormats.Text : data.GetType().FullName ?? data.GetType().Name, data);
    }

    /// <summary>Whether the data holds text.</summary>
    public virtual bool ContainsText() => GetDataPresent(DataFormats.Text);

    /// <summary>The text the data holds, or an empty string.</summary>
    public virtual string GetText() => GetData(DataFormats.Text) as string ?? string.Empty;

    /// <summary>Stores text.</summary>
    /// <param name="textData">The text.</param>
    public virtual void SetText(string textData) => SetData(DataFormats.Text, textData);

    private static string Normalize(string format) =>
        format is "UnicodeText" or "System.String" or "OEMText" or "CommaSeparatedValue" ? DataFormats.Text : format;
}

/// <summary>The names of the clipboard's formats.</summary>
public static class DataFormats
{
    /// <summary>Text.</summary>
    public const string Text = "Text";

    /// <summary>Unicode text.</summary>
    public const string UnicodeText = "UnicodeText";

    /// <summary>Comma-separated values.</summary>
    public const string CommaSeparatedValue = "CommaSeparatedValue";

    /// <summary>HTML.</summary>
    public const string Html = "HTML Format";

    /// <summary>Rich text.</summary>
    public const string Rtf = "Rich Text Format";

    /// <summary>A bitmap.</summary>
    public const string Bitmap = "Bitmap";

    /// <summary>A list of files.</summary>
    public const string FileDrop = "FileDrop";

    /// <summary>A string object.</summary>
    public const string StringFormat = "System.String";
}

/// <summary>The kinds of text the clipboard holds.</summary>
public enum TextDataFormat
{
    /// <summary>Text.</summary>
    Text = 0,

    /// <summary>Unicode text.</summary>
    UnicodeText = 1,

    /// <summary>Rich text.</summary>
    Rtf = 2,

    /// <summary>HTML.</summary>
    Html = 3,

    /// <summary>Comma-separated values.</summary>
    CommaSeparatedValue = 4,
}
