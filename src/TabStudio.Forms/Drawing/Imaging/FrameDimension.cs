namespace System.Drawing.Imaging;

/// <summary>A dimension of an image's frames: time for animations, pages for documents.</summary>
/// <param name="guid">The dimension's identifier.</param>
public sealed class FrameDimension(Guid guid)
{
    /// <summary>The frames of an animation.</summary>
    public static FrameDimension Time { get; } = new(new Guid("6aedbd6d-3fb5-418a-83a6-7f45229dc872"));

    /// <summary>The resolutions of an image.</summary>
    public static FrameDimension Resolution { get; } = new(new Guid("84236f7b-3bd3-428f-8dab-4ea1439ca315"));

    /// <summary>The pages of a document.</summary>
    public static FrameDimension Page { get; } = new(new Guid("7462dc86-6180-4c7e-8e3f-ee7333a7a483"));

    /// <summary>The dimension's identifier.</summary>
    public Guid Guid { get; } = guid;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FrameDimension other && other.Guid == Guid;

    /// <inheritdoc />
    public override int GetHashCode() => Guid.GetHashCode();
}
