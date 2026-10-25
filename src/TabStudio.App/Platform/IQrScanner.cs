namespace TabStudio.App.Platform;

/// <summary>Reads a QR code with the device's camera, to pair with a TabStudio Link host.</summary>
public interface IQrScanner
{
    /// <summary>Whether this device can scan: it has a camera. Without one, pairing uses the typed code.</summary>
    bool IsAvailable { get; }

    /// <summary>Shows the camera until a QR code is read or the user goes back.</summary>
    /// <param name="cancellationToken">Closes the camera.</param>
    /// <returns>The QR code's text, or <see langword="null"/> when the user went back.</returns>
    /// <exception cref="UnauthorizedAccessException">The user did not allow TabStudio to use the camera.</exception>
    /// <exception cref="InvalidOperationException">The camera could not start, for example because another app uses it.</exception>
    Task<string?> ScanAsync(CancellationToken cancellationToken);
}

/// <summary>The scanner of devices without a camera, such as the desktop build: pairing uses the typed code.</summary>
public sealed class NoQrScanner : IQrScanner
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public Task<string?> ScanAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
