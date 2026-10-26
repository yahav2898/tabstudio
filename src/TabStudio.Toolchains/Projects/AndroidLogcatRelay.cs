using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// Receives an app's own logcat through an authenticated loopback connection. Android forbids an ordinary
/// app from reading another UID's logs, so a debug-only provider runs logcat inside the built app's UID.
/// No READ_LOGS permission, root, adb connection, or release instrumentation is needed.
/// </summary>
public sealed class AndroidLogcatRelay : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly string _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Starts receiving lines. The callback runs off the UI thread.</summary>
    public AndroidLogcatRelay(Action<string> line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _listener.Start();
        _ = AcceptAsync(line, _stop.Token);
    }

    /// <summary>Creates a Gradle init script and generated debug source beneath build/, leaving sources intact.</summary>
    public string PrepareBuild(string directory)
    {
        var generated = Path.Combine(directory, "build", "tabstudio-logcat");
        Directory.CreateDirectory(generated);
        var source = Path.Combine(generated, "java", "dev", "tabstudio", "LogcatProvider.java");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, Source(((IPEndPoint)_listener.LocalEndpoint).Port, _token));
        // Use an init script rather than editing any of the user's Gradle files. Each module's existing debug
        // manifest is merged into a generated copy before the provider is inserted.
        var init = Path.Combine(generated, "logcat.init.gradle");
        File.WriteAllText(init, """
            import groovy.util.XmlParser
            import groovy.xml.XmlUtil
            gradle.projectsEvaluated {
              gradle.rootProject.allprojects { project ->
                if (project.plugins.hasPlugin('com.android.application')) {
                  def android = project.extensions.findByName('android')
                  def debug = android.sourceSets.getByName('debug')
                  def original = debug.manifest.srcFile
                  def parser = new XmlParser(false, false)
                  def manifest = original.exists() ? parser.parse(original) : parser.parseText('<manifest xmlns:android="http://schemas.android.com/apk/res/android"/>')
                  manifest.attributes()['xmlns:android'] = 'http://schemas.android.com/apk/res/android'
                  manifest.appendNode('uses-permission', ['android:name': 'android.permission.INTERNET'])
                  def application = manifest.application ? manifest.application[0] : manifest.appendNode('application')
                  application.appendNode('provider', ['android:name': 'dev.tabstudio.LogcatProvider', 'android:authorities': '${applicationId}.tabstudio.logcat', 'android:exported': 'false', 'android:initOrder': '100'])
                  def generated = new File(project.buildDir, 'tabstudio-logcat')
                  generated.mkdirs()
                  def overlay = new File(generated, 'AndroidManifest.xml')
                  overlay.text = XmlUtil.serialize(manifest)
                  debug.manifest.srcFile(overlay)
                  debug.java.srcDir(new File(gradle.startParameter.initScripts.find { it.name == 'logcat.init.gradle' }.parentFile, 'java'))
                }
              }
            }
            """);
        return init;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _stop.Dispose();
    }

    private async Task AcceptAsync(Action<string> line, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = ReadAsync(client, line, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // The IDE stopped or closed the run.
        }
    }

    private async Task ReadAsync(TcpClient client, Action<string> line, CancellationToken cancellationToken)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            try
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                using var reader = new StreamReader(client.GetStream());
                if (await ReadBoundedAsync(reader, timeout.Token).ConfigureAwait(false) != _token)
                {
                    return;
                }

                timeout.CancelAfter(Timeout.InfiniteTimeSpan);
                while (await ReadBoundedAsync(reader, cancellationToken).ConfigureAwait(false) is { } text)
                {
                    line(text);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // The app exited or the IDE ended the run.
            }
        }
    }

    private static async Task<string?> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var text = new System.Text.StringBuilder();
        var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false) > 0)
        {
            if (buffer[0] == '\n')
            {
                return text.ToString().TrimEnd('\r');
            }

            if (text.Length >= 8192)
            {
                throw new IOException("Logcat line exceeds the protocol limit.");
            }

            text.Append(buffer[0]);
        }

        return text.Length == 0 ? null : text.ToString();
    }

    private static string Source(int port, string token) => $$"""
        package dev.tabstudio;
        import android.content.ContentProvider;
        import android.content.ContentValues;
        import android.database.Cursor;
        import android.net.Uri;
        import java.io.*;
        import java.net.*;
        public final class LogcatProvider extends ContentProvider {
          public boolean onCreate() {
            new Thread(() -> {
              for (int attempt = 0; attempt < 10; attempt++) {
                Process logs = null;
                try (Socket socket = new Socket()) {
                  socket.connect(new InetSocketAddress("127.0.0.1", {{port}}), 3000);
                  PrintWriter out = new PrintWriter(new OutputStreamWriter(socket.getOutputStream(), "UTF-8"), true);
                  out.println("{{token}}");
                  logs = new ProcessBuilder("/system/bin/logcat", "-v", "threadtime", "-T", "1", "--pid=" + android.os.Process.myPid()).redirectErrorStream(true).start();
                  try (BufferedReader reader = new BufferedReader(new InputStreamReader(logs.getInputStream(), "UTF-8"))) {
                    String line;
                    while ((line = reader.readLine()) != null && !out.checkError()) out.println(line);
                    return;
                  }
                } catch (IOException ignored) { }
                finally { if (logs != null) logs.destroy(); }
                try { Thread.sleep(2000); } catch (InterruptedException ended) { return; }
              }
            }, "TabStudio logcat").start();
            return true;
          }
          public Cursor query(Uri u, String[] p, String s, String[] a, String o) { return null; }
          public String getType(Uri u) { return null; }
          public Uri insert(Uri u, ContentValues v) { return null; }
          public int delete(Uri u, String s, String[] a) { return 0; }
          public int update(Uri u, ContentValues v, String s, String[] a) { return 0; }
        }
        """;
}
