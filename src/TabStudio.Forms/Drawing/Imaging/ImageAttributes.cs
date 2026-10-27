namespace System.Drawing.Imaging;

/// <summary>How the colors of an image change as it is drawn: a color matrix, a transparent color range, gamma.</summary>
public sealed class ImageAttributes : ICloneable, IDisposable
{
    private ColorMatrix? _matrix;
    private (Color Low, Color High)? _colorKey;
    private float _gamma = 1;

    /// <summary>Sets the color matrix.</summary>
    /// <param name="newColorMatrix">The matrix.</param>
    public void SetColorMatrix(ColorMatrix newColorMatrix) => _matrix = newColorMatrix ?? throw new ArgumentNullException(nameof(newColorMatrix));

    /// <summary>Sets the color matrix.</summary>
    /// <param name="newColorMatrix">The matrix.</param>
    /// <param name="flags">Which colors it applies to: all.</param>
    public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag flags) => SetColorMatrix(newColorMatrix);

    /// <summary>Sets the color matrix.</summary>
    /// <param name="newColorMatrix">The matrix.</param>
    /// <param name="mode">Which colors it applies to: all.</param>
    /// <param name="type">What it applies to: images.</param>
    public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag mode, ColorAdjustType type) => SetColorMatrix(newColorMatrix);

    /// <summary>Clears the color matrix.</summary>
    public void ClearColorMatrix() => _matrix = null;

    /// <summary>Makes colors between two colors transparent.</summary>
    /// <param name="colorLow">The lowest color.</param>
    /// <param name="colorHigh">The highest color.</param>
    public void SetColorKey(Color colorLow, Color colorHigh) => _colorKey = (colorLow, colorHigh);

    /// <summary>Makes colors between two colors transparent.</summary>
    /// <param name="colorLow">The lowest color.</param>
    /// <param name="colorHigh">The highest color.</param>
    /// <param name="type">What it applies to: images.</param>
    public void SetColorKey(Color colorLow, Color colorHigh, ColorAdjustType type) => SetColorKey(colorLow, colorHigh);

    /// <summary>Clears the transparent color range.</summary>
    public void ClearColorKey() => _colorKey = null;

    /// <summary>Sets the gamma correction.</summary>
    /// <param name="gamma">The gamma, from 0.1 to 5.</param>
    public void SetGamma(float gamma) => _gamma = Math.Clamp(gamma, 0.1f, 5f);

    /// <summary>Sets the gamma correction.</summary>
    /// <param name="gamma">The gamma.</param>
    /// <param name="type">What it applies to: images.</param>
    public void SetGamma(float gamma, ColorAdjustType type) => SetGamma(gamma);

    /// <summary>Clears the gamma correction.</summary>
    public void ClearGamma() => _gamma = 1;

    /// <summary>Sets how an image is tiled beyond its rectangle: images are drawn once.</summary>
    /// <param name="mode">The wrap mode.</param>
    public void SetWrapMode(Drawing2D.WrapMode mode)
    {
        // Images are drawn once into their rectangle.
    }

    /// <summary>Sets how an image is tiled beyond its rectangle: images are drawn once.</summary>
    /// <param name="mode">The wrap mode.</param>
    /// <param name="color">The color outside the image.</param>
    public void SetWrapMode(Drawing2D.WrapMode mode, Color color)
    {
        // Images are drawn once into their rectangle.
    }

    /// <summary>Whether the attributes change any color.</summary>
    internal bool ChangesColors => _matrix is not null || _colorKey is not null || Math.Abs(_gamma - 1) > 0.001f;

    /// <summary>Changes pixels as drawing the image with these attributes does.</summary>
    /// <param name="pixels">Unpremultiplied 0xAARRGGBB pixels, changed in place.</param>
    internal void Apply(int[] pixels)
    {
        byte[]? gamma = null;
        if (Math.Abs(_gamma - 1) > 0.001f)
        {
            gamma = new byte[256];
            for (var i = 0; i < 256; i++)
            {
                gamma[i] = (byte)Math.Clamp(Math.Round(255 * Math.Pow(i / 255.0, _gamma)), 0, 255);
            }
        }

        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            if (_colorKey is { } key && InRange(c, key.Low, key.High))
            {
                pixels[i] = 0;
                continue;
            }

            if (_matrix is { } matrix)
            {
                c = matrix.Apply(c);
            }

            if (gamma is not null)
            {
                c = (c & unchecked((int)0xFF000000)) | (gamma[(c >> 16) & 0xFF] << 16) | (gamma[(c >> 8) & 0xFF] << 8) | gamma[c & 0xFF];
            }

            pixels[i] = c;
        }
    }

    /// <inheritdoc />
    public object Clone() => MemberwiseClone();

    /// <inheritdoc />
    public void Dispose()
    {
        // Attributes hold nothing to release.
    }

    private static bool InRange(int c, Color low, Color high)
    {
        var (r, g, b) = ((c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF);
        return r >= low.R && r <= high.R && g >= low.G && g <= high.G && b >= low.B && b <= high.B;
    }
}
