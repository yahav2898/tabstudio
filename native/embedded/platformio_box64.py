"""Explicit native-first PlatformIO 6.1.18 host-package fallback.

Only a missing compatible native registry artifact permits x86-64 selection.
Original packages remain byte-for-byte intact in a private sibling tree. Native
Python launchers in a symlink overlay start original x86 binaries through Box64;
GCC sees its original path/name and locates its original cc1/as/ld children.
"""
import hashlib
import json
import os
from pathlib import Path
import platform
import struct
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
import box64_runtime


def x86_executable(path):
    if not path.is_file() or not os.access(path, os.X_OK):
        return False
    with path.open("rb") as source:
        header = source.read(64)
        if len(header) != 64 or header[:7] != b"\x7fELF\x02\x01\x01" or header[18:20] != b"\x3e\x00":
            return False
        kind = struct.unpack_from("<H", header, 16)[0]
        if kind == 2:  # ET_EXEC, including static GCC helpers.
            return True
        if kind != 3:
            return False
        # Shared libraries may be executable too. Only PIEs with an interpreter
        # receive launchers; libraries must remain unchanged symlink targets.
        offset = struct.unpack_from("<Q", header, 32)[0]
        size, count = struct.unpack_from("<HH", header, 54)
        if size < 56 or count > 1024:
            return False
        for index in range(count):
            source.seek(offset + index * size)
            entry = source.read(4)
            if entry == b"\x03\x00\x00\x00":  # PT_INTERP
                return True
    return False


def launcher(original, interpreter=sys.executable):
    # Native subprocess avoids the shell/exec/vfork chain covered by the PIC
    # static-clone regressions. Every original recipe argument is preserved.
    return "\n".join([
        "#!" + interpreter,
        "import os, subprocess, sys",
        "sys.path.insert(0, '/opt/tabstudio/embedded')",
        "import box64_runtime",
        "subprocess._USE_VFORK = False; subprocess._USE_POSIX_SPAWN = False",
        "args, env = box64_runtime.command(" + repr(str(original)) + ", sys.argv[1:])",
        "env['PATH'] = " + repr(str(original.parent) + ":") + " + env.get('PATH', '/usr/bin:/bin')",
        "fds = ()",
        "if env.get('TABSTUDIO_USB_FD') == '3':",
        "    os.fstat(3); fds = (3,)",
        "result = subprocess.call(args, env=env, pass_fds=fds)",
        "raise SystemExit(result if result >= 0 else 128 - result)", ""])


def overlay(package, interpreter=sys.executable):
    package = Path(package).absolute()
    stamp = package / ".tabstudio-box64-overlay.json"
    if stamp.exists():
        # Refresh scripts after an app update, preserving all original binaries.
        record = json.loads(stamp.read_text())
        original = Path(record["original"])
        if not original.is_dir():
            raise ValueError("The original translated package is missing")
    else:
        originals = package.parent / ".tabstudio-box64-originals"
        originals.mkdir(exist_ok=True)
        identity = hashlib.sha256(str(package).encode()).hexdigest()[:16]
        original = originals / (package.name + "-" + identity)
        if original.exists():
            raise ValueError("An original package is already retained; remove the incomplete private overlay before retrying")
        package.rename(original)
        package.mkdir()
    launchers = []
    for folder, directories, files in os.walk(original, followlinks=False):
        relative = Path(folder).relative_to(original)
        output = package / relative
        output.mkdir(parents=True, exist_ok=True)
        for name in list(directories):
            source = Path(folder) / name
            if source.is_symlink():
                link = output / name
                if not link.exists() and not link.is_symlink():
                    link.symlink_to(source)
                directories.remove(name)
        for name in files:
            source = Path(folder) / name
            destination = output / name
            if x86_executable(source):
                if destination.is_symlink():
                    destination.unlink()
                destination.write_text(launcher(source, interpreter))
                destination.chmod(0o755)
                launchers.append(str(source.relative_to(original)))
            elif not destination.exists() and not destination.is_symlink():
                destination.symlink_to(source)
    stamp.write_text(json.dumps({"original": str(original), "launchers": launchers}, indent=2))
    print("Box64 package overlay:", package.name, "original binaries unchanged:", len(launchers), flush=True)


def configure_policy():
    import platformio
    from platformio.package.manager.tool import ToolPackageManager
    if platformio.__version__ != "6.1.18":
        raise ValueError("The Box64 package policy is verified only with pinned PlatformIO Core 6.1.18")
    original_compatible = ToolPackageManager.is_system_compatible
    original_versions = ToolPackageManager.get_compatible_registry_versions
    original_file = ToolPackageManager.pick_compatible_pkg_file
    original_script = ToolPackageManager.call_pkg_script

    def compatible(value, custom_system=None):
        if custom_system:
            return original_compatible(value, custom_system)
        return original_compatible(value, "linux_aarch64") or original_compatible(value, "linux_x86_64")

    def versions(self, entries, spec=None, custom_system=None):
        if custom_system:
            return original_versions(self, entries, spec, custom_system)
        native = original_versions(self, entries, spec, "linux_aarch64")
        return native or original_versions(self, entries, spec, "linux_x86_64")

    def artifact(self, files, custom_system=None):
        if custom_system:
            return original_file(self, files, custom_system)
        native = original_file(self, files, "linux_aarch64")
        selected = native or original_file(self, files, "linux_x86_64")
        if selected is not None and native is None:
            print("Explicit Box64 fallback: no ARM64 artifact; SHA-256 verification remains owned by PlatformIO:", selected.get("name", selected.get("download_url")), flush=True)
        return selected

    def scripts(self, pkg, event):
        manifest = self.load_manifest(pkg)
        systems = manifest.get("system")
        if (not original_compatible(systems, "linux_aarch64")
                and original_compatible(systems, "linux_x86_64")):
            overlay(pkg.path)
        return original_script(self, pkg, event)

    ToolPackageManager.is_system_compatible = staticmethod(compatible)
    ToolPackageManager.get_compatible_registry_versions = versions
    ToolPackageManager.pick_compatible_pkg_file = artifact
    ToolPackageManager.call_pkg_script = scripts
    # Existing private packages must be refreshed before SCons sees their tools.
    manager = ToolPackageManager()
    for pkg in manager.get_installed():
        systems = manager.load_manifest(pkg).get("system")
        if not original_compatible(systems, "linux_aarch64") and original_compatible(systems, "linux_x86_64"):
            overlay(pkg.path)


def main():
    if os.environ.get("TABSTUDIO_PLATFORMIO_BOX64") != "1":
        raise ValueError("Box64 package fallback requires explicit project hostPackageMode")
    if platform.machine() not in ("aarch64", "x86_64"):
        raise ValueError("Box64 host packages require 64-bit ARM or x64")
    # An isolated Core cache prevents a boxed project from changing native package
    # choices in another project. Native availability is evaluated per requirement.
    private = Path.cwd() / ".tabstudio/platformio-boxed"
    os.environ["PLATFORMIO_CORE_DIR"] = str(private)
    if platform.machine() == "aarch64":
        configure_policy()
    from platformio.__main__ import main as pio_main
    arguments = sys.argv[1:]
    if arguments[:1] == ["--"]:
        arguments = arguments[1:]
    pio_main(["pio", *arguments])


if __name__ == "__main__":
    main()
