using System.Collections;
using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>The child controls.</summary>
public partial class Control
{
    /// <summary>
    /// The child controls of a control, in z-order: the first is drawn on top, and docking lays out the last first,
    /// as in Windows Forms.
    /// </summary>
    [ListBindable(false)]
    public class ControlCollection : ArrangedElementCollection, IList, ICloneable
    {
        /// <summary>Creates the collection of a control's children.</summary>
        /// <param name="owner">The control.</param>
        public ControlCollection(Control owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            Owner = owner;
        }

        /// <summary>The control the children belong to.</summary>
        public Control Owner { get; }

        /// <summary>The child at an index.</summary>
        /// <param name="index">The index.</param>
        public virtual Control this[int index] => (Control)InnerList[index]!;

        /// <summary>The first child with a name, compared ignoring case.</summary>
        /// <param name="key">The name.</param>
        public virtual Control? this[string? key] => IndexOfKey(key) is >= 0 and var index ? this[index] : null;

        /// <inheritdoc />
        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException("Controls are added with Add and ordered with SetChildIndex.");
        }

        /// <inheritdoc />
        bool IList.IsReadOnly => false;

        /// <inheritdoc />
        bool IList.IsFixedSize => false;

        /// <summary>Adds a control on top of the others.</summary>
        /// <param name="value">The control.</param>
        public virtual void Add(Control? value)
        {
            if (value is null)
            {
                return;
            }

            if (value == Owner || value.Contains(Owner))
            {
                throw new ArgumentException("A circular control reference has been made. A control cannot be owned by or parented to itself.");
            }

            if (value is Form { TopLevel: true })
            {
                throw new ArgumentException("Top-level control cannot be added to a control. Set the form's TopLevel to false first.");
            }

            Owner.VerifyThread();
            if (value._parent == Owner)
            {
                // Adding a child again moves it to the end, as in Windows Forms.
                InnerList.Remove(value);
                InnerList.Add(value);
                Owner.PerformLayout(value, "ChildIndex");
                Owner.Invalidate();
                return;
            }

            value._parent?.Controls.Remove(value);
            InnerList.Add(value);
            value.AssignParent(Owner);
            if (value._tabIndex < 0)
            {
                value._tabIndex = InnerList.Count - 1;
            }

            Owner.InitChildLayout(value);
            Owner.OnControlAdded(new ControlEventArgs(value));
            Owner.PerformLayout(value, "Parent");
            Owner.Invalidate();
        }

        /// <summary>Adds controls in order.</summary>
        /// <param name="controls">The controls.</param>
        public virtual void AddRange(params Control[] controls)
        {
            ArgumentNullException.ThrowIfNull(controls);
            Owner.SuspendLayout();
            try
            {
                foreach (var control in controls)
                {
                    Add(control);
                }
            }
            finally
            {
                Owner.ResumeLayout(true);
            }
        }

        /// <summary>Removes a control.</summary>
        /// <param name="value">The control.</param>
        public virtual void Remove(Control? value)
        {
            if (value is null || value._parent != Owner)
            {
                return;
            }

            Owner.VerifyThread();
            Owner.Window?.ForgetControl(value);
            InnerList.Remove(value);
            value.AssignParent(null);
            Owner.OnControlRemoved(new ControlEventArgs(value));
            Owner.PerformLayout(value, "Parent");
            Owner.Invalidate();
        }

        /// <summary>Removes the control at an index.</summary>
        /// <param name="index">The index.</param>
        public void RemoveAt(int index) => Remove(this[index]);

        /// <summary>Removes the first child with a name.</summary>
        /// <param name="key">The name.</param>
        public virtual void RemoveByKey(string? key)
        {
            if (IndexOfKey(key) is >= 0 and var index)
            {
                RemoveAt(index);
            }
        }

        /// <summary>Removes every child.</summary>
        public virtual void Clear()
        {
            Owner.SuspendLayout();
            try
            {
                while (Count > 0)
                {
                    RemoveAt(Count - 1);
                }
            }
            finally
            {
                Owner.ResumeLayout(true);
            }
        }

        /// <summary>Whether a control is a child.</summary>
        /// <param name="control">The control.</param>
        public bool Contains(Control? control) => control is not null && InnerList.Contains(control);

        /// <summary>Whether a child has a name.</summary>
        /// <param name="key">The name.</param>
        public virtual bool ContainsKey(string? key) => IndexOfKey(key) >= 0;

        /// <summary>The index of a child.</summary>
        /// <param name="control">The control.</param>
        public int IndexOf(Control? control) => control is null ? -1 : InnerList.IndexOf(control);

        /// <summary>The index of the first child with a name, compared ignoring case.</summary>
        /// <param name="key">The name.</param>
        public virtual int IndexOfKey(string? key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return -1;
            }

            for (var i = 0; i < InnerList.Count; i++)
            {
                if (string.Equals(((Control)InnerList[i]!).Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The controls with a name, optionally searching the whole tree.</summary>
        /// <param name="key">The name.</param>
        /// <param name="searchAllChildren">Whether to search the children's children too.</param>
        public Control[] Find(string key, bool searchAllChildren)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            var found = new List<Control>();
            Collect(this, key, searchAllChildren, found);
            return [.. found];
        }

        /// <summary>The z-order index of a child.</summary>
        /// <param name="child">The child.</param>
        public int GetChildIndex(Control child) => GetChildIndex(child, true);

        /// <summary>The z-order index of a child.</summary>
        /// <param name="child">The child.</param>
        /// <param name="throwException">Whether to throw when it is not a child.</param>
        public virtual int GetChildIndex(Control child, bool throwException)
        {
            var index = IndexOf(child);
            if (index < 0 && throwException)
            {
                throw new ArgumentException("Child is not a child control of this parent.", nameof(child));
            }

            return index;
        }

        /// <summary>Moves a child in the z-order: index 0 is on top.</summary>
        /// <param name="child">The child.</param>
        /// <param name="newIndex">The new index; -1 moves it to the bottom.</param>
        public virtual void SetChildIndex(Control child, int newIndex)
        {
            var index = GetChildIndex(child);
            newIndex = Math.Clamp(newIndex < 0 ? Count - 1 : newIndex, 0, Count - 1);
            if (index == newIndex)
            {
                return;
            }

            InnerList.RemoveAt(index);
            InnerList.Insert(newIndex, child);
            Owner.PerformLayout(child, "ChildIndex");
            Owner.Invalidate();
        }

        /// <summary>A copy of the children, safe to change the collection while going through it.</summary>
        public Control[] ToArray() => [.. InnerList.Cast<Control>()];

        /// <summary>Creates a collection of the same owner; the controls themselves cannot belong to two parents.</summary>
        public object Clone() => new ControlCollection(Owner);

        /// <inheritdoc />
        int IList.Add(object? value)
        {
            Add((Control?)value);
            return IndexOf((Control?)value);
        }

        /// <inheritdoc />
        void IList.Remove(object? value) => Remove(value as Control);

        /// <inheritdoc />
        bool IList.Contains(object? value) => Contains(value as Control);

        /// <inheritdoc />
        int IList.IndexOf(object? value) => IndexOf(value as Control);

        /// <inheritdoc />
        void IList.Insert(int index, object? value) =>
            throw new NotSupportedException("Controls are added with Add and ordered with SetChildIndex.");

        /// <inheritdoc />
        void IList.Clear() => Clear();

        /// <inheritdoc />
        void IList.RemoveAt(int index) => RemoveAt(index);

        private static void Collect(ControlCollection controls, string key, bool deep, List<Control> found)
        {
            foreach (var control in controls.ToArray())
            {
                if (string.Equals(control.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(control);
                }

                if (deep)
                {
                    Collect(control.Controls, key, deep, found);
                }
            }
        }
    }
}

/// <summary>A collection of laid-out elements: the base of control collections.</summary>
public class ArrangedElementCollection : IList
{
    private readonly ArrayList _list = [];

    internal ArrangedElementCollection()
    {
    }

    /// <summary>The number of elements.</summary>
    public virtual int Count => _list.Count;

    /// <summary>Whether the collection is read-only.</summary>
    public virtual bool IsReadOnly => false;

    /// <inheritdoc />
    bool IList.IsFixedSize => false;

    /// <inheritdoc />
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc />
    object ICollection.SyncRoot => this;

    /// <summary>The elements.</summary>
    internal ArrayList InnerList => _list;

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => _list[index];
        set => throw new NotSupportedException();
    }

    /// <summary>Copies the elements into an array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="index">Where to start in the array.</param>
    public void CopyTo(Array array, int index) => _list.CopyTo(array, index);

    /// <summary>Goes through a copy of the elements, so the collection can change meanwhile.</summary>
    public virtual IEnumerator GetEnumerator() => _list.ToArray().GetEnumerator();

    /// <inheritdoc />
    int IList.Add(object? value) => throw new NotSupportedException();

    /// <inheritdoc />
    void IList.Clear() => throw new NotSupportedException();

    /// <inheritdoc />
    bool IList.Contains(object? value) => _list.Contains(value);

    /// <inheritdoc />
    int IList.IndexOf(object? value) => _list.IndexOf(value);

    /// <inheritdoc />
    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    /// <inheritdoc />
    void IList.Remove(object? value) => throw new NotSupportedException();

    /// <inheritdoc />
    void IList.RemoveAt(int index) => throw new NotSupportedException();
}
