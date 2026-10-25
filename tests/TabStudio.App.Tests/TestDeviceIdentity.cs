using TabStudio.App.Platform;

namespace TabStudio.App.Tests;

/// <summary>A device with a fixed name and kind, for TabStudio Link tests.</summary>
internal sealed class TestDeviceIdentity(string name, string kind) : IDeviceIdentity
{
    public string Name => name;

    public string Kind => kind;
}
