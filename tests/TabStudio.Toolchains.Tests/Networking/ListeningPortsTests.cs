using System.Net;
using System.Net.Sockets;
using TabStudio.Toolchains.Networking;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Networking;

public sealed class ListeningPortsTests
{
    [Theory]
    [InlineData("0100007F:1388", "0A", 5000)]
    [InlineData("00000000:1F90", "0A", 8080)]
    [InlineData("00000000000000000000000001000000:0BB8", "0A", 3000)]
    [InlineData("00000000000000000000000000000000:FFFF", "0A", 65535)]
    [InlineData("0100007F:1388", "01", 0)]
    [InlineData("0101A8C0:1388", "0A", 0)]
    public void Only_local_LISTEN_sockets_are_offered(string address, string state, int expected)
    {
        var ports = ListeningPorts.ParseProc($"0: {address} 00000000:0000 {state} 00000000:00000000 00:00000000 00000000 1000 0 123456\n");
        Assert.Equal(expected == 0 ? [] : new[] { expected }, ports.Select(p => p.Port));
        if (expected != 0) Assert.Equal("123456", ports[0].Inode);
    }

    [Theory]
    [InlineData("Local: http://localhost:5173/", 5173)]
    [InlineData("Serving HTTP on 0.0.0.0 port 8000 (http://0.0.0.0:8000/) ...", 8000)]
    [InlineData("Server listening on port 3000", 3000)]
    [InlineData("Now listening on: http://[::]:5001", 5001)]
    [InlineData("http://example.com:3000", 0)]
    [InlineData("http://localhost:70000", 0)]
    [InlineData("http://localhost:300000", 0)]
    public void Recognizes_framework_output_without_treating_remote_URLs_as_local(string text, int expected)
    {
        Assert.Equal(expected == 0 ? [] : new[] { expected }, ListeningPorts.ParseOutput(text));
    }

    [Fact]
    public void Proc_does_not_offer_another_programs_existing_listener()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var process = new HostRuntime().Start(new ProcessStartSpec { Program = "sh", Arguments = ["-c", "sleep 10"] });
        var foreign = ((IPEndPoint)listener.LocalEndpoint).Port;
        Assert.DoesNotContain(foreign, ListeningPorts.ForProcess(process.Id));
    }

    [Fact]
    public void Output_chunks_wait_for_a_complete_port_and_duplicate_streams_are_deduplicated()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var process = new HostRuntime().Start(new ProcessStartSpec { Program = "sh", Arguments = ["-c", "sleep 10"] });
        var offered = new System.Collections.Concurrent.ConcurrentQueue<int>();
        using var watcher = new ProcessPortWatcher(process, offered.Enqueue);
        watcher.Feed("Listening on http://127.0.0.1:3");
        Assert.Empty(offered);
        watcher.Feed("000/\n");
        watcher.Feed("Listening on port 3000\n", standardError: true);
        Assert.Equal([3000], offered);
    }

    /// <summary>
    /// Nx's <c>serve</c> runs gothinkster/node-express-realworld-example-app with <c>--inspect</c>: the inspector's
    /// WebSocket on 9229 opens before the app's port 3000, and is never the app to preview.
    /// </summary>
    [Theory]
    [InlineData(new[] { "/usr/bin/node", "--inspect", "dist/api/main.js" }, null, 9229)]
    [InlineData(new[] { "node", "--inspect-brk=0.0.0.0:9230", "main.js" }, null, 9230)]
    [InlineData(new[] { "node", "--require", "ts-node/register", "--inspect=9231", "main.ts" }, null, 9231)]
    [InlineData(new[] { "node", "main.js" }, "--max-old-space-size=4096 --inspect-wait", 9229)]
    [InlineData(new[] { "node", "main.js" }, null, null)]
    [InlineData(new[] { "python3", "--inspect" }, null, null)]
    [InlineData(new[] { "node", "--inspection-report" }, null, null)]
    public void The_Node_inspector_s_port_is_known(string[] arguments, string? nodeOptions, int? port) =>
        Assert.Equal(port, ListeningPorts.InspectorPort(arguments, nodeOptions));

    [Fact]
    public async Task A_server_on_IPv4_loopback_is_reached_at_127_0_0_1()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Assert.Equal(new Uri($"http://127.0.0.1:{port}/"), await ListeningPorts.LoopbackAddressAsync(port));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task A_server_bound_to_IPv6_loopback_alone_is_reached_at_its_IPv6_address()
    {
        // A dev server bound to "localhost" where localhost is ::1, as Vite and Next.js are on Node 17 and later.
        if (!Socket.OSSupportsIPv6)
        {
            return;
        }

        var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
        listener.Server.DualMode = false;
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Assert.Equal(new Uri($"http://[::1]:{port}/"), await ListeningPorts.LoopbackAddressAsync(port));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task A_port_nothing_listens_on_keeps_the_IPv4_address()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        Assert.Equal(new Uri($"http://127.0.0.1:{port}/"), await ListeningPorts.LoopbackAddressAsync(port));
    }
}
