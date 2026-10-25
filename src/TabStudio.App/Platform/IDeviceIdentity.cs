using TabStudio.Link.Networking;

namespace TabStudio.App.Platform;

/// <summary>How this device introduces itself to other devices over TabStudio Link.</summary>
public interface IDeviceIdentity
{
    /// <summary>The device's name, such as <c>Pixel 9</c> or <c>Galaxy Tab S9</c>.</summary>
    string Name { get; }

    /// <summary>What the device is, one of <see cref="LinkDeviceKinds"/>.</summary>
    string Kind { get; }
}

/// <summary>The identity of the desktop build: the computer's name.</summary>
public sealed class DefaultDeviceIdentity : IDeviceIdentity
{
    /// <inheritdoc />
    public string Name { get; } = Environment.MachineName;

    /// <inheritdoc />
    public string Kind => LinkDeviceKinds.Computer;
}
