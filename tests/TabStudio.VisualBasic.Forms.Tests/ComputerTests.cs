using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.VisualBasic.Devices;
using Microsoft.VisualBasic.Logging;
using Microsoft.VisualBasic.MyServices.Internal;

namespace TabStudio.VisualBasic.Forms.Tests;

/// <summary>My.Computer, My.User and My.Application.Log on the device.</summary>
public sealed class ComputerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"tabstudio-vb-{Guid.NewGuid():N}");

    public ComputerTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Clock_reads_the_device_clock()
    {
        var clock = new Computer().Clock;
        var before = DateTime.Now;

        var local = clock.LocalTime;
        var utc = clock.GmtTime;
        var ticks = clock.TickCount;

        Assert.InRange(local, before, DateTime.Now);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.InRange(utc, before.ToUniversalTime().AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
        Assert.InRange(Environment.TickCount - ticks, 0, 5000);
    }

    [Fact]
    public void Info_describes_the_operating_system_and_its_memory()
    {
        var info = new Computer().Info;

        Assert.Equal(RuntimeInformation.OSDescription, info.OSFullName);
        Assert.Equal(Environment.OSVersion.Platform.ToString(), info.OSPlatform);
        Assert.Equal(Environment.OSVersion.Version.ToString(), info.OSVersion);
        Assert.Equal(CultureInfo.InstalledUICulture, info.InstalledUICulture);
        Assert.True(info.TotalPhysicalMemory > 0);
        Assert.InRange(info.AvailablePhysicalMemory, 0UL, info.TotalPhysicalMemory);
        Assert.InRange(info.TotalVirtualMemory, info.TotalPhysicalMemory, ulong.MaxValue);
        Assert.InRange(info.AvailableVirtualMemory, 0UL, info.TotalVirtualMemory);
    }

    [Fact]
    public void The_computer_has_the_device_name_its_screen_keyboard_and_mouse()
    {
        var computer = new Computer();

        Assert.Equal(Environment.MachineName, computer.Name);
        Assert.Same(Screen.PrimaryScreen, computer.Screen);
        Assert.Same(computer.Keyboard, new Computer().Keyboard);
        Assert.False(computer.Keyboard.AltKeyDown);
        Assert.False(computer.Keyboard.CtrlKeyDown);
        Assert.False(computer.Keyboard.ShiftKeyDown);
        Assert.False(computer.Keyboard.CapsLock);
        Assert.True(computer.Mouse.WheelExists);
        Assert.Equal(3, computer.Mouse.WheelScrollLines);
        Assert.False(computer.Mouse.ButtonsSwapped);
    }

    [Fact]
    public void What_the_tablet_cannot_do_throws_NotSupportedException_naming_the_member()
    {
        var computer = new Computer();

        var registry = Assert.Throws<NotSupportedException>(() => computer.Registry.CurrentUser);
        Assert.Contains("My.Computer.Registry.CurrentUser", registry.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => computer.Registry.GetValue(@"HKEY_CURRENT_USER\Software", "Name", "none"));
        var keys = Assert.Throws<NotSupportedException>(() => computer.Keyboard.SendKeys("{ENTER}"));
        Assert.Contains("SendKeys", keys.Message, StringComparison.Ordinal);
        var sound = Assert.Throws<NotSupportedException>(() => computer.Audio.Play(Path.Combine(_folder, "ding.wav")));
        Assert.Contains("My.Computer.Audio.Play", sound.Message, StringComparison.Ordinal);
        computer.Audio.Stop();
    }

    [Fact]
    public void FileSystem_reads_and_writes_the_device_files()
    {
        var files = new Computer().FileSystem;
        var path = files.CombinePath(_folder, "stock.txt");

        files.WriteAllText(path, "apples", append: false);
        files.WriteAllText(path, ", pears", append: true);

        Assert.True(files.FileExists(path));
        Assert.Equal("apples, pears", files.ReadAllText(path));
        Assert.Equal("stock.txt", files.GetName(path));
        Assert.Equal(_folder, files.GetParentPath(path));
        Assert.Contains(path, files.GetFiles(_folder));
        Assert.True(files.DirectoryExists(files.SpecialDirectories.Temp));

        files.RenameFile(path, "sold.txt");
        Assert.True(files.FileExists(Path.Combine(_folder, "sold.txt")));
        files.DeleteFile(Path.Combine(_folder, "sold.txt"));
        Assert.False(files.FileExists(Path.Combine(_folder, "sold.txt")));
    }

    [Fact]
    public void Network_copies_file_addresses_and_checks_its_arguments()
    {
        var network = new Network();
        var source = Path.Combine(_folder, "source.txt");
        var target = Path.Combine(_folder, "downloads", "copy.txt");
        File.WriteAllText(source, "price list");

        network.DownloadFile(new Uri(source), target);

        Assert.Equal("price list", File.ReadAllText(target));
        Assert.Throws<IOException>(() => network.DownloadFile(new Uri(source), target));
        network.DownloadFile(new Uri(source), target, string.Empty, string.Empty, showUI: false, connectionTimeout: 1000, overwrite: true);
        Assert.Throws<ArgumentException>(() => network.DownloadFile(new Uri(source), target, string.Empty, string.Empty, showUI: false, connectionTimeout: 0, overwrite: true));
        Assert.Throws<ArgumentException>(() => network.DownloadFile("not an address", target));
        Assert.Throws<NotSupportedException>(() => network.DownloadFile(new Uri("ftp://example.com/list.txt"), Path.Combine(_folder, "ftp.txt")));

        var uploaded = Path.Combine(_folder, "uploaded.txt");
        network.UploadFile(source, new Uri(uploaded));
        Assert.Equal("price list", File.ReadAllText(uploaded));
        Assert.Throws<FileNotFoundException>(() => network.UploadFile(Path.Combine(_folder, "missing.txt"), new Uri(uploaded)));
    }

    [Fact]
    public void User_is_the_device_user_unless_the_program_sets_a_principal()
    {
        string? deviceName = null;
        var deviceAuthenticated = false;
        string? ownName = null;
        var inRole = false;
        var thread = new Thread(() =>
        {
            Thread.CurrentPrincipal = null;
            var user = new User();
            deviceName = user.Name;
            deviceAuthenticated = user.IsAuthenticated;
            user.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("ada"), ["Admins"]);
            ownName = user.Name;
            inRole = user.IsInRole("Admins");
        });
        thread.Start();
        thread.Join();

        Assert.Equal(Environment.UserName, deviceName);
        Assert.True(deviceAuthenticated);
        Assert.Equal("ada", ownName);
        Assert.True(inRole);
    }

    [Fact]
    public void ContextValue_keeps_a_value_per_thread()
    {
        var value = new ContextValue<string>();
        value.Value = "main";
        string? onOtherThread = "unset";
        var thread = new Thread(() => onOtherThread = value.Value);
        thread.Start();
        thread.Join();

        Assert.Equal("main", value.Value);
        Assert.Null(onOtherThread);
        Assert.Null(new ContextValue<string>().Value);
    }

    [Fact]
    public void Log_writes_Information_and_more_severe_entries_to_its_file()
    {
        var log = new Log("Inventory");
        var writer = log.DefaultFileLogWriter!;
        writer.Location = LogFileLocation.Custom;
        writer.CustomLocation = _folder;
        writer.BaseFileName = "inventory";

        log.WriteEntry("Started");
        log.WriteEntry("Details", TraceEventType.Verbose);
        log.WriteException(new InvalidOperationException("Out of stock"), TraceEventType.Warning, "while selling");

        Assert.Equal(Path.Combine(_folder, "inventory.log"), writer.FullLogFileName);
        Assert.Equal(
            ["Inventory\tInformation\t0\tStarted", "Inventory\tWarning\t1\tOut of stock while selling"],
            File.ReadAllLines(writer.FullLogFileName));
        Assert.Equal(SourceLevels.Information, log.TraceSource.Switch.Level);
    }

    [Fact]
    public void Clipboard_keeps_text_files_audio_and_JSON_data()
    {
        var clipboard = new Computer().Clipboard;
        try
        {
            clipboard.SetText("Hello");
            Assert.True(clipboard.ContainsText());
            Assert.Equal("Hello", clipboard.GetText());

            clipboard.SetFileDropList(new StringCollection { "/storage/a.txt", "/storage/b.txt" });
            Assert.True(clipboard.ContainsFileDropList());
            Assert.Equal(["/storage/a.txt", "/storage/b.txt"], clipboard.GetFileDropList().Cast<string>());

            clipboard.SetAudio([1, 2, 3]);
            Assert.True(clipboard.ContainsAudio());
            Assert.NotNull(clipboard.GetAudioStream());

            clipboard.SetDataAsJson("point", new ClipboardPoint(3, 4));
            Assert.True(clipboard.TryGetData("point", out ClipboardPoint? point));
            Assert.Equal(new ClipboardPoint(3, 4), point);
            Assert.False(clipboard.TryGetData("point", out string? _));
        }
        finally
        {
            clipboard.Clear();
        }
    }
}

/// <summary>Data a test puts on the clipboard as JSON.</summary>
public sealed record ClipboardPoint(int X, int Y);
