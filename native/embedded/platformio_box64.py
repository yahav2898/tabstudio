"""Native-first PlatformIO 6.1.18 host-package fallback with executable ELF checks.

A missing compatible native artifact or incompatible actual ELF architecture
permits x86-64 selection; metadata mismatches retain the exact package version.
Original packages remain byte-for-byte intact in a private sibling tree. Native
Python launchers in a symlink overlay start original x86 binaries through Box64;
GCC sees its original path/name and locates its original cc1/as/ld children.
"""
import hashlib
import json
import os
from pathlib import Path
import platform
import runpy
import struct
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
import box64_runtime


def executable_architecture(path):
    """Read executable ELF headers, without trusting a registry's system label.

    Cross-compiler objects and shared libraries without an interpreter are not
    host programs. The executable mode and ELF type are
    checked before auditing the host architecture.
    """
    path = Path(path)
    if not path.is_file() or not os.access(path, os.X_OK):
        return None
    with path.open("rb") as source:
        header = source.read(64)
        if header[:4] != b"\x7fELF":
            return None
        if len(header) < 52 or header[4] not in (1, 2) or header[5] not in (1, 2) or (header[4] == 2 and len(header) < 64):
            raise ValueError("Invalid executable ELF header: " + str(path))
        byte_order = "<" if header[5] == 1 else ">"
        kind, machine = struct.unpack_from(byte_order + "HH", header, 16)
        if kind not in (2, 3):
            return None
        bits = header[4]
        if kind == 3:
            offset = struct.unpack_from(byte_order + ("I" if bits == 1 else "Q"), header, 28 if bits == 1 else 32)[0]
            size, count = struct.unpack_from(byte_order + "HH", header, 42 if bits == 1 else 54)
            minimum = 32 if bits == 1 else 56
            if size < minimum or count > 1024 or offset + size * count > path.stat().st_size:
                raise ValueError("Invalid executable ELF program headers: " + str(path))
            for index in range(count):
                source.seek(offset + index * size)
                if source.read(4) == struct.pack(byte_order + "I", 3):
                    break
            else:
                return None  # ET_DYN libraries may have their execute bit set.
        if bits == 2 and header[5] == 1 and machine in (62, 183):
            return "x86_64" if machine == 62 else "aarch64"
        return "ELF%d machine %d" % (32 if bits == 1 else 64, machine)


def package_architectures(package):
    result = {}
    for folder, _, files in os.walk(package, followlinks=False):
        for name in files:
            path = Path(folder) / name
            architecture = executable_architecture(path)
            if architecture:
                result.setdefault(architecture, []).append(str(path.relative_to(package)))
    return result


def x86_executable(path):
    return executable_architecture(path) == "x86_64"


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
        previous = 1
        while original.exists():
            # PackageManager may replace/update the visible package. Retain its
            # preceding original too; never execute it as the new distribution.
            previous += 1
            original = originals / (package.name + "-" + identity + "-" + str(previous))
        package.rename(original)
        package.mkdir()
        # Publish recovery metadata before constructing the overlay. An interrupted
        # refresh resumes from this pristine tree, rather than wrapping wrappers.
        stamp.write_text(json.dumps({"original": str(original), "launchers": []}, indent=2))
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


def configure_scons_subprocess():
    """Carry this policy into Core's separate, unchanged SCons Python process.

    Patching only Core's CLI leaves SCons filtering the installed x64 package out
    on ARM64. A narrow bootstrap preserves original argv and script bytes without
    global PYTHONPATH/sitecustomize changes that can break vendor SDK imports.
    """
    import platformio
    from platformio import proc
    original = proc.exec_command
    builder = Path(platformio.__file__).resolve().parent / "builder/main.py"

    def execute(*values, **options):
        if values and isinstance(values[0], (list, tuple)):
            arguments = list(values[0])
            if len(arguments) > 2 and Path(str(arguments[1])).name == "scons.py" and "--sconstruct" in arguments:
                index = arguments.index("--sconstruct")
                if index + 1 < len(arguments) and Path(arguments[index + 1]).resolve() == builder:
                    arguments = [arguments[0], "-P", str(Path(__file__).resolve()), "--scons", *arguments[1:]]
                    values = (arguments, *values[1:])
                    if os.environ.get("TABSTUDIO_USB_FD") == "3":
                        os.fstat(3)
                        options = {**options, "pass_fds": tuple(sorted(set(options.get("pass_fds", ())) | {3}))}
        return original(*values, **options)

    proc.exec_command = execute


def configure_policy(architecture=None, allow_fallback=True):
    import platformio
    from platformio.package.manager.tool import ToolPackageManager
    from platformio.package.meta import PackageSpec
    from platformio.registry.mirror import RegistryFileMirrorIterator
    if platformio.__version__ != "6.1.18":
        raise ValueError("The Box64 package policy is verified only with pinned PlatformIO Core 6.1.18")
    architecture = architecture or platform.machine()
    if architecture not in ("aarch64", "x86_64"):
        raise ValueError("PlatformIO package checking requires ARM64 or x64")
    native_system = "linux_" + architecture
    translated = architecture == "aarch64" and allow_fallback
    original_registry = ToolPackageManager.install_from_registry
    original_compatible = ToolPackageManager.is_system_compatible
    original_versions = ToolPackageManager.get_compatible_registry_versions
    original_file = ToolPackageManager.pick_compatible_pkg_file
    original_script = ToolPackageManager.call_pkg_script

    def compatible(value, custom_system=None):
        if custom_system:
            return original_compatible(value, custom_system)
        return original_compatible(value, native_system) or (translated and original_compatible(value, "linux_x86_64"))

    def versions(self, entries, spec=None, custom_system=None):
        if custom_system:
            return original_versions(self, entries, spec, custom_system)
        native = original_versions(self, entries, spec, native_system)
        return native or (original_versions(self, entries, spec, "linux_x86_64") if translated else [])

    def artifact(self, files, custom_system=None):
        if custom_system:
            return original_file(self, files, custom_system)
        native = original_file(self, files, native_system)
        selected = native or (original_file(self, files, "linux_x86_64") if translated else None)
        if selected is not None and native is None:
            print("Box64 fallback: no ARM64 artifact; SHA-256 verification remains owned by PlatformIO:", selected.get("name", selected.get("download_url")), flush=True)
        return selected

    def registry_match(self, packages, spec):
        # Upstream's owner-less search checks only each latest version first.
        # Fetch complete candidate histories before permitting an x86 fallback;
        # otherwise a newer x86 release could hide a compatible older ARM build.
        histories = [(package, self.fetch_registry_package(PackageSpec(
            id=package["id"], owner=package["owner"]["username"], name=package["name"])).get("versions", []))
                     for package in packages]
        for system in (native_system, "linux_x86_64") if translated else (native_system,):
            for package, history in histories:
                version = self.pick_best_registry_version(history, spec, custom_system=system)
                if version:
                    return package, version
        return None, None

    def checked(self, pkg):
        inventory = package_architectures(Path(pkg.path))
        permitted = {architecture, "x86_64"} if translated else {architecture}
        rejected = set(inventory) - permitted
        if rejected:
            if not translated or pkg.metadata.spec.external:
                raise ValueError("Refusing incompatible PlatformIO executable(s) in %s: %s; registry system labels do not override ELF headers" % (pkg.path, ", ".join(sorted(rejected))))
            # A published ARM64 artifact can contain ARM32 programs (the Nordic
            # sreccat package is an example). Retry only this exact package and
            # version's x64 distribution, through PlatformIO's SHA-256 downloader.
            identity = PackageSpec(owner=pkg.metadata.spec.owner, id=pkg.metadata.spec.id, name=pkg.metadata.name)
            package = self.fetch_registry_package(identity)
            version = next((entry for entry in package.get("versions", [])
                            if str(entry["name"]) == str(pkg.metadata.version)), None)
            artifact = original_file(self, version["files"], "linux_x86_64") if version else None
            if artifact is None:
                raise ValueError("No exact x64 replacement for mislabeled PlatformIO package %s@%s (%s)" % (pkg.metadata.name, pkg.metadata.version, ", ".join(sorted(rejected))))
            print("Box64 fallback: native artifact ELF architecture disagrees with its registry label:", pkg.metadata.name, sorted(rejected), flush=True)
            retained = Path(pkg.path).parent / ".tabstudio-rejected-native"
            retained.mkdir(exist_ok=True)
            destination = retained / (Path(pkg.path).name + "-" + hashlib.sha256(str(pkg.metadata.version).encode()).hexdigest()[:12])
            suffix = 1
            while destination.exists():
                suffix += 1
                destination = destination.with_name(destination.name + "-" + str(suffix))
            Path(pkg.path).rename(destination)
            self.memcache_reset()
            replacement = None
            for url, checksum in RegistryFileMirrorIterator(artifact["download_url"]):
                try:
                    replacement = self.install_from_uri(url, identity, checksum or artifact["checksum"]["sha256"])
                    break
                except Exception as error:
                    self.log.warning("Package mirror failed: %s", error)
            if replacement is None:
                raise ValueError("Could not download the exact verified x64 replacement for " + pkg.metadata.name)
            inventory = package_architectures(Path(replacement.path))
            if set(inventory) - {"x86_64"}:
                raise ValueError("The replacement PlatformIO artifact also contains incompatible host executables")
            if str(replacement.metadata.version) != str(pkg.metadata.version):
                raise ValueError("The replacement PlatformIO package changed version")
            pkg = replacement
        if translated and "x86_64" in inventory:
            overlay(pkg.path)
            if "x86_64" in package_architectures(Path(pkg.path)):
                raise ValueError("A PlatformIO package contains x64 executables outside its retained original overlay")
        return pkg

    def registry(self, spec, search_qualifiers=None):
        pkg = original_registry(self, spec, search_qualifiers)
        return checked(self, pkg) if pkg else pkg

    def scripts(self, pkg, event):
        # Runs before a downloaded postinstall.py, including custom URI packages.
        pkg = checked(self, pkg)
        result = original_script(self, pkg, event)
        # A postinstall script may create additional tools. Do not let a newly
        # generated foreign executable bypass the pre-install architecture audit.
        remaining = set(package_architectures(Path(pkg.path))) - {architecture}
        if remaining:
            raise ValueError("PlatformIO postinstall produced incompatible host executables: " + ", ".join(sorted(remaining)))
        return result

    ToolPackageManager.is_system_compatible = staticmethod(compatible)
    ToolPackageManager.get_compatible_registry_versions = versions
    ToolPackageManager.pick_compatible_pkg_file = artifact
    ToolPackageManager.find_best_registry_version = registry_match
    ToolPackageManager.install_from_registry = registry
    ToolPackageManager.call_pkg_script = scripts
    manager = ToolPackageManager()
    for pkg in manager.get_installed():
        checked(manager, pkg)
    configure_scons_subprocess()


def main():
    mode = os.environ.get("TABSTUDIO_PLATFORMIO_BOX64")
    if mode not in ("1", "native"):
        raise ValueError("A PlatformIO native-first or native-only package policy is required")
    if platform.machine() not in ("aarch64", "x86_64"):
        raise ValueError("Box64 host packages require 64-bit ARM or x64")
    # An isolated Core cache prevents a boxed project from changing native package
    # choices in another project. Native availability is evaluated per requirement.
    if mode == "1":
        private = Path.cwd() / ".tabstudio/platformio-boxed"
        os.environ["PLATFORMIO_CORE_DIR"] = str(private)
    os.environ.pop("PLATFORMIO_SYSTEM_TYPE", None)
    configure_policy(allow_fallback=mode == "1")
    arguments = sys.argv[1:]
    if arguments[:1] == ["--"]:
        arguments = arguments[1:]
    if arguments[:1] == ["--scons"]:
        if len(arguments) < 2 or Path(arguments[1]).name != "scons.py" or not Path(arguments[1]).is_file():
            raise ValueError("The PlatformIO child is missing its original SCons entry point")
        sys.argv = arguments[1:]
        sys.path.insert(0, str(Path(arguments[1]).resolve().parent))
        runpy.run_path(arguments[1], run_name="__main__")
        return 0
    from platformio.__main__ import main as pio_main
    return pio_main(["pio", *arguments])


if __name__ == "__main__":
    raise SystemExit(main())
