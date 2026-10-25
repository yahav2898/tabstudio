using System.Net;
using System.Net.Sockets;
using System.Text;
using TabStudio.Swarm.Coordinator;
using TabStudio.Swarm.Web;

namespace TabStudio.Swarm.Tests;

/// <summary>
/// Web apps on the swarm: the tablet serves the app once, on its loopback address, and each device opens it through
/// its own loopback gateway, which requires the run's secret and carries every connection inside the swarm's
/// encrypted connection.
/// </summary>
public sealed class WebTunnelTests
{
    [Fact]
    public async Task A_device_opens_the_tablet_web_app_through_the_tunnel_with_its_secret()
    {
        await using var swarm = new SwarmLoopback();
        var phone = await swarm.JoinAsync("Pixel 9");
        using var server = new TinyWebServer("Hello from the tablet's web app");

        await swarm.Coordinator.RunAsync(
            new SwarmBuild
            {
                AppName = "HelloWeb",
                Kind = SwarmAppKind.Web,
                Entry = TestPrograms.Library("public static class Web { }", "HelloWeb"),
                Web = new SwarmWebTarget(server.Address, "/todos"),
            },
            CancellationToken.None);
        await SwarmLoopback.UntilAsync(() => phone.Host.Current is not null, "the web view");
        var address = phone.Host.Current!.App.Address!;

        Assert.Equal("127.0.0.1", address.Host);
        Assert.Equal("/todos", address.AbsolutePath);
        Assert.Contains($"{SwarmProtocol.WebSecretParameter}=", address.Query, StringComparison.Ordinal);

        using var browser = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = true });
        var page = await browser.GetStringAsync(address);
        Assert.Equal("Hello from the tablet's web app", page);
        Assert.Equal("/todos", server.LastTarget);
        var again = await browser.GetStringAsync(new Uri(address, "/todos/1"));
        Assert.Equal("Hello from the tablet's web app", again);
        Assert.Equal("/todos/1", server.LastTarget);

        // Another app on the device, without the secret, is refused.
        using var stranger = new HttpClient();
        var refused = await stranger.GetAsync(new Uri(address, "/todos"));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        var guessed = await stranger.GetAsync(new Uri(address, $"/todos?{SwarmProtocol.WebSecretParameter}=guess"));
        Assert.Equal(HttpStatusCode.Forbidden, guessed.StatusCode);
    }

    [Theory]
    [InlineData("/?tabstudio-swarm=s3cret", true, "/")]
    [InlineData("/todos?page=2&tabstudio-swarm=s3cret", true, "/todos?page=2")]
    [InlineData("/todos?tabstudio-swarm=wrong", false, "/todos")]
    [InlineData("/todos?page=2", false, "/todos?page=2")]
    public void The_gateway_trades_the_secret_in_the_first_page_for_a_cookie(string target, bool hasSecret, string redirect)
    {
        Assert.Equal(hasSecret, SwarmWebGateway.TryTakeSecretFromQuery(target, "s3cret", out var withoutSecret));
        Assert.Equal(redirect, withoutSecret);
    }

    [Theory]
    [InlineData(" theme=dark; tabstudio-swarm=s3cret", true)]
    [InlineData(" tabstudio-swarm=s3cre", false)]
    [InlineData(" tabstudio-swarm-other=s3cret", false)]
    public void Later_requests_need_the_cookie(string header, bool accepted)
    {
        Assert.Equal(accepted, SwarmWebGateway.CookieHasSecret([header], "s3cret"));
    }

    /// <summary>A web server that answers every request with the same page, as the app the tablet serves.</summary>
    private sealed class TinyWebServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly string _page;

        public TinyWebServer(string page)
        {
            _page = page;
            _listener.Start();
            Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
            _ = Task.Run(ServeAsync);
        }

        public Uri Address { get; }

        public string? LastTarget { get; private set; }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }

        private async Task ServeAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }

                _ = Task.Run(() => AnswerAsync(client));
            }
        }

        private async Task AnswerAsync(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                var head = new StringBuilder();
                var buffer = new byte[4096];
                while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer);
                    if (read == 0)
                    {
                        return;
                    }

                    head.Append(Encoding.Latin1.GetString(buffer, 0, read));
                }

                LastTarget = head.ToString().Split(' ')[1];
                var body = Encoding.UTF8.GetBytes(_page);
                var response = Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response);
                await stream.WriteAsync(body);
            }
        }
    }
}
