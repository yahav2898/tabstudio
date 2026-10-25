using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>Space inside a control around its content, or outside it as a margin.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public struct Padding : IEquatable<Padding>
{
    /// <summary>No padding.</summary>
    public static readonly Padding Empty = new(0);

    /// <summary>Creates the same padding on every side.</summary>
    /// <param name="all">The padding of every side.</param>
    public Padding(int all)
    {
        Left = all;
        Top = all;
        Right = all;
        Bottom = all;
    }

    /// <summary>Creates padding for each side.</summary>
    /// <param name="left">The left padding.</param>
    /// <param name="top">The top padding.</param>
    /// <param name="right">The right padding.</param>
    /// <param name="bottom">The bottom padding.</param>
    public Padding(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>The padding of every side, or -1 when the sides differ.</summary>
    public int All
    {
        readonly get => Left == Top && Top == Right && Right == Bottom ? Left : -1;
        set
        {
            Left = value;
            Top = value;
            Right = value;
            Bottom = value;
        }
    }

    /// <summary>The left padding.</summary>
    public int Left { readonly get; set; }

    /// <summary>The top padding.</summary>
    public int Top { readonly get; set; }

    /// <summary>The right padding.</summary>
    public int Right { readonly get; set; }

    /// <summary>The bottom padding.</summary>
    public int Bottom { readonly get; set; }

    /// <summary>The left and right padding together.</summary>
    public readonly int Horizontal => Left + Right;

    /// <summary>The top and bottom padding together.</summary>
    public readonly int Vertical => Top + Bottom;

    /// <summary>The horizontal and vertical padding as a size.</summary>
    public readonly Size Size => new(Horizontal, Vertical);

    /// <summary>Adds two paddings side by side.</summary>
    /// <param name="p1">The first padding.</param>
    /// <param name="p2">The second padding.</param>
    public static Padding Add(Padding p1, Padding p2) => p1 + p2;

    /// <summary>Subtracts a padding side by side.</summary>
    /// <param name="p1">The padding.</param>
    /// <param name="p2">The padding to subtract.</param>
    public static Padding Subtract(Padding p1, Padding p2) => p1 - p2;

    /// <summary>Adds two paddings side by side.</summary>
    /// <param name="p1">The first padding.</param>
    /// <param name="p2">The second padding.</param>
    public static Padding operator +(Padding p1, Padding p2) =>
        new(p1.Left + p2.Left, p1.Top + p2.Top, p1.Right + p2.Right, p1.Bottom + p2.Bottom);

    /// <summary>Subtracts a padding side by side.</summary>
    /// <param name="p1">The padding.</param>
    /// <param name="p2">The padding to subtract.</param>
    public static Padding operator -(Padding p1, Padding p2) =>
        new(p1.Left - p2.Left, p1.Top - p2.Top, p1.Right - p2.Right, p1.Bottom - p2.Bottom);

    /// <summary>Whether two paddings are the same.</summary>
    /// <param name="p1">The first padding.</param>
    /// <param name="p2">The second padding.</param>
    public static bool operator ==(Padding p1, Padding p2) => p1.Equals(p2);

    /// <summary>Whether two paddings differ.</summary>
    /// <param name="p1">The first padding.</param>
    /// <param name="p2">The second padding.</param>
    public static bool operator !=(Padding p1, Padding p2) => !p1.Equals(p2);

    /// <inheritdoc />
    public readonly bool Equals(Padding other) =>
        Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Padding other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    /// <inheritdoc />
    public override readonly string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{{Left={Left},Top={Top},Right={Right},Bottom={Bottom}}}");
}
