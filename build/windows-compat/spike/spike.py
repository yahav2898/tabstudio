#!/usr/bin/env python3
"""Spike: measures Wine + the .NET Windows Desktop runtime + Xvfb in an Alpine Linux environment.

Runs the same way in an Alpine container on a CI host and in TabStudio's PRoot environment on Android.
Writes results.txt (one "SPIKE key value" line per measurement), results.json and PNG screenshots to the out folder.

usage: spike.py <spike-dir> <out-dir> [--dotnet <dir>] [--arch x64|arm64] [--quick]
  <spike-dir> holds apps/SpikeForms and apps/SpikeWpf (built), and dotnet-<arch>/ (unpacked runtime) or the
  runtime is downloaded from Microsoft's feed into it.
"""
import json
import mmap
import os
import shutil
import signal
import struct
import subprocess
import sys
import threading
import time
import urllib.request
import zlib

SPIKE = sys.argv[1]
OUT = sys.argv[2]
ARGS = sys.argv[3:]
ARCH = ARGS[ARGS.index("--arch") + 1] if "--arch" in ARGS else ("arm64" if os.uname().machine == "aarch64" else "x64")
QUICK = "--quick" in ARGS
DISPLAY_NUMBER = 5
FB_DIR = "/tmp/spike-fb"
FB = FB_DIR + "/Xvfb_screen0"
PREFIX = "/root/.wine-spike"
DESKTOP = (1024, 720)
results = {}
os.makedirs(OUT, exist_ok=True)
log_file = open(os.path.join(OUT, "results.txt"), "a", buffering=1)


def log(key, value):
    results[key] = value
    line = f"SPIKE {key} {value}"
    print(line, flush=True)
    log_file.write(line + "\n")


def note(text):
    print("SPIKE-NOTE " + text, flush=True)
    log_file.write("NOTE " + text + "\n")


def wine_binary():
    for candidate in ("/opt/tabstudio/wine/bin/wine", "/usr/bin/wine", "/usr/bin/wine64"):
        if os.path.exists(candidate):
            return candidate
    return "wine"


WINE = wine_binary()
WINESERVER = os.path.join(os.path.dirname(WINE), "wineserver")
ENV = dict(os.environ)
ENV.update({
    "WINEPREFIX": PREFIX,
    "WINEDEBUG": "-all,err+module,err+seh",
    "WINEDLLOVERRIDES": "winemenubuilder.exe=;winedbg.exe=",
    "DISPLAY": f":{DISPLAY_NUMBER}",
    "DOTNET_EnableWriteXorExecute": "0",
    "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
    "DOTNET_NOLOGO": "1",
    "LANG": "C.UTF-8",
    "PATH": os.path.dirname(WINE) + ":" + os.environ.get("PATH", "/usr/bin:/bin"),
})


def run(command, timeout=300, env=None, check=False):
    start = time.monotonic()
    try:
        done = subprocess.run(command, env=env or ENV, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=timeout, text=True, errors="replace")
        output, code = done.stdout, done.returncode
    except subprocess.TimeoutExpired as ex:
        output, code = (ex.stdout or b"").decode("utf-8", "replace") if isinstance(ex.stdout, bytes) else (ex.stdout or ""), "timeout"
    elapsed = time.monotonic() - start
    if check and code != 0:
        note(f"{command[:3]} failed ({code}): {output[-1500:]}")
    return code, output, elapsed


def du(path):
    total = 0
    for root, _, files in os.walk(path):
        for name in files:
            try:
                total += os.lstat(os.path.join(root, name)).st_size
            except OSError:
                pass
    return total


def mb(size):
    return f"{size / 1048576:.1f}MiB"


def rss_by_name():
    """Resident memory of every process but this script's own tools, by name."""
    by_name = {}
    for pid in os.listdir("/proc"):
        if not pid.isdigit():
            continue
        try:
            with open(f"/proc/{pid}/status") as status:
                fields = dict(line.split(":", 1) for line in status if ":" in line)
            name = fields["Name"].strip()
            rss = int(fields.get("VmRSS", "0 kB").split()[0]) * 1024
        except (OSError, KeyError, ValueError):
            continue
        if name in ("python3", "sh", "ash", "proot", "libproot.so", "ps", "sleep"):
            continue
        by_name[name] = by_name.get(name, 0) + rss
    return by_name


def wine_rss():
    by_name = rss_by_name()
    xvfb = by_name.pop("Xvfb", 0)
    return sum(by_name.values()), xvfb, by_name


# ---------- the framebuffer (Xvfb -fbdir: an XWD file mapped into memory) ----------

class Framebuffer:
    def __init__(self, path):
        self.file = open(path, "rb")
        self.map = mmap.mmap(self.file.fileno(), 0, access=mmap.ACCESS_READ)
        header = struct.unpack(">25I", self.map[:100])
        (header_size, _version, _format, _depth, self.width, self.height, _xoff, self.byte_order, _unit, _bit_order,
         _pad, self.bpp, self.stride, _visual, self.red, self.green, self.blue, _bits, _entries, ncolors) = header[:20]
        self.offset = header_size + ncolors * 12

    def region(self, x, y, w, h):
        rows = []
        for row in range(y, y + h):
            start = self.offset + row * self.stride + x * 4
            rows.append(self.map[start:start + w * 4])
        return b"".join(rows)

    def checksum(self, x, y, w, h):
        return zlib.crc32(self.region(x, y, w, h))

    def png(self, path, x=0, y=0, w=None, h=None):
        w = w or self.width
        h = h or self.height
        raw = bytearray()
        for row in range(y, y + h):
            start = self.offset + row * self.stride + x * 4
            line = self.map[start:start + w * 4]
            rgb = bytearray(w * 3)
            rgb[0::3] = line[2::4]
            rgb[1::3] = line[1::4]
            rgb[2::3] = line[0::4]
            raw += b"\x00" + rgb

        def chunk(kind, data):
            return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

        with open(path, "wb") as png:
            png.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                      + chunk(b"IDAT", zlib.compress(bytes(raw), 6)) + chunk(b"IEND", b""))


def measure_fps(fb, seconds, area):
    """Distinct frames per second the screen area shows, sampled every 2 ms."""
    changes = 0
    last = fb.checksum(*area)
    samples = 0
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        current = fb.checksum(*area)
        samples += 1
        if current != last:
            changes += 1
            last = current
        time.sleep(0.002)
    return changes / seconds, samples / seconds


# ---------- a running Windows program ----------

class Program:
    def __init__(self, name, command, extra_env=None):
        self.name = name
        self.lines = []
        self.event = threading.Condition()
        env = dict(ENV)
        env.update(extra_env or {})
        self.started = time.monotonic()
        self.stderr = open(os.path.join(OUT, f"{name}.stderr.txt"), "w")
        self.process = subprocess.Popen(command, env=env, stdout=subprocess.PIPE, stderr=self.stderr, text=True, errors="replace")
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        for line in self.process.stdout:
            line = line.rstrip()
            with self.event:
                self.lines.append((time.monotonic() - self.started, line))
                self.event.notify_all()
            print(f"  [{self.name}] {line}", flush=True)
        with self.event:
            self.lines.append((time.monotonic() - self.started, "<eof>"))
            self.event.notify_all()

    def wait_for(self, prefix, timeout):
        end = time.monotonic() + timeout
        with self.event:
            while True:
                for at, line in self.lines:
                    if line.startswith(prefix):
                        return at, line
                    if line == "<eof>":
                        return None, None
                remaining = end - time.monotonic()
                if remaining <= 0:
                    return None, None
                self.event.wait(remaining)

    def stop(self):
        if self.process.poll() is None:
            self.process.send_signal(signal.SIGKILL)
            try:
                self.process.wait(10)
            except subprocess.TimeoutExpired:
                pass
        self.stderr.close()


def tail(name, count=25):
    try:
        with open(os.path.join(OUT, f"{name}.stderr.txt")) as stderr:
            lines = stderr.read().splitlines()
        return " / ".join(lines[-count:])
    except OSError:
        return ""


def click(x, y, button=1):
    code, output, elapsed = run(["xdotool", "mousemove", str(x), str(y), "click", str(button)], timeout=30)
    return code, elapsed


def wait_change(fb, area, before, timeout):
    start = time.monotonic()
    while time.monotonic() - start < timeout:
        if fb.checksum(*area) != before:
            return time.monotonic() - start
        time.sleep(0.005)
    return None


# ---------- the steps ----------

def ensure_dotnet():
    folder = os.path.join(SPIKE, f"dotnet-{ARCH}")
    if os.path.exists(os.path.join(folder, "dotnet.exe")):
        return folder
    start = time.monotonic()
    index = json.load(urllib.request.urlopen("https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json", timeout=60))
    release = index["releases"][0]
    total = 0
    for part in ("runtime", "windowsdesktop"):
        name = f"dotnet-runtime-win-{ARCH}.zip" if part == "runtime" else f"windowsdesktop-runtime-win-{ARCH}.zip"
        file = next(f for f in release[part]["files"] if f["name"] == name)
        zip_path = os.path.join(SPIKE, os.path.basename(file["url"]))
        urllib.request.urlretrieve(file["url"], zip_path)
        total += os.path.getsize(zip_path)
        shutil.unpack_archive(zip_path, folder)
        os.remove(zip_path)
    log("dotnet_download_and_unzip_seconds", f"{time.monotonic() - start:.1f}")
    log("dotnet_zips", mb(total))
    return folder


def dedupe_prefix():
    """Replaces the prefix's copies of Wine's own DLLs with links to them, and reports what that saved."""
    lib = os.path.join(os.path.dirname(os.path.dirname(WINE)), "lib", "wine")
    sizes = {}
    for sub in ("system32", "syswow64"):
        sizes[sub] = du(os.path.join(PREFIX, "drive_c", "windows", sub))
    log("prefix_system32", mb(sizes["system32"]))
    log("prefix_syswow64", mb(sizes["syswow64"]))
    builtin = {}
    for arch_dir in os.listdir(lib) if os.path.isdir(lib) else []:
        if arch_dir.endswith("-windows"):
            for name in os.listdir(os.path.join(lib, arch_dir)):
                path = os.path.join(lib, arch_dir, name)
                builtin.setdefault((name.lower(), os.path.getsize(path)), []).append(path)
    saved = 0
    linked = 0
    for sub in ("system32", "syswow64"):
        folder = os.path.join(PREFIX, "drive_c", "windows", sub)
        for root, _, files in os.walk(folder):
            for name in files:
                path = os.path.join(root, name)
                if os.path.islink(path):
                    continue
                size = os.path.getsize(path)
                for candidate in builtin.get((name.lower(), size), []):
                    with open(path, "rb") as a, open(candidate, "rb") as b:
                        if a.read() == b.read():
                            os.remove(path)
                            os.symlink(candidate, path)
                            saved += size
                            linked += 1
                            break
    log("prefix_dedupe", f"linked {linked} files, saved {mb(saved)}, prefix now {mb(du(PREFIX))}")


def registry(entries):
    reg = os.path.join("/tmp", "spike.reg")
    with open(reg, "w") as f:
        f.write("REGEDIT4\n\n" + entries)
    code, output, elapsed = run([WINE, "regedit", "/S", reg], timeout=120)
    return code, elapsed


def start_xvfb():
    os.makedirs(FB_DIR, exist_ok=True)
    os.makedirs("/tmp/.X11-unix", exist_ok=True)
    os.chmod("/tmp/.X11-unix", 0o1777)
    for stale in (f"/tmp/.X{DISPLAY_NUMBER}-lock", f"/tmp/.X11-unix/X{DISPLAY_NUMBER}"):
        if os.path.exists(stale):
            os.remove(stale)
    start = time.monotonic()
    xvfb_log = open(os.path.join(OUT, "xvfb.txt"), "w")
    process = subprocess.Popen(
        ["Xvfb", f":{DISPLAY_NUMBER}", "-screen", "0", "1280x800x24", "-fbdir", FB_DIR, "-ac", "-nolisten", "tcp",
         "+extension", "GLX", "-noreset"], stdout=xvfb_log, stderr=subprocess.STDOUT)
    while time.monotonic() - start < 60:
        if os.path.exists(f"/tmp/.X11-unix/X{DISPLAY_NUMBER}") and os.path.exists(FB):
            break
        if process.poll() is not None:
            break
        time.sleep(0.05)
    log("xvfb_start_seconds", f"{time.monotonic() - start:.2f}")
    log("xvfb_running", process.poll() is None)
    return process


def dotnet_command(dll, *extra):
    return [WINE, "C:\\dotnet\\dotnet.exe", "Z:" + dll.replace("/", "\\"), *extra]


def show_app(fb, label, command, extra_env=None, timeout=240):
    program = Program(label, command, extra_env)
    at, shown = program.wait_for("SHOWN", min(timeout, 150))
    if shown is None:
        log(f"{label}_shown", "no")
        note(f"{label} stderr: {tail(label)}")
        program.stop()
        return None, None
    log(f"{label}_startup_seconds", f"{at:.2f}")
    log(f"{label}_shown_line", shown)
    return program, shown.split()


def exercise(fb, label, program, parts):
    area = (0, 0, DESKTOP[0], DESKTOP[1])
    time.sleep(2.0)
    fb.png(os.path.join(OUT, f"{label}-shown.png"), 0, 0, *DESKTOP)
    total, xvfb, by_name = wine_rss()
    log(f"{label}_rss_wine", mb(total))
    log(f"{label}_rss_xvfb", mb(xvfb))
    log(f"{label}_rss_detail", " ".join(f"{k}={mb(v)}" for k, v in sorted(by_name.items(), key=lambda kv: -kv[1])[:8]))
    bx, by = int(parts[3]), int(parts[4])
    before = fb.checksum(*area)
    code, elapsed = click(bx, by)
    _, clicked = program.wait_for("CLICKED 1", 30)
    changed = wait_change(fb, area, before, 10)
    log(f"{label}_click", f"xdotool={code} ({elapsed:.2f}s) app={'yes' if clicked else 'no'} frame_change={'%.3fs' % changed if changed is not None else 'none'}")
    time.sleep(1.0)
    fb.png(os.path.join(OUT, f"{label}-clicked.png"), 0, 0, *DESKTOP)
    if not QUICK and "OPEN" in parts:
        ox, oy = int(parts[parts.index("OPEN") + 1]), int(parts[parts.index("OPEN") + 2])
        click(ox, oy)
        _, opened = program.wait_for("OPENED", 30)
        log(f"{label}_second_window", "yes" if opened else "no")
        time.sleep(1.5)
        fb.png(os.path.join(OUT, f"{label}-second-window.png"), 0, 0, *DESKTOP)


def animate(fb, label, dll, extra_env=None):
    area = (0, 0, DESKTOP[0], DESKTOP[1])
    program, parts = show_app(fb, label + "_animate", dotnet_command(dll, "--animate"), extra_env)
    if program is None:
        return
    time.sleep(2.0)
    fps, sampling = measure_fps(fb, 5, area)
    log(f"{label}_fps", f"{fps:.1f} (sampled at {sampling:.0f}/s)")
    total, xvfb, _ = wine_rss()
    log(f"{label}_animate_rss_wine", mb(total))
    fb.png(os.path.join(OUT, f"{label}-animate.png"), 0, 0, *DESKTOP)
    program.stop()
    time.sleep(1)


def resize(fb, program):
    code, output, _ = run(["xdotool", "search", "--name", "Wine desktop"], timeout=30)
    ids = output.split()
    log("desktop_windows", " ".join(ids) or "none")
    if not ids:
        code, tree, _ = run(["xwininfo", "-root", "-children"], timeout=30)
        note("root children: " + " | ".join(l.strip() for l in tree.splitlines() if "0x" in l)[:1500])
        return
    run(["xdotool", "windowsize", ids[-1], "800", "600"], timeout=30)
    _, line = program.wait_for("SCREEN 800x600", 15)
    log("resize_desktop_800x600", "the app saw the new screen size" if line else "the app did not see it")
    time.sleep(1)
    fb.png(os.path.join(OUT, "resize-800x600.png"), 0, 0, 1024, 720)
    run(["xdotool", "windowsize", ids[-1], str(DESKTOP[0]), str(DESKTOP[1])], timeout=30)
    _, line = program.wait_for(f"SCREEN {DESKTOP[0]}x{DESKTOP[1]}", 15)
    log("resize_desktop_back", "yes" if line else "no")


def diagnose():
    """When Wine does not start: what it loaded, and what its Unix libraries miss."""
    lib = os.path.join(os.path.dirname(os.path.dirname(WINE)), "lib", "wine")
    env = dict(ENV)
    env["WINEDEBUG"] = "+loaddll,+module,+seh,warn+all"
    code, output, _ = run([WINE, "wineboot", "-i"], timeout=300, env=env)
    with open(os.path.join(OUT, "wineboot-debug.txt"), "w") as f:
        f.write(output)
    note("wineboot +module tail: " + " / ".join(output.splitlines()[-40:]))
    env["WINEDEBUG"] = "+all"
    code, output, _ = run([WINE, "wineboot", "-i"], timeout=300, env=env)
    with open(os.path.join(OUT, "wineboot-all.txt"), "w") as f:
        f.write(output[:400000])
    note("wineboot +all head: " + " / ".join(output.splitlines()[:25]))
    note("wineboot +all tail: " + " / ".join(output.splitlines()[-30:]))
    env = dict(ENV)
    env["WINELOADERNOEXEC"] = "1"
    env["WINEDEBUG"] = "err+all"
    code, output, _ = run([WINE, "wineboot", "-i"], timeout=300, env=env)
    note(f"wineboot without the preloader: exit {code}: " + " / ".join(output.splitlines()[-15:]))
    server = subprocess.Popen([WINESERVER, "-f", "-d1"], env=ENV, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    time.sleep(4)
    server.kill()
    note("wineserver -f -d1: " + " / ".join((server.communicate()[0] or "").splitlines()[-15:]))
    for arch_dir in sorted(os.listdir(lib)) if os.path.isdir(lib) else []:
        note(f"{arch_dir}: {len(os.listdir(os.path.join(lib, arch_dir)))} files")
    unix = os.path.join(lib, "aarch64-unix") if ARCH == "arm64" else os.path.join(lib, "x86_64-unix")
    for name in ("ws2_32.so", "ntdll.so", "win32u.so"):
        path = os.path.join(unix, name)
        if os.path.exists(path):
            code, output, _ = run(["ldd", path], timeout=30)
            note(f"ldd {name}: " + " / ".join(l.strip() for l in output.splitlines() if "not found" in l or "Error" in l) or "ok")
        else:
            note(f"{name}: missing")


def main():
    log("arch", f"{ARCH} {os.uname().machine} {os.uname().release}")
    log("page_size", os.sysconf("SC_PAGE_SIZE"))
    log("wine_files", mb(du("/opt/tabstudio/wine") if os.path.isdir("/opt/tabstudio/wine") else du("/usr/lib/wine")))
    dotnet = ensure_dotnet()
    log("dotnet_size", mb(du(dotnet)))
    xvfb = start_xvfb()
    if xvfb.poll() is not None:
        note("Xvfb: " + open(os.path.join(OUT, "xvfb.txt")).read()[-1500:])
        return

    log("wine_binary", WINE)
    code, output, elapsed = run([WINE, "--version"], timeout=60)
    log("wine_version", f"{output.strip()} ({elapsed:.1f}s)")
    if os.path.isdir(PREFIX):
        shutil.rmtree(PREFIX)
    boot_env = dict(ENV)
    boot_env["WINEDLLOVERRIDES"] = "mscoree=;mshtml=;winemenubuilder.exe=;winedbg.exe="
    code, output, elapsed = run([WINE, "wineboot", "-i"], timeout=600, env=boot_env)
    _, _, waited = run([WINESERVER, "-w"], timeout=300)
    log("wineboot_seconds", f"{elapsed + waited:.1f} exit={code}")
    if code != 0:
        note("wineboot output: " + output[-2000:])
        diagnose()
    log("prefix_size", mb(du(PREFIX)))
    dedupe_prefix()
    os.symlink(dotnet, os.path.join(PREFIX, "drive_c", "dotnet"))
    code, elapsed = registry(
        "[HKEY_CURRENT_USER\\Software\\Wine\\Explorer]\n\"Desktop\"=\"Default\"\n\n"
        f"[HKEY_CURRENT_USER\\Software\\Wine\\Explorer\\Desktops]\n\"Default\"=\"{DESKTOP[0]}x{DESKTOP[1]}\"\n\n"
        "[HKEY_LOCAL_MACHINE\\Software\\Microsoft\\Windows NT\\CurrentVersion\\AeDebug]\n\"Debugger\"=\"\"\n\"Auto\"=\"0\"\n\n")
    log("registry_seconds", f"{elapsed:.1f} exit={code}")

    forms = os.path.join(SPIKE, "apps", "SpikeForms", "SpikeForms.dll")
    wpf = os.path.join(SPIKE, "apps", "SpikeWpf", "SpikeWpf.dll")
    code, output, elapsed = run([WINE, "C:\\dotnet\\dotnet.exe", "--info"], timeout=300)
    log("dotnet_info_seconds", f"{elapsed:.1f} exit={code}")
    note("dotnet --info: " + " / ".join(l.strip() for l in output.splitlines()[:16]))
    code, output, elapsed = run(dotnet_command(forms, "--hello"), timeout=120)
    log("forms_hello_seconds", f"{elapsed:.1f} exit={code} hello={'HELLO' in output}")
    if "HELLO" not in output:
        note("hello output: " + output[-2000:])

    # A persistent session: wineserver and the virtual desktop stay up between the app's runs.
    run([WINESERVER, "-p"], timeout=30)
    session = subprocess.Popen([WINE, "explorer", f"/desktop=Default,{DESKTOP[0]}x{DESKTOP[1]}"], env=ENV,
                               stdout=open(os.path.join(OUT, "explorer.txt"), "w"), stderr=subprocess.STDOUT)
    time.sleep(3)
    code, output, elapsed = run(dotnet_command(forms, "--hello"), timeout=120)
    log("forms_hello_session_seconds", f"{elapsed:.1f} exit={code}")

    fb = Framebuffer(FB)
    log("framebuffer", f"{fb.width}x{fb.height} bpp={fb.bpp} stride={fb.stride} order={fb.byte_order} masks={fb.red:x}/{fb.green:x}/{fb.blue:x}")
    fb.png(os.path.join(OUT, "desktop.png"), 0, 0, *DESKTOP)

    program, parts = show_app(fb, "forms", dotnet_command(forms))
    if program is not None:
        exercise(fb, "forms", program, parts)
        if not QUICK:
            resize(fb, program)
        program.stop()
        time.sleep(1)
        program, parts = show_app(fb, "forms_again", dotnet_command(forms))
        if program is not None:
            program.stop()
            time.sleep(1)
    if not QUICK:
        animate(fb, "forms", forms)

    for mode in ("hardware", "software"):
        if mode == "software":
            registry("[HKEY_CURRENT_USER\\Software\\Microsoft\\Avalon.Graphics]\n\"DisableHWAcceleration\"=dword:00000001\n\n")
        label = f"wpf_{mode}"
        program, parts = show_app(fb, label, dotnet_command(wpf))
        if program is not None:
            exercise(fb, label, program, parts)
            program.stop()
            time.sleep(1)
            if not QUICK:
                animate(fb, label, wpf)

    log("prefix_size_after", mb(du(PREFIX)))
    session.kill()
    run([WINESERVER, "-k"], timeout=60)
    xvfb.terminate()
    with open(os.path.join(OUT, "results.json"), "w") as f:
        json.dump(results, f, indent=1)
    print("SPIKE-DONE", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as ex:  # report and keep the results so far
        import traceback
        note("spike failed: " + traceback.format_exc().replace("\n", " / "))
        print("SPIKE-DONE (failed)", flush=True)
