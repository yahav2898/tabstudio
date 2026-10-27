using System.Numerics;

namespace System.Drawing.Drawing2D;

/// <summary>Combines a brush's transformation with another, as the brushes' transform methods do.</summary>
internal static class BrushTransforms
{
    /// <summary>The transformation with an operation applied before or after it.</summary>
    /// <param name="current">The brush's transformation.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="order">Whether the operation applies first (prepend) or last (append).</param>
    public static Matrix3x2 Apply(Matrix3x2 current, Matrix3x2 operation, MatrixOrder order) =>
        order == MatrixOrder.Prepend ? operation * current : current * operation;
}
