using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace TabStudio.App.Services;

/// <summary>
/// SPIKE (temporary): tries every way of running native programs on Android and logs what works, as
/// TABSTUDIO_SPIKE lines. Removed once the toolchain runtime is chosen.
/// </summary>
public static partial class SelfTest
{
    private const string AlpineVersion = "3.24.3";
    private const string AlpineBranch = "v3.24";

    private static async Task RunToolchainSpikeAsync()
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        var spike = new ToolchainSpike();
        try
        {
            await spike.RunAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            spike.Log("spike", false, $"{ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine("TABSTUDIO_SPIKE_SUMMARY " + string.Join(" | ", spike.Summary));
    }

    private sealed class ToolchainSpike
    {
        private readonly string _files = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "spike");
        private string _lib = string.Empty;
        private string _rootfs = string.Empty;
        private Dictionary<string, string> _guestEnvironment = [];

        public List<string> Summary { get; } = [];

        public void Log(string name, bool ok, string detail)
        {
            detail = detail.ReplaceLineEndings(" ⏎ ");
            if (detail.Length > 900)
            {
                detail = detail[..900] + "…";
            }

            Console.WriteLine($"TABSTUDIO_SPIKE {name}: {(ok ? "OK" : "FAIL")} {detail}");
            Summary.Add($"{name} {(ok ? "OK" : "FAIL")}");
        }

        public async Task RunAsync()
        {
            Directory.CreateDirectory(_files);
            var api = (await ExecAsync("/system/bin/getprop", ["ro.build.version.sdk"], null, null, 10)).Out.Trim();
            var release = (await ExecAsync("/system/bin/getprop", ["ro.build.version.release"], null, null, 10)).Out.Trim();
            var abis = (await ExecAsync("/system/bin/getprop", ["ro.product.cpu.abilist"], null, null, 10)).Out.Trim();
            var free = new DriveInfo(_files).AvailableFreeSpace / 1024 / 1024;
            Log("device", true, $"Android {release} (API {api}), process {RuntimeInformation.ProcessArchitecture}, ABIs {abis}, files {_files}, {free} MB free, temp {Path.GetTempPath()}");

            _lib = FindNativeLibraryDirectory();
            var listing = Directory.Exists(_lib)
                ? string.Join(", ", new DirectoryInfo(_lib).EnumerateFiles().Select(f => $"{f.Name} {f.Length / 1024} KB {f.UnixFileMode}"))
                : "missing";
            Log("native-lib-dir", File.Exists(Path.Combine(_lib, "libproot.so")), $"{_lib}: {listing}");

            var proot = Path.Combine(_lib, "libproot.so");
            await TryAsync("exec-native-lib", proot, ["--version"], null, null, r => r.Code == 0);

            var copy = Path.Combine(_files, "proot-copy");
            File.Copy(proot, copy, overwrite: true);
            File.SetUnixFileMode(copy, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            await TryAsync("exec-app-data (expect denied)", copy, ["--version"], null, null, r => r.Code == 0);

            var linker = Environment.Is64BitProcess ? "/system/bin/linker64" : "/system/bin/linker";
            await TryAsync("linker-exec-app-data", linker, [copy, "--version"], null, null, r => r.Code == 0);
            await TryAsync("child-exec-app-data (expect denied)", "/system/bin/sh", ["-c", $"{copy} --version"], null, null, r => r.Code == 0);
            await TryAsync("child-linker-exec-app-data", "/system/bin/sh", ["-c", $"{linker} {copy} --version"], null, null, r => r.Code == 0);
            await TryAsync("child-exec-native-lib", "/system/bin/sh", ["-c", $"{proot} --version && echo grandchild-ok"], null, null, r => r.Code == 0 && r.Out.Contains("grandchild-ok", StringComparison.Ordinal));

            await SafeAsync("proc-files", () => { ReadProcFiles(); return Task.CompletedTask; });
            await SafeAsync("repository-format", InspectRepositoryAsync);

            var prepared = false;
            await SafeAsync("rootfs", async () => prepared = await PrepareRootfsAsync());
            if (!prepared)
            {
                return;
            }

            await TryGuestAsync("proot-busybox", ["/bin/busybox", "uname", "-a"], null, 60, r => r.Code == 0);
            await TryGuestAsync("proot-grandchild", ["/bin/sh", "-c", "echo shell $$; sh -c 'echo grandchild of $PPID'; cat /etc/alpine-release; ls /proc/self/fd | wc -l"], null, 60, r => r.Code == 0 && r.Out.Contains("grandchild", StringComparison.Ordinal));
            await TryGuestAsync("proot-proc", ["/bin/sh", "-c", "head -c 200 /proc/stat; echo; cat /proc/loadavg /proc/uptime; head -n 2 /proc/meminfo; id"], null, 60, r => r.Code == 0);
            await TryGuestAsync("apk-version", ["/sbin/apk", "--version"], null, 60, r => r.Code == 0);
            var apkClock = Stopwatch.StartNew();
            await TryGuestAsync("apk-add-python-node", ["/sbin/apk", "add", "--no-progress", "python3", "nodejs", "npm"], null, 600, r => r.Code == 0);
            Log("apk-add-time", true, $"{apkClock.Elapsed.TotalSeconds:0.0} s, rootfs {DirectorySize(_rootfs) / 1024 / 1024} MB");
            await TryGuestAsync("python-stdin", ["/usr/bin/python3", "-c", "import sys; name = input('Name? '); print('Hello, ' + name + '!'); print(sys.version)"], "Tablet\n", 60, r => r.Code == 0 && r.Out.Contains("Hello, Tablet!", StringComparison.Ordinal));
            await TryGuestAsync("python-venv", ["/bin/sh", "-c", "python3 -m venv /tmp/venv && /tmp/venv/bin/python -m pip --version"], null, 300, r => r.Code == 0);
            await TryGuestAsync("node-e", ["/usr/bin/node", "-e", "console.log(process.version, 6 * 7, require('os').cpus().length)"], null, 60, r => r.Code == 0 && r.Out.Contains("42", StringComparison.Ordinal));
            await TryGuestAsync("npm-version", ["/usr/bin/npm", "--version"], null, 120, r => r.Code == 0);

            for (var i = 0; i < 3; i++)
            {
                await TryGuestAsync($"python-start-{i}", ["/usr/bin/python3", "-c", "pass"], null, 60, r => r.Code == 0);
            }

            _guestEnvironment["PROOT_NO_SECCOMP"] = "1";
            await TryGuestAsync("python-start-no-seccomp", ["/usr/bin/python3", "-c", "pass"], null, 60, r => r.Code == 0);
            _guestEnvironment.Remove("PROOT_NO_SECCOMP");

            await SafeAsync("pty", () => Task.Run(RunPseudoTerminal).WaitAsync(TimeSpan.FromSeconds(60)));
            await SafeAsync("kill-tree", KillTreeAsync);
            if (int.TryParse(api, CultureInfo.InvariantCulture, out var level) && level >= 31)
            {
                await SafeAsync("phantom-processes", PhantomProcessesAsync);
            }
        }

        private async Task SafeAsync(string name, Func<Task> experiment)
        {
            try
            {
                await experiment();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log(name, false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string FindNativeLibraryDirectory()
        {
            foreach (var line in File.ReadLines("/proc/self/maps"))
            {
                var index = line.IndexOf('/', StringComparison.Ordinal);
                if (index >= 0 && line.EndsWith("/libmonodroid.so", StringComparison.Ordinal))
                {
                    return Path.GetDirectoryName(line[index..])!;
                }
            }

            return string.Empty;
        }

        private void ReadProcFiles()
        {
            var results = new List<string>();
            foreach (var file in new[] { "/proc/stat", "/proc/loadavg", "/proc/uptime", "/proc/meminfo", "/proc/version", "/proc/vmstat", "/proc/self/stat" })
            {
                try
                {
                    var text = File.ReadAllText(file);
                    results.Add($"{file} ok ({text.Length} chars)");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    results.Add($"{file} {ex.GetType().Name}");
                }
            }

            Log("proc-files", true, string.Join(", ", results));
        }

        private async Task InspectRepositoryAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                var arch = AlpineArch();
                var index = await http.GetByteArrayAsync(new Uri($"https://dl-cdn.alpinelinux.org/alpine/{AlpineBranch}/main/{arch}/APKINDEX.tar.gz"));
                var entries = new List<string>();
                string? musl = null;
                await using (var gzip = new GZipStream(new MemoryStream(index), CompressionMode.Decompress))
                {
                    using var reader = new TarReader(gzip);
                    while (await reader.GetNextEntryAsync() is { } entry)
                    {
                        entries.Add($"{entry.Name} ({entry.Length})");
                        if (entry.Name == "APKINDEX" && entry.DataStream is { } data)
                        {
                            using var text = new StreamReader(data);
                            var all = await text.ReadToEndAsync();
                            var start = all.IndexOf("\nP:musl\n", StringComparison.Ordinal);
                            var version = all.IndexOf("\nV:", start + 1, StringComparison.Ordinal);
                            musl = all[(version + 3)..all.IndexOf('\n', version + 1)];
                        }
                    }
                }

                var package = await http.GetByteArrayAsync(new Uri($"https://dl-cdn.alpinelinux.org/alpine/{AlpineBranch}/main/{arch}/musl-{musl}.apk"));
                var magic = Convert.ToHexString(package.AsSpan(0, 8));
                Log("repository-format", true, $"index {index.Length} bytes, entries {string.Join(", ", entries)}; musl-{musl}.apk {package.Length} bytes, starts {magic}");
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
            {
                Log("repository-format", false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string AlpineArch() => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            Architecture.Arm => "armv7",
            _ => "unknown",
        };

        private async Task<bool> PrepareRootfsAsync()
        {
            var clock = Stopwatch.StartNew();
            var arch = AlpineArch();
            var archive = Path.Combine(_files, "alpine.tar.gz");
            _rootfs = Path.Combine(_files, "rootfs");
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    var bytes = await http.GetByteArrayAsync(new Uri($"https://dl-cdn.alpinelinux.org/alpine/{AlpineBranch}/releases/{arch}/alpine-minirootfs-{AlpineVersion}-{arch}.tar.gz"));
                    await File.WriteAllBytesAsync(archive, bytes);
                }

                var download = clock.Elapsed.TotalSeconds;
                if (Directory.Exists(_rootfs))
                {
                    Directory.Delete(_rootfs, recursive: true);
                }

                var (files, links, hardLinks, directories) = await ExtractAsync(archive, _rootfs);
                File.WriteAllText(Path.Combine(_rootfs, "etc", "resolv.conf"), "nameserver 8.8.8.8\nnameserver 1.1.1.1\n");
                var temp = Path.Combine(_files, "proot-tmp");
                Directory.CreateDirectory(temp);
                _guestEnvironment = new Dictionary<string, string>
                {
                    ["PATH"] = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                    ["HOME"] = "/root",
                    ["TERM"] = "xterm-256color",
                    ["LANG"] = "C.UTF-8",
                    ["TMPDIR"] = "/tmp",
                    ["PROOT_TMP_DIR"] = temp,
                    ["PROOT_LOADER"] = Path.Combine(_lib, "libproot-loader.so"),
                };
                Log("rootfs", true, $"downloaded in {download:0.0} s, extracted {files} files, {links} symlinks, {hardLinks} hard links, {directories} folders in {clock.Elapsed.TotalSeconds - download:0.0} s");
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException or TaskCanceledException)
            {
                Log("rootfs", false, $"{ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static async Task<(int Files, int Links, int HardLinks, int Directories)> ExtractAsync(string archive, string destination)
        {
            int files = 0, links = 0, hardLinks = 0, directories = 0;
            Directory.CreateDirectory(destination);
            await using var stream = File.OpenRead(archive);
            await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);
            while (await reader.GetNextEntryAsync() is { } entry)
            {
                var path = Path.GetFullPath(Path.Combine(destination, entry.Name));
                switch (entry.EntryType)
                {
                    case TarEntryType.Directory:
                        Directory.CreateDirectory(path);
                        File.SetUnixFileMode(path, entry.Mode | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                        directories++;
                        break;
                    case TarEntryType.RegularFile or TarEntryType.V7RegularFile:
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        await entry.ExtractToFileAsync(path, overwrite: true);
                        File.SetUnixFileMode(path, entry.Mode | UnixFileMode.UserRead | UnixFileMode.UserWrite);
                        files++;
                        break;
                    case TarEntryType.SymbolicLink:
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.CreateSymbolicLink(path, entry.LinkName);
                        links++;
                        break;
                    case TarEntryType.HardLink:
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.Copy(Path.GetFullPath(Path.Combine(destination, entry.LinkName)), path, overwrite: true);
                        hardLinks++;
                        break;
                }
            }

            return (files, links, hardLinks, directories);
        }

        private async Task TryGuestAsync(string name, string[] command, string? stdin, int timeoutSeconds, Func<Run, bool> success)
        {
            string[] arguments =
            [
                "--kill-on-exit", "--link2symlink", "-0", "-r", _rootfs,
                "-b", "/dev", "-b", "/proc", "-b", "/sys", "-b", "/dev/urandom:/dev/random", "-b", "/proc/self/fd:/dev/fd",
                "-w", "/root", .. command,
            ];
            await TryAsync(name, Path.Combine(_lib, "libproot.so"), arguments, _guestEnvironment, stdin, success, timeoutSeconds);
        }

        private Task TryAsync(string name, string file, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, string? stdin, Func<Run, bool> success, int timeoutSeconds = 60) =>
            SafeAsync(name, async () =>
            {
                var run = await ExecAsync(file, arguments, environment, stdin, timeoutSeconds);
                Log(name, run.Error is null && success(run), run.Error ?? $"exit {run.Code} in {run.Milliseconds} ms; out: {run.Out.Trim()}; err: {run.Err.Trim()}");
            });

        private static async Task<Run> ExecAsync(string file, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, string? stdin, int timeoutSeconds)
        {
            var info = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            if (environment is not null)
            {
                info.Environment.Clear();
                foreach (var (key, value) in environment)
                {
                    info.Environment[key] = value;
                }
            }

            var clock = Stopwatch.StartNew();
            try
            {
                using var process = Process.Start(info)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (stdin is not null)
                {
                    await process.StandardInput.WriteAsync(stdin);
                }

                process.StandardInput.Close();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    return new Run(-1, string.Empty, string.Empty, clock.ElapsedMilliseconds, $"timed out after {timeoutSeconds} s");
                }

                return new Run(process.ExitCode, await output, await error, clock.ElapsedMilliseconds, null);
            }
            catch (Win32Exception ex)
            {
                return new Run(-1, string.Empty, string.Empty, clock.ElapsedMilliseconds, $"exec failed: errno {ex.NativeErrorCode} {ex.Message}");
            }
        }

        private void RunPseudoTerminal()
        {
            try
            {
                string[] arguments =
                [
                    Path.Combine(_lib, "libproot.so"), "--kill-on-exit", "--link2symlink", "-0", "-r", _rootfs,
                    "-b", "/dev", "-b", "/proc", "-b", "/sys", "-w", "/root",
                    "/bin/sh", "-c", "tty; stty size; echo TERM=$TERM; printf '\\033[31mred\\033[0m\\n'",
                ];
                var environment = _guestEnvironment.Select(e => $"{e.Key}={e.Value}").ToArray();
                var clock = Stopwatch.StartNew();
                var (pid, master) = SpikePty.Spawn(arguments[0], arguments, environment, rows: 30, columns: 100);
                var output = new StringBuilder();
                using (var stream = new FileStream(master, FileAccess.Read, 1))
                {
                    var buffer = new byte[4096];
                    while (true)
                    {
                        int read;
                        try
                        {
                            read = stream.Read(buffer, 0, buffer.Length);
                        }
                        catch (IOException)
                        {
                            break; // EIO: the slave side closed.
                        }

                        if (read <= 0)
                        {
                            break;
                        }

                        output.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    }
                }

                var status = SpikePty.Wait(pid);
                var text = output.ToString().Replace("\u001b", "\\e", StringComparison.Ordinal);
                Log("pty", text.Contains("30 100", StringComparison.Ordinal) && text.Contains("/dev/pts/", StringComparison.Ordinal), $"pid {pid} status {status} in {clock.ElapsedMilliseconds} ms: {text}");
            }
            catch (Exception ex) when (ex is IOException or Win32Exception or DllNotFoundException or EntryPointNotFoundException)
            {
                Log("pty", false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private async Task KillTreeAsync()
        {
            string[] arguments =
            [
                "--kill-on-exit", "--link2symlink", "-0", "-r", _rootfs, "-b", "/dev", "-b", "/proc", "-b", "/sys", "-w", "/root",
                "/bin/sh", "-c", "sleep 300 & sleep 301 & (sh -c 'sleep 302') & echo started; wait",
            ];
            var info = new ProcessStartInfo(Path.Combine(_lib, "libproot.so")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardInput = true };
            foreach (var argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            info.Environment.Clear();
            foreach (var (key, value) in _guestEnvironment)
            {
                info.Environment[key] = value;
            }

            using var process = Process.Start(info)!;
            var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60));
            await Task.Delay(1000);
            var before = Descendants(process.Id);
            var names = string.Join(",", before.Select(pid => $"{pid}:{Comm(pid)}"));
            foreach (var pid in before.Append(process.Id))
            {
                _ = SpikePty.Kill(pid, 9);
            }

            await Task.Delay(2000);
            var after = before.Where(pid => Directory.Exists($"/proc/{pid}") && !File.ReadAllText($"/proc/{pid}/stat").Contains(") Z ", StringComparison.Ordinal)).ToList();
            Log("kill-tree", line == "started" && before.Count >= 4 && after.Count == 0, $"descendants before: {names}; alive after kill: {after.Count}");
        }

        private async Task PhantomProcessesAsync()
        {
            var info = new ProcessStartInfo("/system/bin/sh") { UseShellExecute = false, RedirectStandardOutput = true };
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("i=0; while [ $i -lt 40 ]; do sleep 120 & i=$((i+1)); done; echo started; wait");
            using var process = Process.Start(info)!;
            await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            var started = Descendants(process.Id).Count;
            await Task.Delay(TimeSpan.FromSeconds(30));
            var alive = Descendants(process.Id).Count;
            foreach (var pid in Descendants(process.Id).Append(process.Id))
            {
                _ = SpikePty.Kill(pid, 9);
            }

            Log("phantom-processes", true, $"started {started} sleeping children, {alive} alive after 30 s, shell alive: {!process.HasExited}");
        }

        private static List<int> Descendants(int root)
        {
            var parents = new Dictionary<int, int>();
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(directory), CultureInfo.InvariantCulture, out var pid))
                {
                    continue;
                }

                try
                {
                    var stat = File.ReadAllText(Path.Combine(directory, "stat"));
                    var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
                    parents[pid] = int.Parse(fields[1], CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
                {
                    // Gone, or not ours.
                }
            }

            var result = new List<int>();
            var queue = new Queue<int>([root]);
            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                foreach (var (pid, ppid) in parents)
                {
                    if (ppid == parent)
                    {
                        result.Add(pid);
                        queue.Enqueue(pid);
                    }
                }
            }

            return result;
        }

        private static string Comm(int pid)
        {
            try
            {
                return File.ReadAllText($"/proc/{pid}/comm").Trim();
            }
            catch (IOException)
            {
                return "?";
            }
        }

        private static long DirectorySize(string path) =>
            new DirectoryInfo(path).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true }).Sum(f => f.Length);

        private sealed record Run(int Code, string Out, string Err, long Milliseconds, string? Error);
    }

    /// <summary>SPIKE: a pseudo terminal through posix_openpt and posix_spawn with a new session.</summary>
    private static class SpikePty
    {
        private const int OpenReadWrite = 2;
        private const int NoControllingTerminal = 0x100;
        private const short SetSession = 0x80;
        private const short SetSignalDefaults = 0x04;
        private const short SetSignalMask = 0x08;
        private const nuint SetWindowSize = 0x5414;

        public static (int Pid, SafeFileHandle Master) Spawn(string path, string[] arguments, string[] environment, ushort rows, ushort columns)
        {
            var master = posix_openpt(OpenReadWrite | NoControllingTerminal);
            if (master < 0 || grantpt(master) != 0 || unlockpt(master) != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "posix_openpt");
            }

            var name = new byte[128];
            if (ptsname_r(master, name, (nuint)name.Length) != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "ptsname_r");
            }

            var slave = Encoding.UTF8.GetString(name, 0, Array.IndexOf(name, (byte)0));
            var size = new WindowSize { Rows = rows, Columns = columns };
            _ = ioctl(master, SetWindowSize, ref size);

            var actions = Marshal.AllocHGlobal(1024);
            var attributes = Marshal.AllocHGlobal(1024);
            var signals = Marshal.AllocHGlobal(256);
            var slavePath = Marshal.StringToCoTaskMemUTF8(slave);
            var strings = new List<IntPtr> { slavePath };
            try
            {
                _ = posix_spawn_file_actions_init(actions);
                _ = posix_spawn_file_actions_addopen(actions, 0, slavePath, OpenReadWrite, 0);
                _ = posix_spawn_file_actions_adddup2(actions, 0, 1);
                _ = posix_spawn_file_actions_adddup2(actions, 0, 2);
                _ = posix_spawnattr_init(attributes);
                _ = posix_spawnattr_setflags(attributes, SetSession | SetSignalDefaults | SetSignalMask);
                _ = sigfillset(signals);
                _ = posix_spawnattr_setsigdefault(attributes, signals);
                _ = sigemptyset(signals);
                _ = posix_spawnattr_setsigmask(attributes, signals);

                var argv = new IntPtr[arguments.Length + 1];
                for (var i = 0; i < arguments.Length; i++)
                {
                    argv[i] = Marshal.StringToCoTaskMemUTF8(arguments[i]);
                    strings.Add(argv[i]);
                }

                var envp = new IntPtr[environment.Length + 1];
                for (var i = 0; i < environment.Length; i++)
                {
                    envp[i] = Marshal.StringToCoTaskMemUTF8(environment[i]);
                    strings.Add(envp[i]);
                }

                var program = Marshal.StringToCoTaskMemUTF8(path);
                strings.Add(program);
                var result = posix_spawn(out var pid, program, actions, attributes, argv, envp);
                if (result != 0)
                {
                    throw new Win32Exception(result, "posix_spawn");
                }

                return (pid, new SafeFileHandle(master, ownsHandle: true));
            }
            finally
            {
                foreach (var pointer in strings)
                {
                    Marshal.FreeCoTaskMem(pointer);
                }

                Marshal.FreeHGlobal(actions);
                Marshal.FreeHGlobal(attributes);
                Marshal.FreeHGlobal(signals);
            }
        }

        public static string Wait(int pid)
        {
            var result = waitpid(pid, out var status, 0);
            if (result < 0)
            {
                return $"waitpid errno {Marshal.GetLastPInvokeError()}";
            }

            return (status & 0x7f) == 0 ? $"exited {(status >> 8) & 0xff}" : $"signal {status & 0x7f}";
        }

        public static int Kill(int pid, int signal) => kill(pid, signal);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowSize
        {
            public ushort Rows;
            public ushort Columns;
            public ushort Width;
            public ushort Height;
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int posix_openpt(int flags);

        [DllImport("libc", SetLastError = true)]
        private static extern int grantpt(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern int unlockpt(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern int ptsname_r(int fd, byte[] buffer, nuint length);

        [DllImport("libc", SetLastError = true)]
        private static extern int ioctl(int fd, nuint request, ref WindowSize size);

        [DllImport("libc")]
        private static extern int posix_spawn_file_actions_init(IntPtr actions);

        [DllImport("libc")]
        private static extern int posix_spawn_file_actions_addopen(IntPtr actions, int fd, IntPtr path, int flags, uint mode);

        [DllImport("libc")]
        private static extern int posix_spawn_file_actions_adddup2(IntPtr actions, int fd, int newFd);

        [DllImport("libc")]
        private static extern int posix_spawnattr_init(IntPtr attributes);

        [DllImport("libc")]
        private static extern int posix_spawnattr_setflags(IntPtr attributes, short flags);

        [DllImport("libc")]
        private static extern int posix_spawnattr_setsigdefault(IntPtr attributes, IntPtr signals);

        [DllImport("libc")]
        private static extern int posix_spawnattr_setsigmask(IntPtr attributes, IntPtr signals);

        [DllImport("libc")]
        private static extern int sigfillset(IntPtr set);

        [DllImport("libc")]
        private static extern int sigemptyset(IntPtr set);

        [DllImport("libc")]
        private static extern int posix_spawn(out int pid, IntPtr path, IntPtr actions, IntPtr attributes, IntPtr[] argv, IntPtr[] envp);

        [DllImport("libc", SetLastError = true)]
        private static extern int waitpid(int pid, out int status, int options);

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int signal);
    }
}
