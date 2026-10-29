using Microsoft.VisualBasic.MyServices;

namespace Microsoft.VisualBasic.Devices;

/// <summary>
/// The computer without its user interface (the base of <c>My.Computer</c>): its clock, file system, information,
/// name and network. On the tablet these are the device's.
/// </summary>
public class ServerComputer
{
    private static Clock? _clock;
    private ComputerInfo? _computerInfo;
    private FileSystemProxy? _fileIO;
    private Network? _network;
    private RegistryProxy? _registryInstance;

    /// <summary>Creates the computer object, as the My namespace does.</summary>
    public ServerComputer()
    {
    }

    /// <summary>The device's clock: local and UTC time, and the milliseconds since it started.</summary>
    public Clock Clock => _clock ??= new Clock();

    /// <summary>The device's files and folders, as <c>Microsoft.VisualBasic.FileIO.FileSystem</c> reaches them.</summary>
    public FileSystemProxy FileSystem => _fileIO ??= new FileSystemProxy();

    /// <summary>The device's operating system, memory and installed culture.</summary>
    public ComputerInfo Info => _computerInfo ??= new ComputerInfo();

    /// <summary>The device's name.</summary>
    public string Name => Environment.MachineName;

    /// <summary>The device's network: whether it is connected, ping, and downloading and uploading files.</summary>
    public Network Network => _network ??= new Network();

    /// <summary>The Windows registry, which the tablet does not have: every member of it throws <see cref="NotSupportedException"/>.</summary>
    public RegistryProxy Registry => _registryInstance ??= new RegistryProxy();
}
