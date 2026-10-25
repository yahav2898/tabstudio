using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace System.Windows.Forms;

/// <summary>Something that belongs to a grid: a row, a column or a cell.</summary>
public class DataGridViewElement
{
    /// <summary>Creates an element.</summary>
    public DataGridViewElement()
    {
    }

    /// <summary>The grid the element belongs to.</summary>
    public DataGridView? DataGridView { get; internal set; }

    /// <summary>The element's state.</summary>
    public virtual DataGridViewElementStates State => DataGridViewElementStates.Visible;

    /// <summary>Raises the grid's CellClick event, for derived cells.</summary>
    /// <param name="e">The event data.</param>
    protected void RaiseCellClick(DataGridViewCellEventArgs e) => DataGridView?.RaiseCellClick(e);

    /// <summary>Raises the grid's CellContentClick event, for derived cells.</summary>
    /// <param name="e">The event data.</param>
    protected void RaiseCellContentClick(DataGridViewCellEventArgs e) => DataGridView?.RaiseCellContentClick(e);

    /// <summary>Raises the grid's CellValueChanged event, for derived cells.</summary>
    /// <param name="e">The event data.</param>
    protected void RaiseCellValueChanged(DataGridViewCellEventArgs e) => DataGridView?.RaiseCellValueChanged(e);
}

/// <summary>A row or a column of a grid.</summary>
public class DataGridViewBand : DataGridViewElement, ICloneable, IDisposable
{
    private DataGridViewCellStyle? _defaultCellStyle;
    private bool _readOnly;
    private bool _visible = true;
    private bool _selected;

    /// <summary>Creates a band.</summary>
    internal DataGridViewBand()
    {
    }

    /// <summary>The position in the grid's rows or columns, or -1.</summary>
    public int Index { get; internal set; } = -1;

    /// <summary>The style of the band's cells.</summary>
    [AllowNull]
    public virtual DataGridViewCellStyle DefaultCellStyle
    {
        get => _defaultCellStyle ??= new DataGridViewCellStyle();
        set
        {
            _defaultCellStyle = value;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>Whether a style was set for the band's cells.</summary>
    public bool HasDefaultCellStyle => _defaultCellStyle is not null;

    /// <summary>Whether the band's cells cannot be edited.</summary>
    public virtual bool ReadOnly
    {
        get => _readOnly || (DataGridView?.ReadOnly ?? false);
        set
        {
            _readOnly = value;
            DataGridView?.Invalidate();
        }
    }

    /// <summary>Whether the band is shown.</summary>
    public virtual bool Visible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                DataGridView?.OnBandVisibilityChanged();
            }
        }
    }

    /// <summary>Whether the band stays in view when the grid scrolls. Kept for compatibility.</summary>
    public virtual bool Frozen { get; set; }

    /// <summary>Whether the user can resize the band.</summary>
    public virtual DataGridViewTriState Resizable { get; set; }

    /// <summary>Whether the band is selected.</summary>
    public virtual bool Selected
    {
        get => _selected;
        set
        {
            if (_selected != value)
            {
                _selected = value;
                DataGridView?.OnBandSelectionChanged(this);
            }
        }
    }

    /// <summary>Whether the band is shown on the grid now.</summary>
    public virtual bool Displayed => DataGridView is not null && _visible;

    /// <summary>Data about the band.</summary>
    public object? Tag { get; set; }

    /// <summary>The menu a long press on the band opens.</summary>
    public virtual ContextMenuStrip? ContextMenuStrip { get; set; }

    /// <summary>The band's own read-only setting.</summary>
    internal bool OwnReadOnly => _readOnly;

    /// <summary>Sets the selection without telling the grid.</summary>
    internal void SetSelectedInternal(bool value) => _selected = value;

    /// <summary>Copies the band.</summary>
    public virtual object Clone()
    {
        var band = (DataGridViewBand)MemberwiseClone();
        band.DataGridView = null;
        band.Index = -1;
        band._defaultCellStyle = _defaultCellStyle?.Clone();
        return band;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the band.</summary>
    /// <param name="disposing">Whether called from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}

/// <summary>A collection like the Windows Forms base collection, for compatibility.</summary>
public class BaseCollection : MarshalByRefObject, ICollection
{
    /// <summary>The number of items.</summary>
    public virtual int Count => List.Count;

    /// <summary>Whether the collection is read-only.</summary>
    public bool IsReadOnly => false;

    /// <summary>Whether the collection is synchronized.</summary>
    public bool IsSynchronized => false;

    /// <summary>The object to lock on.</summary>
    public object SyncRoot => this;

    /// <summary>The items.</summary>
    protected virtual ArrayList List { get; } = [];

    /// <summary>Copies the items into an array.</summary>
    /// <param name="ar">The array.</param>
    /// <param name="index">Where to start.</param>
    public void CopyTo(Array ar, int index) => List.CopyTo(ar, index);

    /// <inheritdoc />
    public IEnumerator GetEnumerator() => List.ToArray().GetEnumerator();
}
