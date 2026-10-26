using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>Inputs for the ArduinoOTA protocol. Firmware must already contain ArduinoOTA support and a suitable OTA partition layout.</summary>
/// <param name="FirmwarePath">Application BIN; bootloaders and partition tables are not OTA application images.</param>
/// <param name="Target">Selected board's Wi-Fi IP address and ArduinoOTA port, normally 3232.</param>
/// <param name="Password">Password held in memory for this upload; never logged.</param>
/// <param name="LegacyMd5Password">Explicit compatibility with firmware that stores an MD5 password hash even with the newer challenge protocol.</param>
public sealed record ArduinoOtaRequest(string FirmwarePath, IPEndPoint Target, string Password = "", bool LegacyMd5Password = false);

/// <summary>Uploads application firmware through the real ArduinoOTA UDP invitation and reverse TCP transfer protocol.</summary>
/// <remarks>
/// Implements old MD5 and Arduino-ESP32 3.3.1+ PBKDF2-HMAC-SHA256 authentication as documented by upstream tools/espota.py.
/// The protocol's MD5 image checksum is retained for interoperability; success requires the target's final OK.
/// It provides no transport encryption or physical readback, and does not install initial OTA firmware.
/// </remarks>
public static class ArduinoOtaUploader
{
    /// <summary>Creates an authentication response for a validated 32/64-hex-digit nonce and matching client nonce.</summary>
#pragma warning disable CA5351 // Legacy ArduinoOTA and its image checksum require MD5 for protocol compatibility.
    public static string CreateAuthenticationResponse(string password, string nonce, string clientNonce, bool legacyMd5Password = false)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(clientNonce);
        if (nonce.Length is not (32 or 64) || clientNonce.Length != nonce.Length || !nonce.All(Uri.IsHexDigit) || !clientNonce.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("ArduinoOTA requires matching 32- or 64-hex-digit challenge nonces.");
        }
        static string Md5(string value) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(value)));
        static string Sha256(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        string response;
        if (nonce.Length == 32)
        {
            response = Md5(Md5(password) + ":" + nonce + ":" + clientNonce);
        }
        else
        {
            var passwordHash = legacyMd5Password ? Md5(password) : Sha256(password);
            var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passwordHash), Encoding.UTF8.GetBytes(nonce + ":" + clientNonce), 10000, HashAlgorithmName.SHA256, 32);
            response = Sha256(Convert.ToHexStringLower(key) + ":" + nonce + ":" + clientNonce);
            CryptographicOperations.ZeroMemory(key);
        }
        return "200 " + clientNonce + " " + response + "\n";
    }

    /// <summary>Transfers an image, requiring explicit final acceptance. Cancellation or a missing acknowledgment never reports success.</summary>
    public static async Task<EmbeddedFlashResult> UploadAsync(ArduinoOtaRequest request, IProgress<double>? progress = null,
        TimeSpan? operationTimeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var timeout = operationTimeout ?? TimeSpan.FromSeconds(10);
        if (timeout < TimeSpan.FromSeconds(1) || timeout > TimeSpan.FromSeconds(60) || request.Target.Port is < 1 or > 65535 ||
            request.Target.Address.Equals(IPAddress.Any) || request.Target.Address.Equals(IPAddress.IPv6Any))
        {
            throw new ArgumentException("Select the board's Wi-Fi address, OTA port and a 1..60 second operation timeout.", nameof(request));
        }
        if (!string.Equals(Path.GetExtension(request.FirmwarePath), ".bin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ArduinoOTA application uploads require a BIN image.", nameof(request));
        }
        await using var firmware = new FileStream(request.FirmwarePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (firmware.Length is <= 0 or > 128 * 1024 * 1024)
        {
            throw new InvalidDataException("OTA firmware must contain 1 byte..128 MiB.");
        }
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var md5 = Convert.ToHexStringLower(await MD5.HashDataAsync(firmware, cancellationToken).ConfigureAwait(false));
        firmware.Position = 0;
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(firmware, cancellationToken).ConfigureAwait(false));
        firmware.Position = 0;
        using var udp = new UdpClient(request.Target.AddressFamily);
        udp.Connect(request.Target);
        var localAddress = ((IPEndPoint)udp.Client.LocalEndPoint!).Address;
        var listener = new TcpListener(localAddress, 0);
        listener.Start(1);
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var invitation = string.Create(CultureInfo.InvariantCulture, $"0 {port} {firmware.Length} {md5}\n");
            await udp.SendAsync(Encoding.ASCII.GetBytes(invitation), cancellationToken).ConfigureAwait(false);
            var response = await ReceiveUdpAsync(udp, timeout, cancellationToken).ConfigureAwait(false);
            if (response.StartsWith("AUTH ", StringComparison.Ordinal))
            {
                var nonce = response[5..].Trim();
                if (nonce.Length is not (32 or 64) || !nonce.All(Uri.IsHexDigit))
                {
                    throw new InvalidDataException("Invalid ArduinoOTA authentication challenge.");
                }
                var clientNonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(nonce.Length / 2));
                var answer = CreateAuthenticationResponse(request.Password, nonce, clientNonce, request.LegacyMd5Password);
                await udp.SendAsync(Encoding.ASCII.GetBytes(answer), cancellationToken).ConfigureAwait(false);
                response = await ReceiveUdpAsync(udp, timeout, cancellationToken).ConfigureAwait(false);
            }
            if (response != "OK")
            {
                throw new IOException("ArduinoOTA invitation or authentication was rejected.");
            }
            using var acceptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            acceptTimeout.CancelAfter(timeout);
            using var client = await listener.AcceptTcpClientAsync(acceptTimeout.Token).ConfigureAwait(false);
            if (client.Client.RemoteEndPoint is not IPEndPoint peer || !peer.Address.Equals(request.Target.Address))
            {
                throw new IOException("OTA connection came from a different address than the selected board.");
            }
            await using var connection = client.GetStream();
            var buffer = new byte[1024];
            var acknowledgment = new byte[64];
            var finalText = new StringBuilder();
            long sent = 0;
            while (await firmware.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) is var count && count > 0)
            {
                using var transferTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                transferTimeout.CancelAfter(timeout);
                await connection.WriteAsync(buffer.AsMemory(0, count), transferTimeout.Token).ConfigureAwait(false);
                var received = await connection.ReadAsync(acknowledgment, transferTimeout.Token).ConfigureAwait(false);
                if (received == 0)
                {
                    throw new IOException("The board disconnected before acknowledging the upload.");
                }
                finalText.Append(Encoding.ASCII.GetString(acknowledgment, 0, received));
                if (finalText.Length > 256)
                {
                    finalText.Remove(0, finalText.Length - 256);
                }
                sent += count;
                progress?.Report((double)sent / firmware.Length);
            }
            using var finalTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            finalTimeout.CancelAfter(timeout);
            while (!IsSuccessAcknowledgment(finalText.ToString()))
            {
                var received = await connection.ReadAsync(acknowledgment, finalTimeout.Token).ConfigureAwait(false);
                if (received == 0)
                {
                    throw new IOException("The board disconnected without final OTA verification.");
                }
                finalText.Append(Encoding.ASCII.GetString(acknowledgment, 0, received));
                if (finalText.Length > 256)
                {
                    throw new IOException("The board did not return a valid final OTA acknowledgment.");
                }
            }
            return new(sha256, elapsed.Elapsed, 1);
        }
        finally
        {
            listener.Stop();
        }
    }
#pragma warning restore CA5351

    private static bool IsSuccessAcknowledgment(string text)
    {
        var value = text.Trim();
        if (!value.EndsWith("OK", StringComparison.Ordinal))
        {
            return false;
        }
        // A TCP read can coalesce decimal chunk acknowledgments and the final OK.
        return value[..^2].All(c => char.IsAsciiDigit(c) || char.IsWhiteSpace(c));
    }

    private static async Task<string> ReceiveUdpAsync(UdpClient udp, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var response = await udp.ReceiveAsync(deadline.Token).ConfigureAwait(false);
        if (response.Buffer.Length > 256)
        {
            throw new InvalidDataException("OTA response exceeds its protocol bound.");
        }
        return Encoding.ASCII.GetString(response.Buffer).Trim();
    }
}
