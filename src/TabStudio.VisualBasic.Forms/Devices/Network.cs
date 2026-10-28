using System.Net;
using System.Net.NetworkInformation;
using Microsoft.VisualBasic.FileIO;

namespace Microsoft.VisualBasic.Devices;

/// <summary>
/// The device's network (<c>My.Computer.Network</c>): whether it is connected, ping, and downloading and uploading
/// files over HTTP, HTTPS and <c>file:</c> addresses. Transfers never show a progress dialog on the tablet; they run
/// on the calling thread until they finish, as they do on Windows without the dialog.
/// </summary>
public class Network
{
    /// <summary>How long a transfer may wait, in milliseconds, unless the call says otherwise.</summary>
    private const int DefaultTimeout = 100000;

    /// <summary>How long a ping waits, in milliseconds, unless the call says otherwise.</summary>
    private const int DefaultPingTimeout = 500;

    private readonly object _gate = new();
    private readonly List<NetworkAvailableEventHandler> _handlers = [];
    private SynchronizationContext? _synchronizationContext;
    private bool _connected;
    private bool _listening;

    /// <summary>Creates the network object.</summary>
    public Network()
    {
    }

    /// <summary>
    /// Occurs when the device connects to or disconnects from a network, on the synchronization context of the code
    /// that added the first handler, such as a Windows Forms app's UI thread.
    /// </summary>
    public event NetworkAvailableEventHandler? NetworkAvailabilityChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_gate)
            {
                _handlers.Add(value);
                if (!_listening)
                {
                    _synchronizationContext = SynchronizationContext.Current;
                    _connected = IsAvailable;
                    _listening = Listen(true);
                }
            }
        }

        remove
        {
            lock (_gate)
            {
                if (value is not null && _handlers.Remove(value) && _handlers.Count == 0)
                {
                    DisconnectListenerLocked();
                }
            }
        }
    }

    /// <summary>
    /// Whether a network is available. When the device does not say, as some Android versions do not to apps, it is
    /// taken to be available.
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                return true;
            }
        }
    }

    /// <summary>Downloads a file to the device.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    public void DownloadFile(string address, string destinationFileName) =>
        DownloadFile(address, destinationFileName, string.Empty, string.Empty, showUI: false, DefaultTimeout, overwrite: false);

    /// <summary>Downloads a file to the device.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    public void DownloadFile(Uri address, string destinationFileName) =>
        DownloadFile(address, destinationFileName, string.Empty, string.Empty, showUI: false, DefaultTimeout, overwrite: false);

    /// <summary>Downloads a file to the device with a user name and password.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    public void DownloadFile(string address, string destinationFileName, string userName, string password) =>
        DownloadFile(address, destinationFileName, userName, password, showUI: false, DefaultTimeout, overwrite: false);

    /// <summary>Downloads a file to the device with a user name and password.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    public void DownloadFile(Uri address, string destinationFileName, string userName, string password) =>
        DownloadFile(address, destinationFileName, userName, password, showUI: false, DefaultTimeout, overwrite: false);

    /// <summary>Downloads a file to the device.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    /// <param name="userName">The user name, or empty.</param>
    /// <param name="password">The password, or empty.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    public void DownloadFile(string address, string destinationFileName, string userName, string password, bool showUI, int connectionTimeout, bool overwrite) =>
        DownloadFile(address, destinationFileName, userName, password, showUI, connectionTimeout, overwrite, UICancelOption.ThrowException);

    /// <summary>Downloads a file to the device.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    /// <param name="userName">The user name, or empty.</param>
    /// <param name="password">The password, or empty.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    /// <param name="onUserCancel">What cancelling the progress dialog does; there is none on the tablet.</param>
    public void DownloadFile(string address, string destinationFileName, string userName, string password, bool showUI, int connectionTimeout, bool overwrite, UICancelOption onUserCancel)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentNullException(nameof(address));
        }

        DownloadFile(GetUri(address.Trim()), destinationFileName, GetNetworkCredentials(userName, password), showUI, connectionTimeout, overwrite, onUserCancel);
    }

    /// <summary>Downloads a file to the device.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    /// <param name="userName">The user name, or empty.</param>
    /// <param name="password">The password, or empty.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    public void DownloadFile(Uri address, string destinationFileName, string userName, string password, bool showUI, int connectionTimeout, bool overwrite) =>
        DownloadFile(address, destinationFileName, userName, password, showUI, connectionTimeout, overwrite, UICancelOption.ThrowException);

    /// <summary>Downloads a file to the device.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    /// <param name="userName">The user name, or empty.</param>
    /// <param name="password">The password, or empty.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    /// <param name="onUserCancel">What cancelling the progress dialog does; there is none on the tablet.</param>
    public void DownloadFile(Uri address, string destinationFileName, string userName, string password, bool showUI, int connectionTimeout, bool overwrite, UICancelOption onUserCancel) =>
        DownloadFile(address, destinationFileName, GetNetworkCredentials(userName, password), showUI, connectionTimeout, overwrite, onUserCancel);

    /// <summary>Downloads a file to the device with credentials.</summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    /// <param name="networkCredentials">The credentials, or <see langword="null"/>.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    public void DownloadFile(Uri address, string destinationFileName, ICredentials networkCredentials, bool showUI, int connectionTimeout, bool overwrite) =>
        DownloadFile(address, destinationFileName, networkCredentials, showUI, connectionTimeout, overwrite, UICancelOption.ThrowException);

    /// <summary>
    /// Downloads a file to the device with credentials: <c>http</c> and <c>https</c> addresses, and <c>file</c>
    /// addresses, which are copied. The destination's folder is created when it does not exist.
    /// </summary>
    /// <param name="address">The file's address.</param>
    /// <param name="destinationFileName">Where to save it.</param>
    /// <param name="networkCredentials">The credentials, or <see langword="null"/>.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    /// <param name="onUserCancel">What cancelling the progress dialog does; there is none on the tablet.</param>
    /// <exception cref="ArgumentException">The timeout is not positive.</exception>
    /// <exception cref="IOException">The file exists and <paramref name="overwrite"/> is <see langword="false"/>.</exception>
    /// <exception cref="NotSupportedException">The address is not <c>http</c>, <c>https</c> or <c>file</c>.</exception>
    public void DownloadFile(Uri address, string destinationFileName, ICredentials? networkCredentials, bool showUI, int connectionTimeout, bool overwrite, UICancelOption onUserCancel)
    {
        if (connectionTimeout <= 0)
        {
            throw new ArgumentException("The ConnectionTimeout must be greater than 0.", nameof(connectionTimeout));
        }

        ArgumentNullException.ThrowIfNull(address);
        var fullFileName = NormalizeFilePath(destinationFileName, nameof(destinationFileName));
        if (Directory.Exists(fullFileName))
        {
            throw new InvalidOperationException("destinationFileName needs to include a file name.");
        }

        if (File.Exists(fullFileName) && !overwrite)
        {
            throw new IOException($"The file '{destinationFileName}' already exists.");
        }

        var directory = Path.GetDirectoryName(fullFileName);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("destinationFileName needs to include a file name.");
        }

        Directory.CreateDirectory(directory);
        if (address.IsFile)
        {
            File.Copy(address.LocalPath, fullFileName, overwrite);
            return;
        }

        RequireHttp(address, "My.Computer.Network.DownloadFile");
        Wait(() => DownloadAsync(address, fullFileName, networkCredentials, connectionTimeout));
    }

    /// <summary>Whether a computer answers a ping.</summary>
    /// <param name="hostNameOrAddress">The computer's name or IP address.</param>
    /// <returns>Whether it answered within half a second.</returns>
    public bool Ping(string hostNameOrAddress) => Ping(hostNameOrAddress, DefaultPingTimeout);

    /// <summary>Whether the computer of an address answers a ping.</summary>
    /// <param name="address">The address, whose host is pinged.</param>
    /// <returns>Whether it answered within half a second.</returns>
    public bool Ping(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return Ping(address.Host, DefaultPingTimeout);
    }

    /// <summary>Whether a computer answers a ping in time.</summary>
    /// <param name="hostNameOrAddress">The computer's name or IP address.</param>
    /// <param name="timeout">How long to wait, in milliseconds.</param>
    /// <exception cref="InvalidOperationException">No network is available.</exception>
    public bool Ping(string hostNameOrAddress, int timeout)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Unable to ping because a network connection is not available.");
        }

        using var ping = new System.Net.NetworkInformation.Ping();
        return ping.Send(hostNameOrAddress, timeout).Status == IPStatus.Success;
    }

    /// <summary>Whether the computer of an address answers a ping in time.</summary>
    /// <param name="address">The address, whose host is pinged.</param>
    /// <param name="timeout">How long to wait, in milliseconds.</param>
    public bool Ping(Uri address, int timeout)
    {
        ArgumentNullException.ThrowIfNull(address);
        return Ping(address.Host, timeout);
    }

    /// <summary>Uploads a file from the device.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    public void UploadFile(string sourceFileName, string address) =>
        UploadFile(sourceFileName, address, string.Empty, string.Empty, showUI: false, DefaultTimeout);

    /// <summary>Uploads a file from the device.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    public void UploadFile(string sourceFileName, Uri address) =>
        UploadFile(sourceFileName, address, string.Empty, string.Empty, showUI: false, DefaultTimeout);

    /// <summary>Uploads a file from the device with a user name and password.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    public void UploadFile(string sourceFileName, string address, string userName, string password) =>
        UploadFile(sourceFileName, address, userName, password, showUI: false, DefaultTimeout);

    /// <summary>Uploads a file from the device with a user name and password.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    public void UploadFile(string sourceFileName, Uri address, string userName, string password) =>
        UploadFile(sourceFileName, address, userName, password, showUI: false, DefaultTimeout);

    /// <summary>Uploads a file from the device.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    /// <param name="userName">The user name, or empty.</param>
    /// <param name="password">The password, or empty.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    public void UploadFile(string sourceFileName, string address, string userName, string password, bool showUI, int connectionTimeout) =>
        UploadFile(sourceFileName, address, userName, password, showUI, connectionTimeout, UICancelOption.ThrowException);

    /// <summary>Uploads a file from the device.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    /// <param name="userName">The user name, or empty.</param>
    /// <param name="password">The password, or empty.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="onUserCancel">What cancelling the progress dialog does; there is none on the tablet.</param>
    public void UploadFile(string sourceFileName, string address, string userName, string password, bool showUI, int connectionTimeout, UICancelOption onUserCancel)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentNullException(nameof(address));
        }

        UploadFile(sourceFileName, GetUri(address.Trim()), GetNetworkCredentials(userName, password), showUI, connectionTimeout, onUserCancel);
    }

    /// <summary>Uploads a file from the device.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    /// <param name="userName">The user name, or empty.</param>
    /// <param name="password">The password, or empty.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    public void UploadFile(string sourceFileName, Uri address, string userName, string password, bool showUI, int connectionTimeout) =>
        UploadFile(sourceFileName, address, userName, password, showUI, connectionTimeout, UICancelOption.ThrowException);

    /// <summary>Uploads a file from the device.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    /// <param name="userName">The user name, or empty.</param>
    /// <param name="password">The password, or empty.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="onUserCancel">What cancelling the progress dialog does; there is none on the tablet.</param>
    public void UploadFile(string sourceFileName, Uri address, string userName, string password, bool showUI, int connectionTimeout, UICancelOption onUserCancel) =>
        UploadFile(sourceFileName, address, GetNetworkCredentials(userName, password), showUI, connectionTimeout, onUserCancel);

    /// <summary>Uploads a file from the device with credentials.</summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    /// <param name="networkCredentials">The credentials, or <see langword="null"/>.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    public void UploadFile(string sourceFileName, Uri address, ICredentials networkCredentials, bool showUI, int connectionTimeout) =>
        UploadFile(sourceFileName, address, networkCredentials, showUI, connectionTimeout, UICancelOption.ThrowException);

    /// <summary>
    /// Uploads a file from the device with credentials: to an <c>http</c> or <c>https</c> address as a form's file field
    /// named <c>file</c> in a POST, as Windows does, or to a <c>file</c> address, which copies it.
    /// </summary>
    /// <param name="sourceFileName">The file.</param>
    /// <param name="address">Where to upload it.</param>
    /// <param name="networkCredentials">The credentials, or <see langword="null"/>.</param>
    /// <param name="showUI">Whether to show progress; the tablet shows none.</param>
    /// <param name="connectionTimeout">How long to wait, in milliseconds.</param>
    /// <param name="onUserCancel">What cancelling the progress dialog does; there is none on the tablet.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="ArgumentException">The timeout is not positive.</exception>
    /// <exception cref="NotSupportedException">The address is not <c>http</c>, <c>https</c> or <c>file</c>.</exception>
    public void UploadFile(string sourceFileName, Uri address, ICredentials? networkCredentials, bool showUI, int connectionTimeout, UICancelOption onUserCancel)
    {
        var fullFileName = NormalizeFilePath(sourceFileName, nameof(sourceFileName));
        if (!File.Exists(fullFileName))
        {
            throw new FileNotFoundException($"Could not find file '{sourceFileName}'.", sourceFileName);
        }

        if (connectionTimeout <= 0)
        {
            throw new ArgumentException("The ConnectionTimeout must be greater than 0.", nameof(connectionTimeout));
        }

        ArgumentNullException.ThrowIfNull(address);
        if (address.IsFile)
        {
            File.Copy(fullFileName, address.LocalPath, overwrite: true);
            return;
        }

        RequireHttp(address, "My.Computer.Network.UploadFile");
        Wait(() => UploadAsync(fullFileName, address, networkCredentials, connectionTimeout));
    }

    /// <summary>Stops listening for network changes.</summary>
    internal void DisconnectListener()
    {
        lock (_gate)
        {
            DisconnectListenerLocked();
        }
    }

    private static Uri GetUri(string address)
    {
        try
        {
            return new Uri(address);
        }
        catch (UriFormatException ex)
        {
            throw new ArgumentException(
                $"'{address}' is not a valid remote file address. A valid address should include a protocol, a path and a file name.",
                nameof(address),
                ex);
        }
    }

    private static NetworkCredential? GetNetworkCredentials(string? userName, string? password) =>
        string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(password) ? null : new NetworkCredential(userName, password);

    private static string NormalizeFilePath(string path, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, paramName);
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("The path must name a file, not a folder.", paramName);
        }

        return Path.GetFullPath(path);
    }

    private static void RequireHttp(Uri address, string member)
    {
        if (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
        {
            throw Unsupported.Member(member, $"'{address.Scheme}' addresses cannot be reached from the tablet. Use an http, https or file address.");
        }
    }

    /// <summary>Runs a transfer on a thread pool thread and waits for it, so a UI thread's synchronization context cannot deadlock it.</summary>
    private static void Wait(Func<Task> transfer) => Task.Run(transfer).GetAwaiter().GetResult();

    private static HttpClient CreateClient(ICredentials? credentials, int connectionTimeout)
    {
        // The client owns the handler and disposes it.
        var handler = new HttpClientHandler { Credentials = credentials };
        return new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromMilliseconds(connectionTimeout) };
    }

    private static async Task DownloadAsync(Uri address, string fullFileName, ICredentials? credentials, int connectionTimeout)
    {
        using var client = CreateClient(credentials, connectionTimeout);
        using var response = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var partial = fullFileName + ".download";
        using (var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(target).ConfigureAwait(false);
        }

        File.Move(partial, fullFileName, overwrite: true);
    }

    private static async Task UploadAsync(string fullFileName, Uri address, ICredentials? credentials, int connectionTimeout)
    {
        using var client = CreateClient(credentials, connectionTimeout);
        using var content = new MultipartFormDataContent();
        using var file = new FileStream(fullFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(fullFileName));
        using var response = await client.PostAsync(address, content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Listens or stops listening for the device's network address changes.</summary>
    /// <returns>Whether it listens.</returns>
    private bool Listen(bool listen)
    {
        try
        {
            if (listen)
            {
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            }
            else
            {
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            }

            return listen;
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // The device does not report changes to apps; the event then never occurs.
            return false;
        }
    }

    private void DisconnectListenerLocked()
    {
        if (_listening)
        {
            Listen(false);
            _listening = false;
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        NetworkAvailableEventHandler[] handlers;
        SynchronizationContext? context;
        bool available;
        lock (_gate)
        {
            available = IsAvailable;
            if (available == _connected)
            {
                return;
            }

            _connected = available;
            handlers = [.. _handlers];
            context = _synchronizationContext;
        }

        var args = new NetworkAvailableEventArgs(available);
        void Raise(object? state)
        {
            foreach (var handler in handlers)
            {
                handler(this, args);
            }
        }

        if (context is null)
        {
            Raise(null);
        }
        else
        {
            context.Post(Raise, null);
        }
    }
}
