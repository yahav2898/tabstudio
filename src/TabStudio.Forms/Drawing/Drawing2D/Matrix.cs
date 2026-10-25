using System.Numerics;

namespace System.Drawing.Drawing2D;

/// <summary>Whether a transformation is applied before or after the existing one.</summary>
public enum MatrixOrder
{
    /// <summary>The new operation is applied before the existing transformation.</summary>
    Prepend = 0,

    /// <summary>The new operation is applied after the existing transformation.</summary>
    Append = 1,
}

/// <summary>A 3×2 affine matrix that transforms points: translation, scaling, rotation and shearing.</summary>
public sealed class Matrix : MarshalByRefObject, IDisposable
{
    /// <summary>Creates an identity matrix.</summary>
    public Matrix() => Value = Matrix3x2.Identity;

    /// <summary>Creates a matrix from its elements.</summary>
    /// <param name="m11">The first row, first column.</param>
    /// <param name="m12">The first row, second column.</param>
    /// <param name="m21">The second row, first column.</param>
    /// <param name="m22">The second row, second column.</param>
    /// <param name="dx">The horizontal translation.</param>
    /// <param name="dy">The vertical translation.</param>
    public Matrix(float m11, float m12, float m21, float m22, float dx, float dy) => Value = new Matrix3x2(m11, m12, m21, m22, dx, dy);

    /// <summary>Creates a matrix from a <see cref="Matrix3x2"/>.</summary>
    /// <param name="matrix">The matrix.</param>
    public Matrix(Matrix3x2 matrix) => Value = matrix;

    /// <summary>Creates the matrix that maps a rectangle to a parallelogram given by three points.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="plgpts">The upper-left, upper-right and lower-left corners of the parallelogram.</param>
    public Matrix(RectangleF rect, PointF[] plgpts)
    {
        ArgumentNullException.ThrowIfNull(plgpts);
        if (plgpts.Length != 3)
        {
            throw new ArgumentException("Three points are required.", nameof(plgpts));
        }

        var m11 = (plgpts[1].X - plgpts[0].X) / rect.Width;
        var m12 = (plgpts[1].Y - plgpts[0].Y) / rect.Width;
        var m21 = (plgpts[2].X - plgpts[0].X) / rect.Height;
        var m22 = (plgpts[2].Y - plgpts[0].Y) / rect.Height;
        Value = new Matrix3x2(m11, m12, m21, m22, plgpts[0].X - (m11 * rect.X) - (m21 * rect.Y), plgpts[0].Y - (m12 * rect.X) - (m22 * rect.Y));
    }

    /// <summary>The matrix as a <see cref="Matrix3x2"/>.</summary>
    public Matrix3x2 MatrixElements
    {
        get => Value;
        set => Value = value;
    }

    /// <summary>The six elements: m11, m12, m21, m22, dx and dy.</summary>
    public float[] Elements => [Value.M11, Value.M12, Value.M21, Value.M22, Value.M31, Value.M32];

    /// <summary>The horizontal translation.</summary>
    public float OffsetX => Value.M31;

    /// <summary>The vertical translation.</summary>
    public float OffsetY => Value.M32;

    /// <summary>Whether this is the identity matrix.</summary>
    public bool IsIdentity => Value.IsIdentity;

    /// <summary>Whether the matrix can be inverted.</summary>
    public bool IsInvertible => Matrix3x2.Invert(Value, out _);

    internal Matrix3x2 Value { get; private set; }

    /// <summary>Resets to the identity matrix.</summary>
    public void Reset() => Value = Matrix3x2.Identity;

    /// <summary>Applies a translation before this transformation.</summary>
    /// <param name="offsetX">The horizontal offset.</param>
    /// <param name="offsetY">The vertical offset.</param>
    public void Translate(float offsetX, float offsetY) => Translate(offsetX, offsetY, MatrixOrder.Prepend);

    /// <summary>Applies a translation.</summary>
    /// <param name="offsetX">The horizontal offset.</param>
    /// <param name="offsetY">The vertical offset.</param>
    /// <param name="order">Whether it is applied before or after this transformation.</param>
    public void Translate(float offsetX, float offsetY, MatrixOrder order) => Apply(Matrix3x2.CreateTranslation(offsetX, offsetY), order);

    /// <summary>Applies a scaling before this transformation.</summary>
    /// <param name="scaleX">The horizontal factor.</param>
    /// <param name="scaleY">The vertical factor.</param>
    public void Scale(float scaleX, float scaleY) => Scale(scaleX, scaleY, MatrixOrder.Prepend);

    /// <summary>Applies a scaling.</summary>
    /// <param name="scaleX">The horizontal factor.</param>
    /// <param name="scaleY">The vertical factor.</param>
    /// <param name="order">Whether it is applied before or after this transformation.</param>
    public void Scale(float scaleX, float scaleY, MatrixOrder order) => Apply(Matrix3x2.CreateScale(scaleX, scaleY), order);

    /// <summary>Applies a clockwise rotation about the origin before this transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    public void Rotate(float angle) => Rotate(angle, MatrixOrder.Prepend);

    /// <summary>Applies a clockwise rotation about the origin.</summary>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="order">Whether it is applied before or after this transformation.</param>
    public void Rotate(float angle, MatrixOrder order) => Apply(Matrix3x2.CreateRotation(angle * MathF.PI / 180), order);

    /// <summary>Applies a clockwise rotation about a point before this transformation.</summary>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="point">The center of rotation.</param>
    public void RotateAt(float angle, PointF point) => RotateAt(angle, point, MatrixOrder.Prepend);

    /// <summary>Applies a clockwise rotation about a point.</summary>
    /// <param name="angle">The angle in degrees.</param>
    /// <param name="point">The center of rotation.</param>
    /// <param name="order">Whether it is applied before or after this transformation.</param>
    public void RotateAt(float angle, PointF point, MatrixOrder order) =>
        Apply(Matrix3x2.CreateRotation(angle * MathF.PI / 180, new Vector2(point.X, point.Y)), order);

    /// <summary>Applies a shear before this transformation.</summary>
    /// <param name="shearX">The horizontal shear factor.</param>
    /// <param name="shearY">The vertical shear factor.</param>
    public void Shear(float shearX, float shearY) => Shear(shearX, shearY, MatrixOrder.Prepend);

    /// <summary>Applies a shear.</summary>
    /// <param name="shearX">The horizontal shear factor.</param>
    /// <param name="shearY">The vertical shear factor.</param>
    /// <param name="order">Whether it is applied before or after this transformation.</param>
    public void Shear(float shearX, float shearY, MatrixOrder order) => Apply(new Matrix3x2(1, shearY, shearX, 1, 0, 0), order);

    /// <summary>Multiplies this matrix by another, the other applied first.</summary>
    /// <param name="matrix">The other matrix.</param>
    public void Multiply(Matrix matrix) => Multiply(matrix, MatrixOrder.Prepend);

    /// <summary>Multiplies this matrix by another.</summary>
    /// <param name="matrix">The other matrix.</param>
    /// <param name="order">Whether the other is applied before or after this transformation.</param>
    public void Multiply(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        Apply(matrix.Value, order);
    }

    /// <summary>Inverts the matrix.</summary>
    /// <exception cref="ArgumentException">The matrix cannot be inverted.</exception>
    public void Invert()
    {
        if (!Matrix3x2.Invert(Value, out var inverse))
        {
            throw new ArgumentException("The matrix cannot be inverted.");
        }

        Value = inverse;
    }

    /// <summary>Transforms points in place.</summary>
    /// <param name="pts">The points.</param>
    public void TransformPoints(PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (var i = 0; i < pts.Length; i++)
        {
            var v = Vector2.Transform(new Vector2(pts[i].X, pts[i].Y), Value);
            pts[i] = new PointF(v.X, v.Y);
        }
    }

    /// <summary>Transforms points in place, rounding to whole pixels.</summary>
    /// <param name="pts">The points.</param>
    public void TransformPoints(Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (var i = 0; i < pts.Length; i++)
        {
            var v = Vector2.Transform(new Vector2(pts[i].X, pts[i].Y), Value);
            pts[i] = new Point((int)MathF.Round(v.X), (int)MathF.Round(v.Y));
        }
    }

    /// <summary>Transforms vectors in place, ignoring the translation.</summary>
    /// <param name="pts">The vectors.</param>
    public void TransformVectors(PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (var i = 0; i < pts.Length; i++)
        {
            var v = Vector2.TransformNormal(new Vector2(pts[i].X, pts[i].Y), Value);
            pts[i] = new PointF(v.X, v.Y);
        }
    }

    /// <summary>Creates a copy.</summary>
    public Matrix Clone() => new(Value);

    /// <inheritdoc />
    public void Dispose()
    {
        // A matrix holds nothing to release.
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Matrix other && other.Value == Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    private void Apply(Matrix3x2 operation, MatrixOrder order) =>
        Value = order == MatrixOrder.Prepend ? operation * Value : Value * operation;
}
