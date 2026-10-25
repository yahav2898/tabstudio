namespace TabStudio.Link.Qr;

/// <summary>Reed-Solomon error correction over GF(256) with the QR code polynomial x⁸ + x⁴ + x³ + x² + 1.</summary>
internal static class ReedSolomon
{
    /// <summary>The error correction codewords of a block.</summary>
    /// <param name="data">The block's data codewords.</param>
    /// <param name="degree">How many error correction codewords to compute.</param>
    public static byte[] Compute(ReadOnlySpan<byte> data, int degree)
    {
        var divisor = Generator(degree);
        var result = new byte[degree];
        foreach (var value in data)
        {
            var factor = (byte)(value ^ result[0]);
            Array.Copy(result, 1, result, 0, degree - 1);
            result[degree - 1] = 0;
            for (var i = 0; i < degree; i++)
            {
                result[i] ^= Multiply(divisor[i], factor);
            }
        }

        return result;
    }

    /// <summary>The generator polynomial (x − α⁰)(x − α¹)…(x − α^(degree−1)), without its leading 1, highest power first.</summary>
    private static byte[] Generator(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        byte root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], root);
                if (j + 1 < degree)
                {
                    result[j] ^= result[j + 1];
                }
            }

            root = Multiply(root, 0x02);
        }

        return result;
    }

    /// <summary>Multiplies two elements of GF(256), reducing by 0x11D.</summary>
    private static byte Multiply(byte x, byte y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }

        return (byte)z;
    }
}
