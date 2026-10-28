namespace Microsoft.VisualBasic.Devices;

/// <summary>Handles <see cref="Network.NetworkAvailabilityChanged"/>.</summary>
/// <param name="sender">The network object.</param>
/// <param name="e">Whether a network is available now.</param>
public delegate void NetworkAvailableEventHandler(object sender, NetworkAvailableEventArgs e);

/// <summary>The data of <see cref="Network.NetworkAvailabilityChanged"/>: whether a network is available now.</summary>
public class NetworkAvailableEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="networkAvailable">Whether a network is available.</param>
    public NetworkAvailableEventArgs(bool networkAvailable) => IsNetworkAvailable = networkAvailable;

    /// <summary>Whether a network is available.</summary>
    public bool IsNetworkAvailable { get; }
}
