using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>Images of one size, for tool bars, tabs and lists to show by index or key.</summary>
[DefaultProperty(nameof(Images))]
public sealed class ImageList : Component
{
    private Size _imageSize = new(16, 16);

    /// <summary>Creates an image list.</summary>
    public ImageList()
    {
        Images = new ImageCollection(this);
    }

    /// <summary>Creates an image list that is disposed with a container.</summary>
    /// <param name="container">The container.</param>
    public ImageList(IContainer container)
        : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    /// <summary>Occurs when the images are recreated. Kept for compatibility.</summary>
    public event EventHandler? RecreateHandle;

    /// <summary>The images.</summary>
    public ImageCollection Images { get; }

    /// <summary>The size images are drawn at.</summary>
    public Size ImageSize
    {
        get => _imageSize;
        set
        {
            if (value.Width is <= 0 or > 256 || value.Height is <= 0 or > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ImageSize must be between 1 and 256 pixels.");
            }

            _imageSize = value;
            RecreateHandle?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The color depth. Images keep their own colors.</summary>
    public ColorDepth ColorDepth { get; set; } = ColorDepth.Depth32Bit;

    /// <summary>A color drawn transparent.</summary>
    public Color TransparentColor { get; set; } = Color.Transparent;

    /// <summary>
    /// The images as a whole, as the designer stores them in the form's .resx file: setting it replaces the list's
    /// images and size; getting it copies them, for another list.
    /// </summary>
    public ImageListStreamer? ImageStream
    {
        get => Images.Count == 0 ? null : new ImageListStreamer(_imageSize, [.. Images.Cast<Image>().Select(image => (Image)image.Clone())]);
        set
        {
            if (value is null)
            {
                Images.Clear();
                return;
            }

            // The images a .resx file holds for the designer, or another list's: they replace this list's.
            Images.Clear();
            _imageSize = value.ImageSize;
            foreach (var image in value.Images)
            {
                Images.Add((Image)image.Clone());
            }

            RecreateHandle?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Data about the list.</summary>
    public object? Tag { get; set; }

    /// <summary>Whether the images exist. They do.</summary>
    public bool HandleCreated => true;

    /// <summary>Draws an image at a point.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="pt">The point.</param>
    /// <param name="index">The image.</param>
    public void Draw(Graphics g, Point pt, int index) => Draw(g, pt.X, pt.Y, index);

    /// <summary>Draws an image at a point.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="index">The image.</param>
    public void Draw(Graphics g, int x, int y, int index) => Draw(g, x, y, _imageSize.Width, _imageSize.Height, index);

    /// <summary>Draws an image in a rectangle.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="index">The image.</param>
    public void Draw(Graphics g, int x, int y, int width, int height, int index)
    {
        ArgumentNullException.ThrowIfNull(g);
        g.DrawImage(Images[index], new Rectangle(x, y, width, height));
    }

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()} Images.Count: {Images.Count}, ImageSize: {_imageSize}";

    /// <summary>The images of an image list, with optional keys.</summary>
    public sealed class ImageCollection : IList
    {
        private readonly ImageList _owner;
        private readonly List<(Image Image, string Key)> _images = [];

        internal ImageCollection(ImageList owner) => _owner = owner;

        /// <summary>The number of images.</summary>
        public int Count => _images.Count;

        /// <summary>Whether the list is empty.</summary>
        public bool Empty => _images.Count == 0;

        /// <summary>Whether the collection is read-only.</summary>
        public bool IsReadOnly => false;

        /// <summary>The keys, in order.</summary>
        public StringCollection Keys
        {
            get
            {
                var keys = new StringCollection();
                foreach (var (_, key) in _images)
                {
                    keys.Add(key);
                }

                return keys;
            }
        }

        /// <inheritdoc />
        bool IList.IsFixedSize => false;

        /// <inheritdoc />
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc />
        object ICollection.SyncRoot => this;

        /// <summary>The image at an index.</summary>
        /// <param name="index">The index.</param>
        public Image this[int index]
        {
            get => _images[index].Image;
            set => _images[index] = (value ?? throw new ArgumentNullException(nameof(value)), _images[index].Key);
        }

        /// <summary>The image with a key.</summary>
        /// <param name="key">The key.</param>
        public Image? this[string key] => IndexOfKey(key) is >= 0 and var index ? _images[index].Image : null;

        /// <inheritdoc />
        object? IList.this[int index]
        {
            get => this[index];
            set => this[index] = (Image)value!;
        }

        /// <summary>Adds an image.</summary>
        /// <param name="value">The image.</param>
        public void Add(Image value) => Add(string.Empty, value);

        /// <summary>Adds an icon.</summary>
        /// <param name="value">The icon.</param>
        public void Add(Icon value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Add(value.ToBitmap());
        }

        /// <summary>Adds an image with a key.</summary>
        /// <param name="key">The key.</param>
        /// <param name="image">The image.</param>
        public void Add(string key, Image image)
        {
            ArgumentNullException.ThrowIfNull(image);
            _images.Add((image, key ?? string.Empty));
            _owner.RecreateHandle?.Invoke(_owner, EventArgs.Empty);
        }

        /// <summary>Adds an icon with a key.</summary>
        /// <param name="key">The key.</param>
        /// <param name="icon">The icon.</param>
        public void Add(string key, Icon icon)
        {
            ArgumentNullException.ThrowIfNull(icon);
            Add(key, icon.ToBitmap());
        }

        /// <summary>Adds the images of a strip, each as wide as the list's image size.</summary>
        /// <param name="value">The strip.</param>
        /// <returns>The number of images added.</returns>
        public int AddStrip(Image value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value is not Bitmap bitmap)
            {
                throw new ArgumentException("Image must be a bitmap.", nameof(value));
            }

            var width = _owner.ImageSize.Width;
            var count = bitmap.Width / width;
            for (var i = 0; i < count; i++)
            {
                Add(bitmap.Clone(new Rectangle(i * width, 0, width, bitmap.Height), bitmap.PixelFormat));
            }

            return count;
        }

        /// <summary>Adds images.</summary>
        /// <param name="images">The images.</param>
        public void AddRange(params Image[] images)
        {
            ArgumentNullException.ThrowIfNull(images);
            foreach (var image in images)
            {
                Add(image);
            }
        }

        /// <summary>Removes every image.</summary>
        public void Clear() => _images.Clear();

        /// <summary>Whether an image is in the list.</summary>
        /// <param name="image">The image.</param>
        public bool Contains(Image image) => _images.Exists(entry => entry.Image == image);

        /// <summary>Whether a key is in the list.</summary>
        /// <param name="key">The key.</param>
        public bool ContainsKey(string key) => IndexOfKey(key) >= 0;

        /// <summary>The index of an image, or -1.</summary>
        /// <param name="image">The image.</param>
        public int IndexOf(Image image) => _images.FindIndex(entry => entry.Image == image);

        /// <summary>The index of the image with a key, or -1.</summary>
        /// <param name="key">The key.</param>
        public int IndexOfKey(string? key) =>
            string.IsNullOrEmpty(key) ? -1 : _images.FindIndex(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>Removes an image.</summary>
        /// <param name="image">The image.</param>
        public void Remove(Image image)
        {
            var index = IndexOf(image);
            if (index >= 0)
            {
                _images.RemoveAt(index);
            }
        }

        /// <summary>Removes the image at an index.</summary>
        /// <param name="index">The index.</param>
        public void RemoveAt(int index) => _images.RemoveAt(index);

        /// <summary>Removes the image with a key.</summary>
        /// <param name="key">The key.</param>
        public void RemoveByKey(string key)
        {
            var index = IndexOfKey(key);
            if (index >= 0)
            {
                _images.RemoveAt(index);
            }
        }

        /// <summary>Sets the key of an image.</summary>
        /// <param name="index">The image.</param>
        /// <param name="name">The key.</param>
        public void SetKeyName(int index, string name) => _images[index] = (_images[index].Image, name ?? string.Empty);

        /// <inheritdoc />
        public IEnumerator GetEnumerator() => _images.Select(entry => entry.Image).ToList().GetEnumerator();

        /// <inheritdoc />
        void ICollection.CopyTo(Array dest, int index) => ((ICollection)_images.Select(entry => entry.Image).ToArray()).CopyTo(dest, index);

        /// <inheritdoc />
        int IList.Add(object? value)
        {
            Add((Image)value!);
            return Count - 1;
        }

        /// <inheritdoc />
        bool IList.Contains(object? value) => value is Image image && Contains(image);

        /// <inheritdoc />
        int IList.IndexOf(object? value) => value is Image image ? IndexOf(image) : -1;

        /// <inheritdoc />
        void IList.Insert(int index, object? value) => throw new NotSupportedException("Images are added with Add.");

        /// <inheritdoc />
        void IList.Remove(object? value)
        {
            if (value is Image image)
            {
                Remove(image);
            }
        }
    }
}

/// <summary>The color depth of an image list.</summary>
public enum ColorDepth
{
    /// <summary>4 bits.</summary>
    Depth4Bit = 4,

    /// <summary>8 bits.</summary>
    Depth8Bit = 8,

    /// <summary>16 bits.</summary>
    Depth16Bit = 16,

    /// <summary>24 bits.</summary>
    Depth24Bit = 24,

    /// <summary>32 bits.</summary>
    Depth32Bit = 32,
}
