namespace System.Drawing.Imaging;

/// <summary>
/// A 5 by 5 matrix that transforms colors: a color is the row vector (r, g, b, a, 1), each from 0 to 1, multiplied
/// by the matrix. Used with <see cref="ImageAttributes.SetColorMatrix(ColorMatrix)"/>, for grayscale, sepia or
/// translucent images.
/// </summary>
public sealed class ColorMatrix
{
    private readonly float[][] _matrix;

    /// <summary>Creates the identity matrix.</summary>
    public ColorMatrix()
    {
        _matrix = [new float[5], new float[5], new float[5], new float[5], new float[5]];
        for (var i = 0; i < 5; i++)
        {
            _matrix[i][i] = 1;
        }
    }

    /// <summary>Creates a matrix of rows.</summary>
    /// <param name="newColorMatrix">Five rows of five values.</param>
    public ColorMatrix(float[][] newColorMatrix)
        : this()
    {
        ArgumentNullException.ThrowIfNull(newColorMatrix);
        for (var i = 0; i < Math.Min(5, newColorMatrix.Length); i++)
        {
            for (var j = 0; j < Math.Min(5, newColorMatrix[i]?.Length ?? 0); j++)
            {
                _matrix[i][j] = newColorMatrix[i][j];
            }
        }
    }

    /// <summary>A value of the matrix.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    public float this[int row, int column]
    {
        get => _matrix[row][column];
        set => _matrix[row][column] = value;
    }

#pragma warning disable CA1707 // GDI+'s own names.
    /// <summary>Row 0, column 0.</summary>
    public float Matrix00 { get => _matrix[0][0]; set => _matrix[0][0] = value; }

    /// <summary>Row 0, column 1.</summary>
    public float Matrix01 { get => _matrix[0][1]; set => _matrix[0][1] = value; }

    /// <summary>Row 0, column 2.</summary>
    public float Matrix02 { get => _matrix[0][2]; set => _matrix[0][2] = value; }

    /// <summary>Row 0, column 3.</summary>
    public float Matrix03 { get => _matrix[0][3]; set => _matrix[0][3] = value; }

    /// <summary>Row 0, column 4.</summary>
    public float Matrix04 { get => _matrix[0][4]; set => _matrix[0][4] = value; }

    /// <summary>Row 1, column 0.</summary>
    public float Matrix10 { get => _matrix[1][0]; set => _matrix[1][0] = value; }

    /// <summary>Row 1, column 1.</summary>
    public float Matrix11 { get => _matrix[1][1]; set => _matrix[1][1] = value; }

    /// <summary>Row 1, column 2.</summary>
    public float Matrix12 { get => _matrix[1][2]; set => _matrix[1][2] = value; }

    /// <summary>Row 1, column 3.</summary>
    public float Matrix13 { get => _matrix[1][3]; set => _matrix[1][3] = value; }

    /// <summary>Row 1, column 4.</summary>
    public float Matrix14 { get => _matrix[1][4]; set => _matrix[1][4] = value; }

    /// <summary>Row 2, column 0.</summary>
    public float Matrix20 { get => _matrix[2][0]; set => _matrix[2][0] = value; }

    /// <summary>Row 2, column 1.</summary>
    public float Matrix21 { get => _matrix[2][1]; set => _matrix[2][1] = value; }

    /// <summary>Row 2, column 2.</summary>
    public float Matrix22 { get => _matrix[2][2]; set => _matrix[2][2] = value; }

    /// <summary>Row 2, column 3.</summary>
    public float Matrix23 { get => _matrix[2][3]; set => _matrix[2][3] = value; }

    /// <summary>Row 2, column 4.</summary>
    public float Matrix24 { get => _matrix[2][4]; set => _matrix[2][4] = value; }

    /// <summary>Row 3, column 0.</summary>
    public float Matrix30 { get => _matrix[3][0]; set => _matrix[3][0] = value; }

    /// <summary>Row 3, column 1.</summary>
    public float Matrix31 { get => _matrix[3][1]; set => _matrix[3][1] = value; }

    /// <summary>Row 3, column 2.</summary>
    public float Matrix32 { get => _matrix[3][2]; set => _matrix[3][2] = value; }

    /// <summary>Row 3, column 3: the alpha scale.</summary>
    public float Matrix33 { get => _matrix[3][3]; set => _matrix[3][3] = value; }

    /// <summary>Row 3, column 4.</summary>
    public float Matrix34 { get => _matrix[3][4]; set => _matrix[3][4] = value; }

    /// <summary>Row 4, column 0: the red offset.</summary>
    public float Matrix40 { get => _matrix[4][0]; set => _matrix[4][0] = value; }

    /// <summary>Row 4, column 1: the green offset.</summary>
    public float Matrix41 { get => _matrix[4][1]; set => _matrix[4][1] = value; }

    /// <summary>Row 4, column 2: the blue offset.</summary>
    public float Matrix42 { get => _matrix[4][2]; set => _matrix[4][2] = value; }

    /// <summary>Row 4, column 3: the alpha offset.</summary>
    public float Matrix43 { get => _matrix[4][3]; set => _matrix[4][3] = value; }

    /// <summary>Row 4, column 4.</summary>
    public float Matrix44 { get => _matrix[4][4]; set => _matrix[4][4] = value; }
#pragma warning restore CA1707

    /// <summary>Transforms one unpremultiplied 0xAARRGGBB color.</summary>
    /// <param name="argb">The color.</param>
    internal int Apply(int argb)
    {
        Span<float> v = [((argb >> 16) & 0xFF) / 255f, ((argb >> 8) & 0xFF) / 255f, (argb & 0xFF) / 255f, ((argb >> 24) & 0xFF) / 255f, 1];
        Span<int> result = stackalloc int[4];
        for (var j = 0; j < 4; j++)
        {
            var sum = 0f;
            for (var i = 0; i < 5; i++)
            {
                sum += v[i] * _matrix[i][j];
            }

            result[j] = (int)Math.Clamp(MathF.Round(sum * 255), 0, 255);
        }

        return (result[3] << 24) | (result[0] << 16) | (result[1] << 8) | result[2];
    }
}
